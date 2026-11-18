using Admins.Contract;
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

namespace Admins.Services.Access;

/// <summary>
/// Admins, groups, the permissions they grant and the immunity rules between them.
/// </summary>
public sealed partial class AccessService : IAdminsManager, IGroupsManager, IDisposable
{
    private static readonly IReadOnlySet<string> NoPermissions = new HashSet<string>();

    private readonly ISwiftlyCore _core;
    private readonly ILogger<AccessService> _logger;
    private readonly AdminsDatabase _database;
    private readonly ServerService _server;
    private readonly Messenger _messenger;
    private readonly IDisposable? _configSubscription;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    // Game thread only. Keyed by slot because the SteamID reads 0 once a client is disconnecting.
    private readonly Dictionary<int, Grant> _grants = [];

    private volatile Snapshot _snapshot = Snapshot.Empty;
    private volatile AccessConfiguration _config;
    private ImmunityMode _immunityMode;
    private CancellationTokenSource? _syncTimer;
    private bool _disposed;

    public AccessService(
        ISwiftlyCore core,
        ILogger<AccessService> logger,
        IOptionsMonitor<Configuration> config,
        AdminsDatabase database,
        ServerService server,
        Messenger messenger)
    {
        _core = core;
        _logger = logger;
        _database = database;
        _server = server;
        _messenger = messenger;
        _config = config.CurrentValue.Admins;
        _immunityMode = config.CurrentValue.ImmunityMode;

        _configSubscription = config.OnChange(c =>
        {
            var restart = c.Admins.DatabaseSyncIntervalSeconds != _config.DatabaseSyncIntervalSeconds;
            _config = c.Admins;
            _immunityMode = c.ImmunityMode;
            if (restart) _core.Scheduler.NextTick(RestartSyncTimer);
        });

        _server.GuidChanged += ReconcileAll;
        core.Registrator.Register(this);

        RestartSyncTimer();
        RunReload();
    }

    public event Action<IPlayer, IAdmin>? OnAdminLoad;

    #region Lookups

    public IAdmin? GetAdmin(ulong steamId64) => AdminHere(steamId64);

    public IAdmin? GetAdmin(IPlayer player) => AdminHere(player.SteamID);

    public IAdmin? GetAdmin(int playerId) => _core.PlayerManager.GetPlayer(playerId) is { } player ? GetAdmin(player) : null;

    public IReadOnlyList<IAdmin> GetAllAdmins() => _snapshot.Admins;

    public IGroup? GetGroup(string groupName) => _snapshot.GroupsByName.GetValueOrDefault(groupName.Trim());

    public IReadOnlyList<IGroup> GetAllGroups() => _snapshot.Groups;

    public IReadOnlyList<IGroup> GetAdminGroups(IAdmin admin) => [.. GroupsHere(admin, _snapshot)];

    public IReadOnlyList<IGroup> GetPlayerGroups(IPlayer player) => GetAdmin(player) is { } admin ? GetAdminGroups(admin) : [];

    public int GetImmunity(ulong steamId64) => AdminHere(steamId64) is { } admin ? ImmunityOf(admin, _snapshot) : 0;

    public bool CanTarget(IPlayer? admin, ulong targetSteamId64)
    {
        if (admin is null) return true;

        var adminSteamId = admin.SteamID;
        if (adminSteamId == targetSteamId64) return true;

        var snapshot = _snapshot;
        if (AdminHere(targetSteamId64, snapshot) is not { } target) return true;

        var a = AdminHere(adminSteamId, snapshot) is { } self ? ImmunityOf(self, snapshot) : 0;
        var t = ImmunityOf(target, snapshot);

        return _immunityMode switch
        {
            ImmunityMode.IgnoreImmunity => true,
            ImmunityMode.ProtectFromLowerAccess => a >= t,
            ImmunityMode.ProtectFromEqualOrLowerAccess => a > t,
            ImmunityMode.ProtectWithNoImmunityBypass => (a == 0 && t == 0) || a > t,
            _ => false,
        };
    }

    private IAdmin? AdminHere(ulong steamId64) => AdminHere(steamId64, _snapshot);

    private Admin? AdminHere(ulong steamId64, Snapshot snapshot) =>
        steamId64 != 0 && snapshot.AdminsBySteamId.TryGetValue(steamId64, out var admin) && AppliesHere(admin.Servers) ? admin : null;

    private IEnumerable<IGroup> GroupsHere(IAdmin admin, Snapshot snapshot)
    {
        foreach (var name in admin.Groups)
        {
            if (name is not null && snapshot.GroupsByName.TryGetValue(name.Trim(), out var group) && AppliesHere(group.Servers))
            {
                yield return group;
            }
        }
    }

    private int ImmunityOf(IAdmin admin, Snapshot snapshot) =>
        GroupsHere(admin, snapshot).Select(g => g.Immunity).Append(admin.Immunity).Max();

