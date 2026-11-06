using Admins.Contract;
using Admins.Database;
using Admins.Database.Models;
using Admins.Models;
using Admins.Services.Access;
using Admins.Services.Server;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Services.Bans;

public sealed partial class BansService : IBansManager, IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<BansService> _logger;
    private readonly AdminsDatabase _database;
    private readonly ServerService _server;
    private readonly AccessService _access;
    private readonly Messenger _messenger;
    private readonly TargetResolver _targets;
    private readonly MenuShortcuts _shortcuts;
    private readonly IDisposable? _configSubscription;
    private volatile BansConfiguration _config;
    private CancellationTokenSource? _syncTimer;

    public BansService(
        ISwiftlyCore core,
        ILogger<BansService> logger,
        IOptionsMonitor<Configuration> config,
        AdminsDatabase database,
        ServerService server,
        AccessService access,
        Messenger messenger,
        TargetResolver targets,
        MenuShortcuts shortcuts)
    {
        _core = core;
        _logger = logger;
        _database = database;
        _server = server;
        _access = access;
        _messenger = messenger;
        _targets = targets;
        _shortcuts = shortcuts;
        _config = config.CurrentValue.Bans;

        _configSubscription = config.OnChange(c =>
        {
            var restart = c.Bans.DatabaseSyncIntervalSeconds != _config.DatabaseSyncIntervalSeconds;
            _config = c.Bans;
            if (restart) _core.Scheduler.NextTick(RestartSyncTimer);
        });

        core.Registrator.Register(this);
        RestartSyncTimer();
    }

    public BansConfiguration Config => _config;

    public event Action<IBan>? OnAdminBanAdded;

    public event Action<IBan>? OnAdminBanUpdated;

    public event Action<IBan>? OnAdminBanRemoved;

    #region Enforcement

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var playerId = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            if (!_database.Enabled) return;

            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player is not { IsFakeClient: false, IsAuthorized: true }) return;

            var target = PlayerRef.Of(player);

            _ = Task.Run(async () =>
            {
                try
                {
                    if (await FindActiveBanAsync((long)target.SteamId, target.Ip).ConfigureAwait(false) is Ban ban)
                    {
                        _core.Scheduler.NextTick(() => Kick(target, ban));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ban check failed for {SteamId}.", target.SteamId);
                }
            });
        });
    }

    [EventListener<EventDelegates.OnClientSteamAuthorizeFail>]
    public void OnClientSteamAuthorizeFail(IOnClientSteamAuthorizeFailEvent e)
    {
        if (!_config.KickOnSteamAuthorizationFailure) return;

        _core.PlayerManager.GetPlayer(e.PlayerId)?.Kick("Steam authorization failed.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_AUTHINVALID);
    }

    private void RestartSyncTimer()
    {
        _syncTimer?.Cancel();
        _syncTimer = null;

        var interval = _config.DatabaseSyncIntervalSeconds;
        if (interval > 0)
        {
            _syncTimer = _core.Scheduler.DelayAndRepeatBySeconds(interval, interval, CheckOnlinePlayers);
        }
    }

    // One query for the whole server instead of one per player.
    private void CheckOnlinePlayers()
    {
        if (!_database.Enabled) return;

        var online = _messenger.Humans().Where(p => p.IsAuthorized).Select(PlayerRef.Of).ToList();
        if (online.Count == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var steamIds = online.Select(p => (long)p.SteamId).Where(id => id != 0).Distinct().ToList();
                var ips = online.Select(p => p.Ip).Where(ip => ip.Length > 0).Distinct().ToList();
                var now = AdminsDatabase.NowMs;
                var guid = _server.Guid;

                using var db = _database.Connect();
                var bans = (await db.SelectAsync<Ban>(b =>
                    ((b.BanType == BanType.SteamID && steamIds.Contains(b.SteamId64)) || (b.BanType == BanType.IP && ips.Contains(b.PlayerIp)))
                    && (b.ExpiresAt == 0 || b.ExpiresAt > now)
                    && (b.Server == guid || b.GlobalBan == true)).ConfigureAwait(false)).ToList();

                if (bans.Count == 0) return;

                _core.Scheduler.NextTick(() =>
                {
                    foreach (var player in online)
                    {
                        if (bans.FirstOrDefault(b => Matches(b, player.SteamId, player.Ip)) is { } ban) Kick(player, ban);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periodic ban check failed.");
            }
        });
    }

    private void KickMatching(Ban ban)
    {
        foreach (var player in _messenger.Humans().Select(PlayerRef.Of).ToList())
        {
            if (Matches(ban, player.SteamId, player.Ip)) Kick(player, ban);
        }
    }

    private static bool Matches(Ban ban, ulong steamId, string ip) =>
        ban.BanType == BanType.SteamID
            ? steamId != 0 && (ulong)ban.SteamId64 == steamId
            : ip.Length > 0 && Net.NormalizeIp(ban.PlayerIp) == ip;

    private void Kick(PlayerRef target, Ban ban)
    {
        if (target.Resolve(_core) is not { } player) return;

        var message = _messenger.Text(player, "ban.kick_message", ban.Reason, _messenger.Timestamp(player, ban.ExpiresAt), ban.AdminName, ban.AdminSteamId64);
        player.SendConsole(message);
        player.Kick(message, ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
    }

    #endregion

    #region Issuing and lifting

    /// <summary>
    /// Bans an online player. Game thread; the caller has already checked permission and immunity.
    /// </summary>
    public void BanPlayer(Issuer issuer, IPlayer target, BanType type, TimeSpan duration, string reason, bool global)
    {
        var player = PlayerRef.Of(target);

        if (type == BanType.IP && player.Ip.Length == 0)
        {
            _messenger.Reply(issuer, "ban.no_ip", player.Name);
            return;
        }

        Store(issuer, NewBan(issuer, (long)player.SteamId, player.Name, player.Ip, type, duration, reason, global), player.Name);
    }

    /// <summary>
    /// Bans a SteamID64 or IP that is not on the server. Game thread; the caller has already checked immunity.
    /// </summary>
    public void BanOffline(Issuer issuer, ulong steamId64, string? ip, BanType type, TimeSpan duration, string reason, bool global)
    {
        var address = Net.NormalizeIp(ip);
        var display = type == BanType.IP ? address : steamId64.ToString();

        Store(issuer, NewBan(issuer, (long)steamId64, "Unknown", address, type, duration, reason, global), display);
    }

    private Ban NewBan(Issuer issuer, long steamId64, string name, string ip, BanType type, TimeSpan duration, string reason, bool global)
    {
        var now = AdminsDatabase.NowMs;

        return new Ban
        {
            SteamId64 = steamId64,
            PlayerName = name,
            PlayerIp = ip,
            BanType = type,
            ExpiresAt = Durations.ExpiresAt(duration, now),
            Length = (long)duration.TotalMilliseconds,
            Reason = reason,
            AdminSteamId64 = (long)issuer.SteamId,
            AdminName = issuer.Name,
            Server = _server.Guid,
            GlobalBan = global,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // Kicks even when storing fails: the player still has to leave, and the admin is told the ban was not saved.
    private void Store(Issuer issuer, Ban ban, string display)
    {
        _ = Task.Run(async () =>
        {
            var stored = false;

            if (_database.Enabled)
            {
                try
                {
                    using var db = _database.Connect();
                    ban.Id = Convert.ToInt64(await db.InsertAsync(ban).ConfigureAwait(false));
                    stored = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store a ban for {Target}.", display);
                    _messenger.ReplyLater(issuer, "ban.not_saved", display);
                }
            }

            _core.Scheduler.NextTick(() =>
            {
                KickMatching(ban);
                if (stored) Raise(OnAdminBanAdded, ban);

                if (_messenger.IsConnected(issuer))
                {
                    _messenger.Reply(issuer, ban.GlobalBan ? "ban.applied_global" : "ban.applied", issuer.Name, display, _messenger.Duration(issuer.Player, ban.Length), ban.Reason);
                }
            });
        });
    }

    /// <summary>
    /// Active bans that apply to this server, newest first.
    /// </summary>
    public async Task<IReadOnlyList<Ban>> ListActiveAsync(int limit)
    {
        if (!_database.Enabled) return [];

        var now = AdminsDatabase.NowMs;
        var guid = _server.Guid;

        using var db = _database.Connect();
        var bans = await db.SelectAsync<Ban>(b => (b.ExpiresAt == 0 || b.ExpiresAt > now) && (b.Server == guid || b.GlobalBan == true)).ConfigureAwait(false);
        return [.. bans.OrderByDescending(b => b.Id).Take(limit)];
    }

    /// <summary>
    /// Expires the given bans and raises <see cref="OnAdminBanRemoved"/>. Returns how many were lifted.
    /// </summary>
    public async Task<int> LiftAsync(IReadOnlyList<Ban> bans)
    {
        if (bans.Count == 0 || !_database.Enabled) return 0;

        var now = AdminsDatabase.NowMs;
        using (var db = _database.Connect())
        {
            foreach (var ban in bans)
            {
                ban.ExpiresAt = now;
                ban.UpdatedAt = now;
                await db.UpdateAsync(ban).ConfigureAwait(false);
            }
        }

        _core.Scheduler.NextTick(() =>
        {
            foreach (var ban in bans) Raise(OnAdminBanRemoved, ban);
        });

        return bans.Count;
    }

    private async Task<IReadOnlyList<Ban>> FindApplicableActiveAsync(ulong? steamId64, string? ip)
    {
        var now = AdminsDatabase.NowMs;
        var guid = _server.Guid;

        using var db = _database.Connect();
        IEnumerable<Ban> bans;

        if (steamId64 is { } steamId)
        {
            var id = (long)steamId;
            bans = await db.SelectAsync<Ban>(b => b.SteamId64 == id && (b.ExpiresAt == 0 || b.ExpiresAt > now) && (b.Server == guid || b.GlobalBan == true)).ConfigureAwait(false);
        }
        else
        {
            var address = Net.NormalizeIp(ip);
            var withPort = address + ":";
            bans = await db.SelectAsync<Ban>(b =>
                b.BanType == BanType.IP
                && (b.PlayerIp == address || b.PlayerIp.StartsWith(withPort))
                && (b.ExpiresAt == 0 || b.ExpiresAt > now)
                && (b.Server == guid || b.GlobalBan == true)).ConfigureAwait(false);
        }

        return [.. bans];
    }

    #endregion

    #region IBansManager

    public async Task<IReadOnlyList<IBan>> FindBansAsync(long? steamId64 = null, string? playerIp = null, BanType? banType = null, RecordStatus status = RecordStatus.All)
    {
        if (!_database.Enabled) return [];

        var address = Net.NormalizeIp(playerIp);
        var withPort = address + ":";
        var now = AdminsDatabase.NowMs;

        using var db = _database.Connect();
        IEnumerable<Ban> bans = (steamId64, address.Length > 0) switch
        {
            ({ } id, true) => await db.SelectAsync<Ban>(b => b.SteamId64 == id || b.PlayerIp == address || b.PlayerIp.StartsWith(withPort)).ConfigureAwait(false),
            ({ } id, false) => await db.SelectAsync<Ban>(b => b.SteamId64 == id).ConfigureAwait(false),
            (null, true) => await db.SelectAsync<Ban>(b => b.PlayerIp == address || b.PlayerIp.StartsWith(withPort)).ConfigureAwait(false),
            _ => await db.GetAllAsync<Ban>().ConfigureAwait(false),
        };

        return [.. bans.Where(b => (banType is null || b.BanType == banType) && StatusMatches(b.IsActive(now), status))];
    }

    public async Task<IBan?> FindActiveBanAsync(long steamId64, string? playerIp)
    {
        if (!_database.Enabled) return null;

        var address = Net.NormalizeIp(playerIp);
        var withPort = address + ":";
        var now = AdminsDatabase.NowMs;
        var guid = _server.Guid;

        using var db = _database.Connect();
        var bans = address.Length == 0
            ? await db.SelectAsync<Ban>(b =>
                b.BanType == BanType.SteamID && b.SteamId64 == steamId64
                && (b.ExpiresAt == 0 || b.ExpiresAt > now)
                && (b.Server == guid || b.GlobalBan == true)).ConfigureAwait(false)
            : await db.SelectAsync<Ban>(b =>
                ((b.BanType == BanType.SteamID && b.SteamId64 == steamId64) || (b.BanType == BanType.IP && (b.PlayerIp == address || b.PlayerIp.StartsWith(withPort))))
                && (b.ExpiresAt == 0 || b.ExpiresAt > now)
                && (b.Server == guid || b.GlobalBan == true)).ConfigureAwait(false);

        return bans.OrderBy(b => b.BanType).FirstOrDefault();
    }

    public async Task<IBan> AddBanAsync(IBan ban)
    {
        var row = Ban.From(ban);
        var now = AdminsDatabase.NowMs;
        row.Id = 0;
        if (row.CreatedAt == 0) row.CreatedAt = now;
        row.UpdatedAt = now;
        row.PlayerIp = Net.NormalizeIp(row.PlayerIp);
        if (string.IsNullOrEmpty(row.Server)) row.Server = _server.Guid;

        if (_database.Enabled)
        {
            using var db = _database.Connect();
            row.Id = Convert.ToInt64(await db.InsertAsync(row).ConfigureAwait(false));
        }

        _core.Scheduler.NextTick(() =>
        {
            if (row.IsActive(AdminsDatabase.NowMs)) KickMatching(row);
            Raise(OnAdminBanAdded, row);
        });

        return row;
    }

    public async Task UpdateBanAsync(IBan ban)
    {
        if (!_database.Enabled) return;

        var row = Ban.From(ban);
        row.UpdatedAt = AdminsDatabase.NowMs;

        using (var db = _database.Connect())
        {
            await db.UpdateAsync(row).ConfigureAwait(false);
        }

        _core.Scheduler.NextTick(() =>
        {
            if (row.IsActive(AdminsDatabase.NowMs)) KickMatching(row);
            Raise(OnAdminBanUpdated, row);
        });
    }

    public async Task RemoveBanAsync(IBan ban)
    {
        if (!_database.Enabled) return;

        Ban? row;
        var id = ban.Id;
        using (var db = _database.Connect())
        {
            row = await db.GetAsync<Ban>(id).ConfigureAwait(false);
        }

        if (row is not null && row.IsActive(AdminsDatabase.NowMs)) await LiftAsync([row]).ConfigureAwait(false);
    }

    #endregion

    private void Raise(Action<IBan>? handler, IBan ban)
    {
        try
        {
            handler?.Invoke(ban);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A ban event handler threw.");
        }
    }

    private static bool StatusMatches(bool active, RecordStatus status) => status switch
    {
        RecordStatus.Active => active,
        RecordStatus.Expired => !active,
        _ => true,
    };

    public void Dispose()
    {
        _syncTimer?.Cancel();
        _configSubscription?.Dispose();
    }
}
