using Admins.Database;
using Admins.Database.Models;
using Admins.Services.Server;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

/// <summary>
/// Players who left in the last few minutes, so a player who leaves to dodge a punishment can still be punished from
/// the menu. Kept in memory, and refilled from <c>player_sessions</c> when the plugin loads. Game thread only.
/// </summary>
public sealed class RecentPlayers
{
    private const int Capacity = 50;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ISwiftlyCore _core;
    private readonly Dictionary<int, (ulong SessionId, string Ip)> _ips = [];
    private readonly List<Entry> _entries = [];

    public RecentPlayers(ISwiftlyCore core, ILogger<RecentPlayers> logger, AdminsDatabase database, ServerService server)
    {
        _core = core;
        core.Registrator.Register(this);

        if (database.Enabled) Load(logger, database, server.Guid);
    }

    public sealed record Entry(ulong SteamId, string Name, string Ip, DateTime LeftAt);

    // A session still open was left so by a crash or an unload: it ended at its last heartbeat.
    private void Load(ILogger logger, AdminsDatabase database, string guid)
    {
        var since = AdminsDatabase.NowMs - (long)Window.TotalMilliseconds;

        _ = Task.Run(async () =>
        {
            try
            {
                using var db = database.Connect();
                var rows = await db.FromAsync<PlayerSession>(q => q.Select()
                    .Where(s => s.Server == guid && (s.DisconnectedAt >= since || (s.DisconnectedAt == 0 && s.LastSeenAt >= since)))
                    .OrderByDescending(s => s.Id)
                    .Page(1, Capacity * 4)).ConfigureAwait(false);

                var loaded = rows
                    .Select(s => new Entry((ulong)s.SteamId64, s.PlayerName, s.PlayerIp, DateTimeOffset.FromUnixTimeMilliseconds(s.DisconnectedAt != 0 ? s.DisconnectedAt : s.LastSeenAt).UtcDateTime))
                    .ToList();

                _core.Scheduler.NextTick(() => Merge(loaded));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Loading the players who recently left failed.");
            }
        });
    }

    // A leave seen since the plugin loaded is newer than the stored one of the same player.
    private void Merge(IReadOnlyList<Entry> loaded)
    {
        foreach (var entry in loaded.OrderByDescending(e => e.LeftAt))
        {
            if (!_entries.Any(e => e.SteamId == entry.SteamId)) _entries.Add(entry);
        }

        _entries.Sort((a, b) => b.LeftAt.CompareTo(a.LeftAt));
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);
    }

    [EventListener<EventDelegates.OnClientPutInServer>]
    public void OnClientPutInServer(IOnClientPutInServerEvent e)
    {
        if (e.Kind != ClientKind.Player) return;

        var slot = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            if (_core.PlayerManager.GetPlayer(slot) is { IsFakeClient: false } player) _ips[slot] = (player.SessionId, Net.NormalizeIp(player.IPAddress));
        });
    }

    // The leaver's SteamID and name are only readable from the event at this point.
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect e)
    {
        if (e.XuID == 0 || e.UserIdPlayer is not { } player) return HookResult.Continue;

        var ip = _ips.TryGetValue(player.Slot, out var known) && known.SessionId == player.SessionId ? known.Ip : string.Empty;

        _entries.RemoveAll(entry => entry.SteamId == e.XuID);
        _entries.Insert(0, new Entry(e.XuID, e.Name, ip, DateTime.UtcNow));
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);

        return HookResult.Continue;
    }

    /// <summary>Newest first, without anyone who has come back.</summary>
    public IReadOnlyList<Entry> List()
    {
        var cutoff = DateTime.UtcNow - Window;
        _entries.RemoveAll(entry => entry.LeftAt < cutoff);

        var online = _core.PlayerManager.GetAllPlayers().Where(p => !p.IsFakeClient).Select(p => p.SteamID).ToHashSet();
        return [.. _entries.Where(entry => !online.Contains(entry.SteamId))];
    }
}
