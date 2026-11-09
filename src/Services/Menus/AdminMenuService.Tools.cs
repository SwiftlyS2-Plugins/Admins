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
        var (builder, page) = NewPage("admins.tools", T(viewer, "menu.tools.title"));
        var issuer = Issuer.From(viewer);

        if (Has(viewer, "admins.commands.esp"))
        {
            builder.Add(Toggle(viewer, "menu.tools.esp", () => _esp.IsEnabled(viewer), () => _esp.Toggle(issuer)));
        }

        if (Has(viewer, "admins.commands.hide"))
        {
            builder.Add(Toggle(viewer, "menu.tools.hide", () => _presence.IsHidden(viewer), () =>
            {
                if (_presence.IsHidden(viewer)) _presence.Unhide(issuer);
                else _presence.Hide(issuer);
            }));
        }

        if (Has(viewer, "admins.commands.noclip"))
        {
            var noclip = Toggle(viewer, "menu.tools.noclip", () => _control.IsNoclip(viewer), () => _control.SetNoclip(issuer, viewer, !_control.IsNoclip(viewer)));
            noclip.VisibleWhen = _ => viewer.IsAlive;
            builder.Add(noclip);
        }

        if (Has(viewer, "admins.commands.god"))
        {
            var god = Toggle(viewer, "menu.tools.god", () => _control.IsGod(viewer), () => _control.SetGod(issuer, viewer, !_control.IsGod(viewer)));
            god.VisibleWhen = _ => viewer.IsAlive;
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
            });
        }

        return Build(builder, page);
    }

    private IMenu ServerPage(IPlayer viewer)
    {
        var (builder, page) = NewPage("admins.server", T(viewer, "menu.server.title"));
        var issuer = Issuer.From(viewer);

        if (Has(viewer, "admins.commands.restartround")) builder.Add(Cached(T(viewer, "menu.server.restart"), () => RestartPage(viewer)));

        if (Has(viewer, "admins.commands.clean"))
        {
            builder.Add(new Row(T(viewer, "menu.server.clean"), _ => _server.CleanWeapons(issuer)) { Validating = page.Guard });
        }

        if (Has(viewer, "admins.commands.map")) builder.Add(Cached(T(viewer, "menu.server.map"), () => MapPage(viewer)));

        return Build(builder, page);
    }

    private IMenu RestartPage(IPlayer viewer)
    {
        var (builder, page) = NewPage("admins.server.restart", T(viewer, "menu.server.restart_title"));

        foreach (var delay in RestartDelays)
        {
            builder.Add(new Row(T(viewer, "menu.server.restart_in", delay), session =>
            {
                _server.RestartRound(Issuer.From(viewer), delay);
                session.Close();
            })
            {
                Validating = page.Guard,
            });
        }

        return Build(builder, page);
    }

    private IMenu MapPage(IPlayer viewer)
    {
        var (builder, page) = NewPage("admins.server.map", T(viewer, "menu.server.map_title"));
        builder.AddHeader(Line(_ => T(viewer, "menu.server.pending"), _ => _server.MapChangePending, WarnStyle));

        foreach (var map in _config.CurrentValue.Server.Maps.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            builder.Add(new Link(map, _ => MapConfirm(viewer, map)));
        }

        builder.Add(new Row(T(viewer, "menu.server.other_map"), session => PrintUsage(session, ["!map <map_name|ws:workshop_id>"])));
        return Build(builder, page);
    }

    private IMenu MapConfirm(IPlayer viewer, string map)
    {
        var (builder, page) = NewPage("admins.server.map_confirm", T(viewer, "menu.server.map_confirm_title"));
        builder.AddHeader(Line(_ => T(viewer, "menu.server.pending"), _ => _server.MapChangePending, WarnStyle));

        builder.Add(new Row(T(viewer, "menu.server.map_confirm", map), session =>
        {
            if (_server.ChangeMap(Issuer.From(viewer), map)) CloseMenu(session);
        })
        {
            Style = DangerStyle,
            Validating = page.Guard,
        });

        return Build(builder, page);
    }
}