    private bool AppliesHere(IEnumerable<string> servers)
    {
        var guid = _server.Guid;
        return servers.Any(s => string.Equals(s?.Trim(), guid, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Loading

    public Task RefreshAsync() => ReloadAsync();

    public void SetAdmins(IEnumerable<IAdmin> admins)
    {
        var current = _snapshot;
        _snapshot = new Snapshot([.. admins.Select(Admin.From)], current.Groups);
        _core.Scheduler.NextTick(ReconcileAll);
    }

    public void SetGroups(IEnumerable<IGroup> groups)
    {
        var current = _snapshot;
        _snapshot = new Snapshot(current.Admins, [.. groups.Select(Group.From)]);
        _core.Scheduler.NextTick(ReconcileAll);
    }

    /// <summary>
    /// Reloads from the database and completes once permissions are re-applied on the game thread.
    /// </summary>
    public async Task ReloadAsync()
    {
        if (_database.Enabled)
        {
            await _reloadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using var db = _database.Connect();
                var groups = (await db.GetAllAsync<Group>().ConfigureAwait(false)).ToList();
                var admins = (await db.GetAllAsync<Admin>().ConfigureAwait(false)).ToList();
                _snapshot = new Snapshot(admins, groups);
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        await _core.Scheduler.NextTickAsync(ReconcileAll).ConfigureAwait(false);
    }

    private void RunReload() =>
        _ = Task.Run(async () =>
        {
            try
            {
                await ReloadAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load admins and groups; keeping the previous list.");
            }
        });

    private void RestartSyncTimer()
    {
        _syncTimer?.Cancel();
        _syncTimer = null;

        var interval = _config.DatabaseSyncIntervalSeconds;
        if (interval > 0)
        {
            // DelayAndRepeat, not RepeatBySeconds: the latter fires immediately and would double the startup load. A reload
            // still waiting on a slow database is not queued behind.
            _syncTimer = _core.Scheduler.DelayAndRepeatGuarded(interval, interval, _logger, () =>
            {
                if (_reloadLock.CurrentCount > 0) RunReload();
            });
        }
    }

    #endregion

    #region Permission grants

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var playerId = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player is { IsFakeClient: false, IsAuthorized: true }) Apply(player);
        });
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e) => Revoke(e.PlayerId);

    private void ReconcileAll()
    {
        // ReloadAsync gets here through NextTickAsync, which SwiftlyS2 still runs after an unload.
        if (_disposed) return;

        var connected = new HashSet<int>();

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player.IsFakeClient || !player.IsAuthorized) continue;

            connected.Add(player.Slot);
            Apply(player);
        }

        foreach (var slot in _grants.Keys.Where(slot => !connected.Contains(slot)).ToList())
        {
            Revoke(slot);
        }
    }

    // Applies only the difference to the previous grant, so a reload never leaves a window in which an
    // online admin has no permissions.
    private void Apply(IPlayer player)
    {
        var steamId = player.SteamID;
        if (steamId == 0) return;

        var snapshot = _snapshot;
        var admin = AdminHere(steamId, snapshot);
        var permissions = admin is null ? NoPermissions : PermissionsOf(admin, snapshot);

        if (_grants.TryGetValue(player.Slot, out var current) && (current.SteamId != steamId || current.SessionId != player.SessionId))
        {
            Revoke(player.Slot);
            current = null;
        }

        var previous = current?.Permissions ?? NoPermissions;

        foreach (var permission in previous)
        {
            if (!permissions.Contains(permission)) _core.Permission.RemovePermission(steamId, permission);
        }

        foreach (var permission in permissions)
        {
            if (!previous.Contains(permission)) _core.Permission.AddPermission(steamId, permission);
        }

        if (admin is null)
        {
            _grants.Remove(player.Slot);
            return;
        }

        var fingerprint = $"{admin.Id}|{ImmunityOf(admin, snapshot)}|{string.Join(',', permissions.Order(StringComparer.Ordinal))}";
        _grants[player.Slot] = new Grant(steamId, player.SessionId, fingerprint, permissions);

        if (current?.Fingerprint == fingerprint) return;

        try
        {
            OnAdminLoad?.Invoke(player, admin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An OnAdminLoad handler threw.");
        }
    }

    private void Revoke(int slot)
    {
        if (!_grants.Remove(slot, out var grant)) return;

        foreach (var permission in grant.Permissions)
        {
            _core.Permission.RemovePermission(grant.SteamId, permission);
        }
    }

    private IReadOnlySet<string> PermissionsOf(Admin admin, Snapshot snapshot)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var permission in admin.Permissions.Concat(GroupsHere(admin, snapshot).SelectMany(g => g.Permissions)))
        {
            var trimmed = permission?.Trim();
            if (!string.IsNullOrEmpty(trimmed)) permissions.Add(trimmed);
        }

        return permissions;
    }

    #endregion

    public void Dispose()
    {
        _disposed = true;
        _syncTimer?.Cancel();
        _configSubscription?.Dispose();
        _server.GuidChanged -= ReconcileAll;

        foreach (var slot in _grants.Keys.ToList())
        {
            Revoke(slot);
        }
    }

    private sealed record Grant(ulong SteamId, ulong SessionId, string Fingerprint, IReadOnlySet<string> Permissions);

    private sealed class Snapshot
    {
        public static readonly Snapshot Empty = new([], []);

        public Snapshot(IReadOnlyList<Admin> admins, IReadOnlyList<Group> groups)
        {
            Admins = admins;
            Groups = groups;

            foreach (var admin in admins)
            {
                AdminsBySteamId[(ulong)admin.SteamId64] = admin;
            }

            foreach (var group in groups)
            {
                GroupsByName.TryAdd(group.Name.Trim(), group);
            }
        }

        public IReadOnlyList<Admin> Admins { get; }

        public IReadOnlyList<Group> Groups { get; }

        public Dictionary<ulong, Admin> AdminsBySteamId { get; } = [];

        public Dictionary<string, Group> GroupsByName { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
