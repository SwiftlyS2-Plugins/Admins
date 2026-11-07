using Admins.Contract;
using Admins.Services.Access;
using Admins.Services.Bans;
using Admins.Services.Comms;
using Admins.Services.Esp;
using Admins.Services.PlayerControl;
using Admins.Services.Presence;
using Admins.Services.ServerControl;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

/// <summary>
/// The <c>!admin</c> menu. Menus are built per viewer, already localized and filtered by the viewer's permissions,
/// so every entry shown is one the viewer may use.
/// </summary>
public sealed partial class AdminMenuService : IAdminMenuAPI
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<AdminMenuService> _logger;
    private readonly Messenger _messenger;
    private readonly AccessService _access;
    private readonly BansService _bans;
    private readonly CommsService _comms;
    private readonly PlayerControlService _control;
    private readonly ServerControlService _server;
    private readonly EspService _esp;
    private readonly PresenceService _presence;
    private readonly Lock _registryLock = new();
    private readonly List<Registration> _registrations = [];

    public AdminMenuService(
        ISwiftlyCore core,
        ILogger<AdminMenuService> logger,
        Messenger messenger,
        AccessService access,
        BansService bans,
        CommsService comms,
        PlayerControlService control,
        ServerControlService server,
        EspService esp,
        PresenceService presence,
        MenuShortcuts shortcuts)
    {
        _core = core;
        _logger = logger;
        _messenger = messenger;
        _access = access;
        _bans = bans;
        _comms = comms;
        _control = control;
        _server = server;
        _esp = esp;
        _presence = presence;

        shortcuts.OpenBan = (viewer, type, global) => OpenPicker(viewer, (session, target) => BuildBanForm(viewer, target, session.Menu, type, global));
        shortcuts.OpenComms = (viewer, action, global) => OpenPicker(viewer, (session, target) => BuildCommsForm(viewer, target, session.Menu, action, global));

        core.Registrator.Register(this);
    }

    private sealed record Registration(string Id, string[] Permissions, Func<IPlayer, string> GetTitle, Func<IPlayer, IMenu> CreateMenu);

    [Command("admin", permission: "admins.commands.admin")]
    public void AdminCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is not { } player)
        {
            _messenger.Reply(Issuer.From(context), "command.player_only");
            return;
        }

        OpenAdminMenu(player);
    }

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
        var builder = _core.Menu.CreateMenu("admins.main").WithTitle(T(viewer, "menu.title"));

        builder.Add(new SubmenuComponent(T(viewer, "menu.players"), s => BuildPlayerList(viewer, s.Menu)));

        if (CanBan(viewer, global: false) || CanBan(viewer, global: true) || Has(viewer, "admins.commands.unban"))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.bans"), s => BuildBansMenu(viewer, s.Menu)));
        }

        if (Enum.GetValues<CommsAction>().Any(a => CanComms(viewer, a, global: false) || CanComms(viewer, a, global: true)) || CanLiftAnyComms(viewer))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.comms"), s => BuildCommsMenu(viewer, s.Menu)));
        }

        if (Has(viewer, "admins.commands.restartround") || Has(viewer, "admins.commands.clean") || Has(viewer, "admins.commands.map"))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.server"), s => BuildServerMenu(viewer, s.Menu)));
        }

        if (Has(viewer, "admins.commands.esp"))
        {
            AddSelfToggle(builder, viewer, "menu.esp", () => _esp.IsEnabled(viewer), issuer => _esp.Toggle(issuer));
        }

        if (Has(viewer, "admins.commands.hide"))
        {
            AddSelfToggle(builder, viewer, "menu.hide", () => _presence.IsHidden(viewer), issuer =>
            {
                if (_presence.IsHidden(viewer)) _presence.Unhide(issuer);
                else _presence.Hide(issuer);
            });
        }

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

            builder.Add(new SubmenuComponent(title, s => CreateRegistered(registration, viewer, s.Menu)));
        }

        return builder.Build();
    }

    private IMenu CreateRegistered(Registration registration, IPlayer viewer, IMenu parent)
    {
        try
        {
            var menu = registration.CreateMenu(viewer);
            menu.Parent ??= parent;
            return menu;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin menu entry {Id} failed to build its menu.", registration.Id);
            return Notice(viewer, parent, T(viewer, "menu.entry_failed"));
        }
    }

    #endregion

    #region Players

    private void OpenPicker(IPlayer viewer, Func<IMenuSession, PlayerRef, IMenu> next) =>
        BuildPicker(viewer, null, T(viewer, "menu.select_player"), excludeSelf: true, next).Open(viewer);

    private IMenu BuildPlayerList(IPlayer viewer, IMenu parent) =>
        BuildPicker(viewer, parent, T(viewer, "menu.players"), excludeSelf: false, (session, target) => BuildPlayerMenu(viewer, target, session.Menu));

    private IMenu BuildPicker(IPlayer viewer, IMenu? parent, string title, bool excludeSelf, Func<IMenuSession, PlayerRef, IMenu> next)
    {
        var list = new PlayerListComponent("admins.players")
        {
            Filter = (_, candidate) => candidate.Controller is not { IsValid: true, IsHLTV: true } && !(excludeSelf && candidate.Slot == viewer.Slot),
            Sorter = players => players.OrderBy(Names.Of, StringComparer.OrdinalIgnoreCase),
            Formatter = player => _access.CanTarget(viewer, player.SteamID) ? Names.Of(player) : T(viewer, "menu.player_immune", Names.Of(player)),
            EmptyText = T(viewer, "menu.no_players"),
            OnPlayerSelected = ctx => next(ctx.Session, PlayerRef.Of(ctx.Target)).Open(viewer),
        };

        var builder = _core.Menu.CreateMenu("admins.players").WithTitle(title).Add(list);
        if (parent is not null) builder.WithParent(parent);
        return builder.Build();
    }

    private IMenu BuildPlayerMenu(IPlayer viewer, PlayerRef target, IMenu parent)
    {
        var builder = _core.Menu.CreateMenu("admins.player").WithTitle(target.Name).WithParent(parent);

        builder.AddHeader(new TextComponent
        {
            TextProvider = _ => target.Resolve(_core) is { } player
                ? T(viewer, "menu.player.header", player.SteamID, _access.GetImmunity(player.SteamID))
                : T(viewer, "menu.player_left"),
        });

        if (CanBan(viewer, global: false) || CanBan(viewer, global: true))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.player.ban"), s => BuildBanForm(viewer, target, s.Menu, BanType.SteamID, global: false)));
        }

        if (Enum.GetValues<CommsAction>().Any(a => CanComms(viewer, a, global: false) || CanComms(viewer, a, global: true)))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.player.comms"), s => BuildCommsForm(viewer, target, s.Menu, CommsAction.Gag, global: false)));
        }

        AddLift(builder, viewer, target, SanctionKind.Gag, "admins.commands.ungag", "menu.player.ungag");
        AddLift(builder, viewer, target, SanctionKind.Mute, "admins.commands.unmute", "menu.player.unmute");

        AddAction(builder, viewer, target, "admins.commands.kick", "menu.player.kick", (issuer, player, session) =>
        {
            if (_control.Kick(issuer, player, null)) session.Close();
        });

        AddAction(builder, viewer, target, "admins.commands.slay", "menu.player.slay", (issuer, player, _) => _control.Slay(issuer, player));
        AddAction(builder, viewer, target, "admins.commands.slap", "menu.player.slap", (issuer, player, _) => _control.Slap(issuer, player, 0));

        if (Has(viewer, "admins.commands.freeze") || Has(viewer, "admins.commands.unfreeze"))
        {
            AddToggle(builder, viewer, target, "menu.player.freeze", _control.IsFrozen, (issuer, player, on) =>
                Has(viewer, on ? "admins.commands.freeze" : "admins.commands.unfreeze")
                    ? _control.SetFrozen(issuer, player, on)
                    : Denied(issuer));
        }

        if (Has(viewer, "admins.commands.god")) AddToggle(builder, viewer, target, "menu.player.god", _control.IsGod, _control.SetGod);
        if (Has(viewer, "admins.commands.noclip")) AddToggle(builder, viewer, target, "menu.player.noclip", _control.IsNoclip, _control.SetNoclip);
        if (Has(viewer, "admins.commands.beacon")) AddToggle(builder, viewer, target, "menu.player.beacon", _control.HasBeacon, _control.SetBeacon);
        if (Has(viewer, "admins.commands.drug")) AddToggle(builder, viewer, target, "menu.player.drug", _control.IsDrugged, _control.SetDrug);

        AddAction(builder, viewer, target, "admins.commands.respawn", "menu.player.respawn", (issuer, player, _) => _control.Respawn(issuer, player));

        if (Has(viewer, "admins.commands.hrespawn"))
        {
            AddAction(builder, viewer, target, "admins.commands.hrespawn", "menu.player.hrespawn", (issuer, player, _) => _control.RespawnAtDeath(issuer, player))
                .VisibleWhen = _ => target.Resolve(_core) is { } player && _control.HasDeathPosition(player);
        }

        if (Has(viewer, "admins.commands.team"))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.player.team"), s => BuildTeamMenu(viewer, target, s.Menu)));
        }

        AddAction(builder, viewer, target, "admins.commands.swap", "menu.player.swap", (issuer, player, _) => _control.Swap(issuer, player));
        AddAction(builder, viewer, target, "admins.commands.goto", "menu.player.goto", (issuer, player, _) => _control.Goto(issuer, player));
        AddAction(builder, viewer, target, "admins.commands.bring", "menu.player.bring", (issuer, player, _) => _control.Bring(issuer, player));

        return builder.Build();
    }

    private IMenu BuildTeamMenu(IPlayer viewer, PlayerRef target, IMenu parent)
    {
        var builder = _core.Menu.CreateMenu("admins.team").WithTitle(T(viewer, "menu.player.team")).WithParent(parent);

        foreach (var team in new[] { Team.T, Team.CT, Team.Spectator })
        {
            builder.Add(new ButtonComponent(T(viewer, $"team.{team.ToString().ToLowerInvariant()}"), ctx =>
            {
                if (Resolve(viewer, target, ctx.Session) is { } player && _control.MoveToTeam(Issuer.From(viewer), player, team)) ctx.Session.Close();
                return ValueTask.CompletedTask;
            }));
        }

        return builder.Build();
    }

    private ButtonComponent AddAction(IMenuBuilder builder, IPlayer viewer, PlayerRef target, string permission, string labelKey, Action<Issuer, IPlayer, IMenuSession> action)
    {
        var button = new ButtonComponent(T(viewer, labelKey), ctx =>
        {
            if (Resolve(viewer, target, ctx.Session) is { } player)
            {
                action(Issuer.From(viewer), player, ctx.Session);
                ctx.Session.Invalidate();
            }

            return ValueTask.CompletedTask;
        });

        if (Has(viewer, permission)) builder.Add(button);
        return button;
    }

    private void AddToggle(IMenuBuilder builder, IPlayer viewer, PlayerRef target, string labelKey, Func<IPlayer, bool> isOn, Func<Issuer, IPlayer, bool, bool> set)
    {
        builder.Add(new ButtonComponent(string.Empty, ctx =>
        {
            if (Resolve(viewer, target, ctx.Session) is { } player)
            {
                set(Issuer.From(viewer), player, !isOn(player));
                ctx.Session.Invalidate();
            }

            return ValueTask.CompletedTask;
        })
        {
            TextProvider = _ => T(viewer, labelKey, T(viewer, target.Resolve(_core) is { } player && isOn(player) ? "menu.on" : "menu.off")),
        });
    }

    private void AddSelfToggle(IMenuBuilder builder, IPlayer viewer, string labelKey, Func<bool> isOn, Action<Issuer> toggle)
    {
        builder.Add(new ButtonComponent(string.Empty, ctx =>
        {
            toggle(Issuer.From(viewer));
            ctx.Session.Invalidate();
            return ValueTask.CompletedTask;
        })
        {
            TextProvider = _ => T(viewer, labelKey, T(viewer, isOn() ? "menu.on" : "menu.off")),
        });
    }

    private void AddLift(IMenuBuilder builder, IPlayer viewer, PlayerRef target, SanctionKind kind, string permission, string labelKey)
    {
        if (!Has(viewer, permission)) return;

        builder.Add(new ButtonComponent(T(viewer, labelKey), ctx =>
        {
            if (Resolve(viewer, target, ctx.Session) is { } player)
            {
                _comms.Lift(Issuer.From(viewer), [(player.SteamID, Net.NormalizeIp(player.IPAddress))], kind == SanctionKind.Gag ? CommsAction.Gag : CommsAction.Mute, target.Name);
            }

            return ValueTask.CompletedTask;
        })
        {
            VisibleWhen = _ => target.Resolve(_core) is { } player && _comms.GetActive(player, kind) is not null,
        });
    }

    /// <summary>
    /// The target if still connected; otherwise tells the viewer and leaves the player's menu.
    /// </summary>
    private IPlayer? Resolve(IPlayer viewer, PlayerRef target, IMenuSession session)
    {
        if (target.Resolve(_core) is { } player) return player;

        _messenger.Chat(viewer, "menu.player_left");
        session.Close();
        return null;
    }

    #endregion

    #region Helpers

    private string T(IPlayer viewer, string key, params object[] args) => _messenger.Text(viewer, key, args);

    private bool Has(IPlayer viewer, string permission) => _core.Permission.PlayerHasPermission(viewer.SteamID, permission);

    // admins.menu.bans / admins.menu.comms predate the per-command checks; they keep granting local actions only.
    private bool CanBan(IPlayer viewer, bool global) =>
        global ? Has(viewer, "admins.commands.globalban") : Has(viewer, "admins.commands.ban") || Has(viewer, "admins.menu.bans");

    private bool CanComms(IPlayer viewer, CommsAction action, bool global)
    {
        var name = action.ToString().ToLowerInvariant();
        return global ? Has(viewer, $"admins.commands.global{name}") : Has(viewer, $"admins.commands.{name}") || Has(viewer, "admins.menu.comms");
    }

    private bool CanLiftAnyComms(IPlayer viewer) => Has(viewer, "admins.commands.ungag") || Has(viewer, "admins.commands.unmute");

    private bool Denied(Issuer issuer)
    {
        _messenger.Reply(issuer, "menu.no_permission");
        return false;
    }

    private IMenu Notice(IPlayer viewer, IMenu parent, string text) =>
        _core.Menu.CreateMenu("admins.notice").WithTitle(T(viewer, "menu.title")).WithParent(parent).Add(new TextComponent(text)).Build();

    #endregion
}
