using Admins.Contract;
using Admins.Database;
using Admins.Models;
using Admins.Services.Access;
using Admins.Services.Bans;
using Admins.Services.Comms;
using Admins.Services.Esp;
using Admins.Services.PlayerControl;
using Admins.Services.Presence;
using Admins.Services.ServerControl;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;
using Punishment = Admins.Contract.AdminPunishment;

namespace Admins.Services.Menus;

/// <summary>
/// The <c>!admin</c> menu. Pages are built per viewer, filtered by the viewer's permissions, and opened as submenus of
/// the page they are picked from, so Tab always goes back one page. Game thread only.
/// </summary>
public sealed partial class AdminMenuService : IAdminMenuAPI, IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<AdminMenuService> _logger;
    private readonly IOptionsMonitor<Configuration> _config;
    private readonly AdminsDatabase _database;
    private readonly Messenger _messenger;
    private readonly TargetResolver _targets;
    private readonly AccessService _access;
    private readonly BansService _bans;
    private readonly CommsService _comms;
    private readonly PlayerControlService _control;
    private readonly ServerControlService _server;
    private readonly EspService _esp;
    private readonly PresenceService _presence;
    private readonly RecentPlayers _recent;
    private readonly CancellationTokenSource _refresh;
    private readonly Lock _registryLock = new();
    private readonly List<Registration> _registrations = [];

    private readonly HashSet<IMenuSession> _open = [];
    private readonly Dictionary<int, ViewerState> _viewers = [];

    public AdminMenuService(
        ISwiftlyCore core,
        ILogger<AdminMenuService> logger,
        IOptionsMonitor<Configuration> config,
        AdminsDatabase database,
        Messenger messenger,
        TargetResolver targets,
        AccessService access,
        BansService bans,
        CommsService comms,
        PlayerControlService control,
        ServerControlService server,
        EspService esp,
        PresenceService presence,
        RecentPlayers recent,
        MenuShortcuts shortcuts)
    {
        _core = core;
        _logger = logger;
        _config = config;
        _database = database;
        _messenger = messenger;
        _targets = targets;
        _access = access;
        _bans = bans;
        _comms = comms;
        _control = control;
        _server = server;
        _esp = esp;
        _presence = presence;
        _recent = recent;

        shortcuts.OpenBan = (viewer, type, global) => OpenShortcut(viewer, Punishment.Ban, type == BanType.IP, global);
        shortcuts.OpenComms = (viewer, action, type, global) => OpenShortcut(viewer, KindOf(action), type == SanctionType.IP, global);
        shortcuts.OpenKick = viewer => OpenShortcut(viewer, Punishment.Kick, byIp: false, global: false);

        core.Registrator.Register(this);

        // Pages show live state (alive, time left, toggles); frames are only sent when a session is invalidated.
        _refresh = core.Scheduler.DelayAndRepeatBySeconds(1f, 1f, () =>
        {
            _open.RemoveWhere(session => !session.IsOpen);
            foreach (var session in _open) session.Invalidate();
        });
    }

    private sealed record Registration(string Id, string[] Permissions, Func<IPlayer, string> GetTitle, Func<IPlayer, IMenu> CreateMenu);

    private sealed class ViewerState(ulong sessionId)
    {
        public ulong SessionId { get; } = sessionId;

        public Dictionary<Punishment, string> LastReason { get; } = [];

        public Dictionary<Punishment, TimeSpan> LastLength { get; } = [];
    }

    private ViewerState StateOf(IPlayer viewer)
    {
        if (!_viewers.TryGetValue(viewer.Slot, out var state) || state.SessionId != viewer.SessionId)
        {
            state = new ViewerState(viewer.SessionId);
            _viewers[viewer.Slot] = state;
        }

        return state;
    }

    [Command("admin", permission: "admins.commands.admin")]
    public void AdminCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        if (issuer.Player is not { } viewer)
        {
            _messenger.Reply(issuer, "command.player_only");
            return;
        }

        if (context.Args.Length == 0)
        {
            OpenAdminMenu(viewer);
            return;
        }

        var query = string.Join(' ', context.Args);
        var found = _targets.Find(viewer, query, out var error)
            .Where(p => !_presence.IsHidden(p) || _presence.CanSeeHidden(viewer, p))
            .ToList();

        if (found.Count == 0)
        {
            _messenger.Reply(issuer, error == TargetError.Ambiguous ? "command.multiple_targets" : "command.player_not_found", query);
            return;
        }

        if (found.Count > 1)
        {
            _messenger.Reply(issuer, "command.multiple_targets", query);
            return;
        }

        // Built as if picked from the list, so going back walks the usual way up.
        var players = PlayersPage(viewer);
        players.Parent = CreateAdminMenu(viewer);

        var sheet = PlayerPage(viewer, PlayerRef.Of(found[0]), players);
        sheet.Parent = players;
        sheet.Open(viewer);
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e) => _viewers.Remove(e.PlayerId);

    [EventListener<EventDelegates.OnMapUnload>]
    public void OnMapUnload(IOnMapUnloadEvent e) => _core.Menu.CloseAll();

    #region IAdminMenuAPI

    public void RegisterSubmenu(string id, string[] permissions, Func<IPlayer, string> getTitle, Func<IPlayer, IMenu> createMenu)
    {
        lock (_registryLock)
        {
            _registrations.RemoveAll(r => r.Id == id);
            _registrations.Add(new Registration(id, permissions ?? [], getTitle, createMenu));
        }
    }

    public void UnregisterSubmenu(string id)
    {
        lock (_registryLock)
        {
            _registrations.RemoveAll(r => r.Id == id);
        }
    }

    public void OpenAdminMenu(IPlayer player) => CreateAdminMenu(player).Open(player);

    public IMenu CreateAdminMenu(IPlayer viewer)
    {
        var (builder, page) = NewPage(AdminMenuIds.Root, T(viewer, "menu.title"));

        // Fixed labels with ids: a menu skin's sidebar keys its entries on them.
        builder.Add(Cached(T(viewer, "menu.root.players"), () => PlayersPage(viewer), AdminMenuIds.Players));
        if (_database.Enabled && BanListAvailable(viewer)) builder.Add(Cached(T(viewer, "menu.root.bans"), () => BansPage(viewer), AdminMenuIds.Bans));
        if (_database.Enabled && CommsListAvailable(viewer)) builder.Add(Cached(T(viewer, "menu.root.comms"), () => CommsPage(viewer), AdminMenuIds.Comms));
        if (ToolsAvailable(viewer)) builder.Add(Cached(T(viewer, "menu.root.tools"), () => ToolsPage(viewer), AdminMenuIds.Tools));
        if (ServerAvailable(viewer)) builder.Add(Cached(T(viewer, "menu.root.server"), () => ServerPage(viewer), AdminMenuIds.Server));

        List<Registration> registrations;
        lock (_registryLock)
        {
            registrations = [.. _registrations];
        }

        foreach (var registration in registrations)
        {
            if (registration.Permissions.Length > 0 && !registration.Permissions.Any(p => Has(viewer, p))) continue;

            string title;
            try
            {
                title = registration.GetTitle(viewer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin menu entry {Id} failed to provide a title.", registration.Id);
                continue;
            }

            builder.Add(new Link(title, _ => CreateRegistered(registration, viewer), registration.Id));
        }

        return Build(builder, page);
    }

    public IReadOnlyList<IPlayer> GetListedPlayers(IPlayer viewer) => [.. Candidates(viewer, AnyPlayer)];

    public AdminPlayerState GetPlayerState(IPlayer player) => new(
        Hidden: _presence.IsHidden(player),
        Frozen: _control.IsFrozen(player),
        Noclip: _control.IsNoclip(player),
        God: _control.IsGod(player),
        Beacon: _control.HasBeacon(player),
        Drugged: _control.IsDrugged(player),
        Esp: _esp.IsEnabled(player),
        HasDeathPosition: _control.HasDeathPosition(player));

    public IReadOnlyList<AdminRecentRow> GetRecentPlayers() =>
        [.. _recent.List().Select(entry => new AdminRecentRow(entry.SteamId, entry.Name, new DateTimeOffset(entry.LeftAt, TimeSpan.Zero)))];

    public bool IsMapChangePending() => _server.MapChangePending;

    private IMenu CreateRegistered(Registration registration, IPlayer viewer)
    {
        try
        {
            return registration.CreateMenu(viewer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin menu entry {Id} failed to build its menu.", registration.Id);

            var (builder, page) = NewPage(AdminMenuIds.Notice, T(viewer, "menu.title"));
            builder.AddHeader(Line(_ => T(viewer, "menu.entry_failed")));
            return Build(builder, page);
        }
    }

    #endregion

    public void Dispose() => _refresh.Cancel();
}
