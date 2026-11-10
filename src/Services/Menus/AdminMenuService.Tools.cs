using Admins.Contract;
using Admins.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

public sealed partial class AdminMenuService
{
    private static readonly float[] RestartDelays = [1f, 5f, 10f];

    private bool ToolsAvailable(IPlayer viewer) =>
        new[] { "admins.commands.esp", "admins.commands.hide", "admins.commands.noclip", "admins.commands.god", "admins.commands.respawn" }.Any(p => Has(viewer, p));

    private bool ServerAvailable(IPlayer viewer) =>
        new[] { "admins.commands.restartround", "admins.commands.clean", "admins.commands.map" }.Any(p => Has(viewer, p));

    /// <summary>What an admin turns on for themselves.</summary>
    private IMenu ToolsPage(IPlayer viewer)
    {
        var (builder, page) = NewPage(AdminMenuIds.Tools, T(viewer, "menu.tools.title"));
        var issuer = Issuer.From(viewer);

        if (Has(viewer, "admins.commands.esp"))
        {
            var esp = Toggle(viewer, "menu.tools.esp", () => _esp.IsEnabled(viewer), () => _esp.Toggle(issuer));
            esp.Tag = new AdminActionRow(AdminAction.Esp);
            builder.Add(esp);
        }

        if (Has(viewer, "admins.commands.hide"))
        {
            var hide = Toggle(viewer, "menu.tools.hide", () => _presence.IsHidden(viewer), () =>
            {
                if (_presence.IsHidden(viewer)) _presence.Unhide(issuer);
                else _presence.Hide(issuer);
            });
            hide.Tag = new AdminActionRow(AdminAction.Hide);
            builder.Add(hide);
        }

        if (Has(viewer, "admins.commands.noclip"))
        {
            var noclip = Toggle(viewer, "menu.tools.noclip", () => _control.IsNoclip(viewer), () => _control.SetNoclip(issuer, viewer, !_control.IsNoclip(viewer)));
            noclip.VisibleWhen = _ => viewer.IsAlive;
            noclip.Tag = new AdminActionRow(AdminAction.Noclip);
            builder.Add(noclip);
        }

        if (Has(viewer, "admins.commands.god"))
        {
            var god = Toggle(viewer, "menu.tools.god", () => _control.IsGod(viewer), () => _control.SetGod(issuer, viewer, !_control.IsGod(viewer)));
            god.VisibleWhen = _ => viewer.IsAlive;
            god.Tag = new AdminActionRow(AdminAction.God);
            builder.Add(god);
        }

        if (Has(viewer, "admins.commands.respawn"))
        {
            builder.Add(new Row(T(viewer, "menu.tools.respawn"), session =>
            {
                _control.Respawn(issuer, viewer);
                session.Invalidate();
            })
            {
                VisibleWhen = _ => !viewer.IsAlive && OnTeam(viewer),
                Tag = new AdminActionRow(AdminAction.Respawn),
            });
        }

        return Build(builder, page);
    }

    private IMenu ServerPage(IPlayer viewer)
    {
        var (builder, page) = NewPage(AdminMenuIds.Server, T(viewer, "menu.server.title"));
        var issuer = Issuer.From(viewer);

        if (Has(viewer, "admins.commands.restartround"))
        {
            var restart = Cached(T(viewer, "menu.server.restart"), () => RestartPage(viewer));
            restart.Tag = new AdminActionRow(AdminAction.RestartRound);
            builder.Add(restart);
        }

        if (Has(viewer, "admins.commands.clean"))
        {
            builder.Add(new Row(T(viewer, "menu.server.clean"), _ => _server.CleanWeapons(issuer))
            {
                Validating = page.Guard,
                Tag = new AdminActionRow(AdminAction.CleanWeapons),
            });
        }

        if (Has(viewer, "admins.commands.map"))
        {
            var map = Cached(T(viewer, "menu.server.map"), () => MapPage(viewer));
            map.Tag = new AdminActionRow(AdminAction.ChangeMap);
            builder.Add(map);
        }

        return Build(builder, page);
    }

    private IMenu RestartPage(IPlayer viewer)
    {
        var (builder, page) = NewPage(AdminMenuIds.ServerRestart, T(viewer, "menu.server.restart_title"));

        foreach (var delay in RestartDelays)
        {
            builder.Add(new Row(T(viewer, "menu.server.restart_in", delay), session =>
            {
                _server.RestartRound(Issuer.From(viewer), delay);
                session.Close();
            })
            {
                Validating = page.Guard,
                Tag = new AdminRestartRow(delay),
            });
        }

        return Build(builder, page);
    }

    private IMenu MapPage(IPlayer viewer)
    {
        var (builder, page) = NewPage(AdminMenuIds.ServerMap, T(viewer, "menu.server.map_title"));
        builder.AddHeader(Line(_ => T(viewer, "menu.server.pending"), _ => _server.MapChangePending, WarnStyle));

        foreach (var map in _config.CurrentValue.Server.Maps.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            builder.Add(new Link(map, _ => MapConfirm(viewer, map)) { Tag = new AdminMapRow(map) });
        }

        builder.Add(new Row(T(viewer, "menu.server.other_map"), session => PrintUsage(session, ["!map <map_name|ws:workshop_id>"]))
        {
            Tag = new AdminActionRow(AdminAction.OtherMap),
        });
        return Build(builder, page);
    }

    private IMenu MapConfirm(IPlayer viewer, string map)
    {
        var (builder, page) = NewPage(AdminMenuIds.ServerMapConfirm, T(viewer, "menu.server.map_confirm_title"));
        builder.AddHeader(Line(_ => T(viewer, "menu.server.pending"), _ => _server.MapChangePending, WarnStyle));

        builder.Add(new Row(T(viewer, "menu.server.map_confirm", map), session =>
        {
            if (_server.ChangeMap(Issuer.From(viewer), map)) CloseMenu(session);
        })
        {
            Style = DangerStyle,
            Validating = page.Guard,
            Tag = new AdminActionRow(AdminAction.ConfirmMap),
        });

        var menu = Build(builder, page);
        menu.Tag = new AdminMapRow(map);
        return menu;
    }
}
