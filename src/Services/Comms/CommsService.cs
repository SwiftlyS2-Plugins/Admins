using Admins.Contract;
using Admins.Database;
using Admins.Database.Models;
using Admins.Models;
using Admins.Services.Access;
using Admins.Services.Presence;
using Admins.Services.Server;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Comms;

public enum CommsAction
{
    Gag,
    Mute,
    Silence,
}

public sealed partial class CommsService : ICommsManager, IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<CommsService> _logger;
    private readonly AdminsDatabase _database;
    private readonly ServerService _server;
    private readonly AccessService _access;
    private readonly Messenger _messenger;
    private readonly TargetResolver _targets;
    private readonly MenuShortcuts _shortcuts;
    private readonly PresenceService _presence;
    private readonly IDisposable? _configSubscription;
    private readonly CancellationTokenSource _voiceTimer;
    private volatile CommsConfiguration _config;
    private CancellationTokenSource? _syncTimer;

    // Game thread only. Keyed by slot because the SteamID already reads 0 when the disconnect event fires.
    private readonly Dictionary<int, CacheEntry> _cache = [];
    private readonly Dictionary<int, int> _versions = [];
    private readonly Dictionary<int, VoiceFlagValue> _savedVoice = [];

    public CommsService(
        ISwiftlyCore core,
        ILogger<CommsService> logger,
        IOptionsMonitor<Configuration> config,
        AdminsDatabase database,
        ServerService server,
        AccessService access,
        Messenger messenger,
        TargetResolver targets,
        MenuShortcuts shortcuts,
        PresenceService presence)
    {
        _core = core;
        _logger = logger;
        _database = database;
        _server = server;
        _access = access;
        _messenger = messenger;
        _targets = targets;
        _shortcuts = shortcuts;
        _presence = presence;
        _config = config.CurrentValue.Comms;

        _configSubscription = config.OnChange(c =>
        {
            var restart = c.Comms.DatabaseSyncIntervalSeconds != _config.DatabaseSyncIntervalSeconds;
            _config = c.Comms;
            if (restart) _core.Scheduler.NextTick(RestartSyncTimer);
        });

        core.Registrator.Register(this);
        _voiceTimer = core.Scheduler.DelayAndRepeatBySeconds(1f, 1f, EnforceVoice);
        presence.Changed += OnPresenceChanged;
        RestartSyncTimer();

        // Covers a hot reload; on a cold start nobody is connected yet.
        core.Scheduler.NextTick(LoadOnline);
    }

    public CommsConfiguration Config => _config;

    public event Action<ISanction>? OnAdminSanctionAdded;

    public event Action<ISanction>? OnAdminSanctionUpdated;

    public event Action<ISanction>? OnAdminSanctionRemoved;

    private sealed record CacheEntry(ulong SessionId, List<Sanction> Sanctions);

    public static IReadOnlyList<SanctionKind> KindsOf(CommsAction action) => action switch
    {
        CommsAction.Gag => [SanctionKind.Gag],
        CommsAction.Mute => [SanctionKind.Mute],
        _ => [SanctionKind.Gag, SanctionKind.Mute],
    };

    #region Cache and enforcement

    /// <summary>
    /// The longest-running active sanction of a kind for an online player. Game thread.
    /// </summary>
    public Sanction? GetActive(IPlayer player, SanctionKind kind)
    {
        if (!_cache.TryGetValue(player.Slot, out var entry) || entry.SessionId != player.SessionId) return null;

        var now = AdminsDatabase.NowMs;
        return entry.Sanctions
            .Where(s => s.SanctionKind == kind && s.IsActive(now))
            .OrderByDescending(s => s.ExpiresAt == 0 ? long.MaxValue : s.ExpiresAt)
            .FirstOrDefault();
    }

    public ISanction? GetActiveSanction(IPlayer player, SanctionKind kind) => GetActive(player, kind);

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var playerId = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            if (_core.PlayerManager.GetPlayer(playerId) is { IsFakeClient: false } player) Load([PlayerRef.Of(player)]);
        });
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        _cache.Remove(e.PlayerId);
        _savedVoice.Remove(e.PlayerId);
        Bump(e.PlayerId);
    }

    private void RestartSyncTimer()
    {
        _syncTimer?.Cancel();
        _syncTimer = null;

        var interval = _config.DatabaseSyncIntervalSeconds;
        if (interval > 0)
        {
            _syncTimer = _core.Scheduler.DelayAndRepeatBySeconds(interval, interval, LoadOnline);
        }
    }

    private void LoadOnline() => Load([.. _messenger.Humans().Where(p => p.IsAuthorized).Select(PlayerRef.Of)]);

    // A sanction issued or lifted while the query runs bumps the slot's version, and the stale result is
    // re-queried instead of overwriting the fresher cache entry.
    private void Load(IReadOnlyList<PlayerRef> players)
    {
        if (!_database.Enabled || players.Count == 0) return;

        var versions = players.ToDictionary(p => p.Slot, p => _versions.GetValueOrDefault(p.Slot));

        _ = Task.Run(async () =>
        {
            try
            {
                var active = await FindActiveForAsync(players).ConfigureAwait(false);

                _core.Scheduler.NextTick(() =>
                {
                    var stale = new List<PlayerRef>();

                    foreach (var player in players)
                    {
                        if (!_core.PlayerManager.IsSessionIdValid(player.SessionId)) continue;

                        if (_versions.GetValueOrDefault(player.Slot) != versions[player.Slot])
                        {
                            stale.Add(player);
                            continue;
                        }

                        _cache[player.Slot] = new CacheEntry(player.SessionId, [.. active.Where(s => Matches(s, player.SteamId, player.Ip))]);
                    }

                    if (stale.Count > 0) Load(stale);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load sanctions for {Count} player(s).", players.Count);
            }
        });
    }

    private async Task<IReadOnlyList<Sanction>> FindActiveForAsync(IReadOnlyList<PlayerRef> players)
    {
        var steamIds = players.Select(p => (long)p.SteamId).Where(id => id != 0).Distinct().ToList();
        var ips = players.Select(p => p.Ip).Where(ip => ip.Length > 0).Distinct().ToList();
        var now = AdminsDatabase.NowMs;
        var guid = _server.Guid;

        using var db = _database.Connect();
        var rows = await db.SelectAsync<Sanction>(s =>
            ((s.SanctionType == SanctionType.SteamID && steamIds.Contains(s.SteamId64)) || (s.SanctionType == SanctionType.IP && ips.Contains(s.PlayerIp)))
            && (s.ExpiresAt == 0 || s.ExpiresAt > now)
            && (s.Server == guid || s.GlobalSanction == true)).ConfigureAwait(false);

        return [.. rows];
    }

    private static bool Matches(Sanction sanction, ulong steamId, string ip) =>
        sanction.SanctionType == SanctionType.SteamID
            ? steamId != 0 && (ulong)sanction.SteamId64 == steamId
            : ip.Length > 0 && Net.NormalizeIp(sanction.PlayerIp) == ip;

    private void Bump(int slot) => _versions[slot] = _versions.GetValueOrDefault(slot) + 1;

    private void OnPresenceChanged() => _core.Scheduler.NextTick(EnforceVoice);

    // A hidden admin is muted too, so an open mic cannot give them away.
    private void EnforceVoice()
    {
        foreach (var player in _messenger.Humans().ToList())
        {
            var slot = player.Slot;
            var mute = GetActive(player, SanctionKind.Mute);

            if (mute is not null || _presence.IsHidden(player))
            {
                var flags = player.VoiceFlags;
                if (flags == VoiceFlagValue.Muted) continue;

                _savedVoice.TryAdd(slot, flags);
                player.VoiceFlags = VoiceFlagValue.Muted;
                if (mute is not null) _messenger.Chat(player, "mute.message", mute.AdminName, _messenger.Timestamp(player, mute.ExpiresAt), mute.Reason);
            }
            else if (_savedVoice.Remove(slot, out var original))
            {
                player.VoiceFlags = original;
            }
        }
    }

    [ClientChatHookHandler]
    public HookResult OnClientChat(int playerId, string text, bool teamonly)
    {
        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player is null || player.IsFakeClient) return HookResult.Continue;

        var config = _config;
        var marker = config.AdminChatStartCharacter;

        // Checked before the gag on purpose: a gagged player can still reach the admins.
        if (teamonly && config.EnableAdminChat && !string.IsNullOrEmpty(marker) && text.StartsWith(marker, StringComparison.Ordinal))
        {
            SendAdminChat(player, text[marker.Length..].Trim());
            return HookResult.Stop;
        }

        // Commands already ran before this hook; only the chat line itself would give a hidden admin away.
        if (_presence.IsHidden(player))
        {
            if (text.Length > 0 && text[0] is not ('!' or '/')) _messenger.Chat(player, "hide.chat_blocked");
            return HookResult.Stop;
        }

        if (GetActive(player, SanctionKind.Gag) is { } gag)
        {
            _messenger.Chat(player, "gag.message", gag.AdminName, _messenger.Timestamp(player, gag.ExpiresAt), gag.Reason);
            return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    private void SendAdminChat(IPlayer sender, string message)
    {
        if (message.Length == 0) return;

        var name = Names.Of(sender);
        var key = _core.Permission.PlayerHasPermission(sender.SteamID, "admins.chat") ? "admins.admin_chat_format" : "admins.player_chat_format";

        foreach (var player in _messenger.Humans())
        {
            if (player.Slot == sender.Slot || _core.Permission.PlayerHasPermission(player.SteamID, "admins.chat"))
            {
                player.SendChat(_messenger.Text(player, key, name, message));
            }
        }
    }

    #endregion

    #region Issuing and lifting

    /// <summary>
    /// Sanctions an online player. Game thread; the caller has already checked permission and immunity.
    /// </summary>
    public void SanctionPlayer(Issuer issuer, IPlayer target, CommsAction action, SanctionType type, TimeSpan duration, string reason, bool global)
    {
        var player = PlayerRef.Of(target);

        if (type == SanctionType.IP && player.Ip.Length == 0)
        {
            _messenger.Reply(issuer, "comms.no_ip", player.Name);
            return;
        }

        Store(issuer, Create(issuer, (long)player.SteamId, player.Name, player.Ip, action, type, duration, reason, global), action, player.Name);
    }

    /// <summary>
    /// Sanctions a SteamID64 or IP that is not on the server. Game thread; the caller has already checked immunity.
    /// </summary>
    public void SanctionOffline(Issuer issuer, ulong steamId64, string? ip, CommsAction action, SanctionType type, TimeSpan duration, string reason, bool global)
    {
        var address = Net.NormalizeIp(ip);
        var display = type == SanctionType.IP ? address : steamId64.ToString();

        Store(issuer, Create(issuer, (long)steamId64, "Unknown", address, action, type, duration, reason, global), action, display);
    }

    private List<Sanction> Create(Issuer issuer, long steamId64, string name, string ip, CommsAction action, SanctionType type, TimeSpan duration, string reason, bool global)
    {
        var now = AdminsDatabase.NowMs;

        return
        [
            .. KindsOf(action).Select(kind => new Sanction
            {
                SteamId64 = steamId64,
                PlayerName = name,
                PlayerIp = ip,
                SanctionType = type,
                SanctionKind = kind,
                ExpiresAt = Durations.ExpiresAt(duration, now),
                Length = (long)duration.TotalMilliseconds,
                Reason = reason,
                AdminSteamId64 = (long)issuer.SteamId,
                AdminName = issuer.Name,
                Server = _server.Guid,
                GlobalSanction = global,
                CreatedAt = now,
                UpdatedAt = now,
            }),
        ];
    }

    // Applies in memory even when storing fails, so the player is silenced for the rest of the session.
    private void Store(Issuer issuer, List<Sanction> sanctions, CommsAction action, string display)
    {
        _ = Task.Run(async () =>
        {
            var stored = false;

            if (_database.Enabled)
            {
                try
                {
                    using var db = _database.Connect();
                    foreach (var sanction in sanctions)
                    {
                        sanction.Id = Convert.ToInt64(await db.InsertAsync(sanction).ConfigureAwait(false));
                    }

                    stored = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store a sanction for {Target}.", display);
                    _messenger.ReplyLater(issuer, "comms.not_saved", display);
                }
            }

            _core.Scheduler.NextTick(() =>
            {
                Apply(sanctions);
                if (stored) foreach (var sanction in sanctions) Raise(OnAdminSanctionAdded, sanction);

                if (_messenger.IsConnected(issuer))
                {
                    var first = sanctions[0];
                    _messenger.Reply(issuer, "comms.applied", issuer.Name, ActionLabel(issuer.Player, action, first.GlobalSanction), display, _messenger.Duration(issuer.Player, first.Length), first.Reason);
                }
            });
        });
    }

    private void Apply(IReadOnlyList<Sanction> sanctions)
    {
        var now = AdminsDatabase.NowMs;

        foreach (var player in _messenger.Humans().ToList())
        {
            var reference = PlayerRef.Of(player);
            var matched = sanctions.Where(s => s.IsActive(now) && Matches(s, reference.SteamId, reference.Ip)).ToList();
            if (matched.Count == 0) continue;

            Bump(reference.Slot);

            if (!_cache.TryGetValue(reference.Slot, out var entry) || entry.SessionId != reference.SessionId)
            {
                entry = new CacheEntry(reference.SessionId, []);
                _cache[reference.Slot] = entry;
            }

            entry.Sanctions.AddRange(matched);

            if (matched.FirstOrDefault(s => s.SanctionKind == SanctionKind.Gag) is { } gag)
            {
                _messenger.Chat(player, "gag.message", gag.AdminName, _messenger.Timestamp(player, gag.ExpiresAt), gag.Reason);
            }
        }

        EnforceVoice();
    }

    /// <summary>
    /// Expires the given sanctions, drops them from the cache and raises <see cref="OnAdminSanctionRemoved"/>.
    /// </summary>
    public async Task<int> LiftAsync(IReadOnlyList<Sanction> sanctions)
    {
        if (sanctions.Count == 0) return 0;

        if (_database.Enabled)
        {
            var now = AdminsDatabase.NowMs;
            using var db = _database.Connect();
            foreach (var sanction in sanctions.Where(s => s.Id != 0))
            {
                sanction.ExpiresAt = now;
                sanction.UpdatedAt = now;
                await db.UpdateAsync(sanction).ConfigureAwait(false);
            }
        }

        await _core.Scheduler.NextTickAsync(() => Forget(sanctions)).ConfigureAwait(false);
        return sanctions.Count;
    }

    private void Forget(IReadOnlyList<Sanction> sanctions)
    {
        var ids = sanctions.Where(s => s.Id != 0).Select(s => s.Id).ToHashSet();

        foreach (var (slot, entry) in _cache)
        {
            if (entry.Sanctions.RemoveAll(s => sanctions.Contains(s) || (s.Id != 0 && ids.Contains(s.Id))) > 0) Bump(slot);
        }

        foreach (var sanction in sanctions) Raise(OnAdminSanctionRemoved, sanction);

        EnforceVoice();
    }

    /// <summary>
    /// Active sanctions that apply to this server, newest first.
    /// </summary>
    public async Task<IReadOnlyList<Sanction>> ListActiveAsync(int limit)
    {
        if (!_database.Enabled) return [];

        var now = AdminsDatabase.NowMs;
        var guid = _server.Guid;

        using var db = _database.Connect();
        var rows = await db.SelectAsync<Sanction>(s => (s.ExpiresAt == 0 || s.ExpiresAt > now) && (s.Server == guid || s.GlobalSanction == true)).ConfigureAwait(false);
        return [.. rows.OrderByDescending(s => s.Id).Take(limit)];
    }

    private async Task<IReadOnlyList<Sanction>> FindApplicableActiveAsync(IReadOnlyList<(ulong SteamId, string Ip)> subjects, IReadOnlyList<SanctionKind> kinds)
    {
        var steamIds = subjects.Select(s => (long)s.SteamId).Where(id => id != 0).Distinct().ToList();
        var ips = subjects.Select(s => s.Ip).Where(ip => ip.Length > 0).Distinct().ToList();
        if (steamIds.Count == 0 && ips.Count == 0) return [];

        var kindList = kinds.ToList();
        var now = AdminsDatabase.NowMs;
        var guid = _server.Guid;

        using var db = _database.Connect();
        var rows = await db.SelectAsync<Sanction>(s =>
            (steamIds.Contains(s.SteamId64) || (s.SanctionType == SanctionType.IP && ips.Contains(s.PlayerIp)))
            && kindList.Contains(s.SanctionKind)
            && (s.ExpiresAt == 0 || s.ExpiresAt > now)
            && (s.Server == guid || s.GlobalSanction == true)).ConfigureAwait(false);

        return [.. rows];
    }

    public string ActionLabel(IPlayer? viewer, CommsAction action, bool global) => _messenger.Text(viewer, (global, action) switch
    {
        (true, CommsAction.Gag) => "global_gag",
        (true, CommsAction.Mute) => "global_mute",
        (true, _) => "global_silence",
        (false, CommsAction.Gag) => "gag",
        (false, CommsAction.Mute) => "mute",
        _ => "silence",
    });

    #endregion

    #region ICommsManager

    public async Task<IReadOnlyList<ISanction>> FindSanctionsAsync(long? steamId64 = null, string? playerIp = null, SanctionKind? sanctionKind = null, SanctionType? sanctionType = null, RecordStatus status = RecordStatus.All)
    {
        if (!_database.Enabled) return [];

        var address = Net.NormalizeIp(playerIp);
        var now = AdminsDatabase.NowMs;

        using var db = _database.Connect();
        IEnumerable<Sanction> rows = (steamId64, address.Length > 0) switch
        {
            ({ } id, true) => await db.SelectAsync<Sanction>(s => s.SteamId64 == id || s.PlayerIp == address).ConfigureAwait(false),
            ({ } id, false) => await db.SelectAsync<Sanction>(s => s.SteamId64 == id).ConfigureAwait(false),
            (null, true) => await db.SelectAsync<Sanction>(s => s.PlayerIp == address).ConfigureAwait(false),
            _ => await db.GetAllAsync<Sanction>().ConfigureAwait(false),
        };

        return
        [
            .. rows.Where(s =>
                (sanctionKind is null || s.SanctionKind == sanctionKind)
                && (sanctionType is null || s.SanctionType == sanctionType)
                && status switch
                {
                    RecordStatus.Active => s.IsActive(now),
                    RecordStatus.Expired => !s.IsActive(now),
                    _ => true,
                }),
        ];
    }

    public async Task<ISanction> AddSanctionAsync(ISanction sanction)
    {
        var row = Sanction.From(sanction);
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
            Apply([row]);
            Raise(OnAdminSanctionAdded, row);
        });

        return row;
    }

    public async Task UpdateSanctionAsync(ISanction sanction)
    {
        if (!_database.Enabled) return;

        var row = Sanction.From(sanction);
        row.UpdatedAt = AdminsDatabase.NowMs;

        using (var db = _database.Connect())
        {
            await db.UpdateAsync(row).ConfigureAwait(false);
        }

        _core.Scheduler.NextTick(() =>
        {
            LoadOnline();
            Raise(OnAdminSanctionUpdated, row);
        });
    }

    public async Task RemoveSanctionAsync(ISanction sanction)
    {
        if (!_database.Enabled) return;

        Sanction? row;
        var id = sanction.Id;
        using (var db = _database.Connect())
        {
            row = await db.GetAsync<Sanction>(id).ConfigureAwait(false);
        }

        if (row is not null && row.IsActive(AdminsDatabase.NowMs)) await LiftAsync([row]).ConfigureAwait(false);
    }

    #endregion

    private void Raise(Action<ISanction>? handler, ISanction sanction)
    {
        try
        {
            handler?.Invoke(sanction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A sanction event handler threw.");
        }
    }

    // Unloading must not leave anyone muted by a plugin that is no longer there to unmute them.
    public void Dispose()
    {
        _voiceTimer.Cancel();
        _syncTimer?.Cancel();
        _configSubscription?.Dispose();
        _presence.Changed -= OnPresenceChanged;

        foreach (var (slot, flags) in _savedVoice)
        {
            if (_core.PlayerManager.GetPlayer(slot) is { } player) player.VoiceFlags = flags;
        }

        _savedVoice.Clear();
    }
}
