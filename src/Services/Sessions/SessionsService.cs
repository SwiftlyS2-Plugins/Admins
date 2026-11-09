using System.Data;
using Admins.Database;
using Admins.Database.Models;
using Admins.Models;
using Admins.Services.Server;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Sessions;

/// <summary>
/// Records each connection as a <see cref="PlayerSession"/>: opened once Steam authorizes the player, kept current by a
/// heartbeat, closed when they leave. Game thread, except the writes.
/// </summary>
public sealed partial class SessionsService : IDisposable
{
    private const float HeartbeatSeconds = 60f;
    private const float CleanupSeconds = 6 * 3600f;

    private readonly ISwiftlyCore _core;
    private readonly ILogger<SessionsService> _logger;
    private readonly IOptionsMonitor<Configuration> _config;
    private readonly AdminsDatabase _database;
    private readonly ServerService _server;
    private readonly Messenger _messenger;
    private readonly TargetResolver _targets;
    private readonly CancellationTokenSource _heartbeat;
    private readonly CancellationTokenSource _cleanup;

    // Game thread only, keyed by slot.
    private readonly Dictionary<int, Tracked> _tracked = [];

    // Every write is chained on the one before, in the order the game thread asked for them, so no session is updated
    // before its insert lands. Replaced on the game thread only.
    private Task _writes = Task.CompletedTask;
    private volatile bool _disposed;

    public SessionsService(
        ISwiftlyCore core,
        ILogger<SessionsService> logger,
        IOptionsMonitor<Configuration> config,
        AdminsDatabase database,
        ServerService server,
        Messenger messenger,
        TargetResolver targets)
    {
        _core = core;
        _logger = logger;
        _config = config;
        _database = database;
        _server = server;
        _messenger = messenger;
        _targets = targets;

        CloseLeftOpen();
        Cleanup();

        core.Registrator.Register(this);
        _heartbeat = core.Scheduler.DelayAndRepeatBySeconds(HeartbeatSeconds, HeartbeatSeconds, Heartbeat);
        _cleanup = core.Scheduler.DelayAndRepeatBySeconds(CleanupSeconds, CleanupSeconds, Cleanup);

        // Covers a hot reload; on a cold start nobody is connected yet.
        core.Scheduler.NextTick(Heartbeat);
    }

    private sealed class Tracked(ulong sessionId, PlayerSession row)
    {
        public ulong SessionId { get; } = sessionId;

        /// <summary>Only touched by the queued writes once the first one is queued.</summary>
        public PlayerSession Row { get; } = row;
    }

    private bool Recording => _database.Enabled && _config.CurrentValue.Sessions.Enabled;

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var slot = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            if (!Recording || _core.PlayerManager.GetPlayer(slot) is not { IsFakeClient: false, IsAuthorized: true } player || player.SteamID == 0) return;
            if (_tracked.TryGetValue(slot, out var tracked) && tracked.SessionId == player.SessionId) return;

            // From the connection, not from Steam's answer a few seconds later.
            Track(player, AdminsDatabase.NowMs - player.ConnectedTime * 1000L);
        });
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        if (_tracked.Remove(e.PlayerId, out var tracked)) Close(tracked, AdminsDatabase.NowMs);
    }

    private void Track(IPlayer player, long connectedAt)
    {
        var now = AdminsDatabase.NowMs;
        if (_tracked.Remove(player.Slot, out var stale)) Close(stale, now);

        var row = new PlayerSession
        {
            SteamId64 = (long)player.SteamID,
            PlayerName = Names.Of(player),
            PlayerIp = Net.NormalizeIp(player.IPAddress),
            Server = _server.Guid,
            ConnectedAt = connectedAt,
            LastSeenAt = now,
        };

        _tracked[player.Slot] = new Tracked(player.SessionId, row);
        Enqueue("open", async db => row.Id = Convert.ToInt64(await db.InsertAsync(row).ConfigureAwait(false)));
    }

    private void Close(Tracked tracked, long at)
    {
        var row = tracked.Row;
        Enqueue("close", async db =>
        {
            if (row.Id == 0) return;

            row.LastSeenAt = at;
            row.DisconnectedAt = at;
            await db.UpdateAsync(row).ConfigureAwait(false);
        });
    }

    // Also starts tracking whoever is not tracked yet: players from before a hot reload, or from before recording was
    // turned on.
    private void Heartbeat()
    {
        var now = AdminsDatabase.NowMs;

        if (!Recording)
        {
            foreach (var tracked in _tracked.Values) Close(tracked, now);
            _tracked.Clear();
            return;
        }

        var beats = new List<(PlayerSession Row, string Name)>();

        foreach (var player in _messenger.Humans().Where(p => p.IsAuthorized && p.SteamID != 0).ToList())
        {
            if (_tracked.TryGetValue(player.Slot, out var tracked) && tracked.SessionId == player.SessionId) beats.Add((tracked.Row, Names.Of(player)));
            else Track(player, now);
        }

        if (beats.Count == 0) return;

        Enqueue("heartbeat", async db =>
        {
            foreach (var (row, name) in beats)
            {
                if (row.Id == 0) continue;

                row.LastSeenAt = now;
                row.PlayerName = name;
                await db.UpdateAsync(row).ConfigureAwait(false);
            }
        });
    }

    // A crash or an unload leaves sessions open; they ended at their last heartbeat. Queued first, so nothing this run
    // opens is among them.
    private void CloseLeftOpen()
    {
        var guid = _server.Guid;

        Enqueue("close the last run's sessions", async db =>
        {
            foreach (var row in await db.SelectAsync<PlayerSession>(s => s.Server == guid && s.DisconnectedAt == 0).ConfigureAwait(false))
            {
                row.DisconnectedAt = row.LastSeenAt;
                await db.UpdateAsync(row).ConfigureAwait(false);
            }
        });
    }

    // Only this server's rows: servers sharing the database may keep theirs for longer.
    private void Cleanup()
    {
        var days = _config.CurrentValue.Sessions.RetentionDays;
        if (days <= 0) return;

        var guid = _server.Guid;
        var cutoff = AdminsDatabase.NowMs - days * 86_400_000L;
        Enqueue("delete old sessions", db => db.DeleteMultipleAsync<PlayerSession>(s => s.Server == guid && s.DisconnectedAt != 0 && s.DisconnectedAt < cutoff));
    }

    private void Enqueue(string what, Func<IDbConnection, Task> write)
    {
        if (!_database.Enabled) return;

        _writes = _writes.ContinueWith(async _ =>
        {
            // After an unload the next instance owns these rows; a late write here could reopen one it just closed.
            if (_disposed) return;

            try
            {
                using var db = _database.Connect();
                await write(db).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Writing player sessions failed ({What}).", what);
            }
        }, TaskScheduler.Default).Unwrap();
    }

    public void Dispose()
    {
        _disposed = true;
        _heartbeat.Cancel();
        _cleanup.Cancel();
    }
}
