using Admins.Contract;
using Admins.Services.Sessions;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

public sealed partial class AdminMenuService
{
    private sealed class SessionsLoad
    {
        public int Version { get; set; }

        public bool Failed { get; set; }

        /// <summary>Null until loaded.</summary>
        public AccountSessions? Account { get; set; }
    }

    // The page lists IP addresses, so it takes what !sessions takes.
    private bool SessionsAvailable(IPlayer viewer, PlayerRef target) => target.SteamId != 0 && _database.Enabled && Has(viewer, "admins.commands.sessions");

    /// <summary>
    /// A player's time played, the addresses they connected from and their connections, on every server sharing the
    /// database as <c>!sessions</c> shows them. Picking an address prints who else used it.
    /// </summary>
    private IMenu SessionsPage(IPlayer viewer, PlayerRef target)
    {
        var (builder, page) = NewPage(AdminMenuIds.PlayerSessions, T(viewer, "menu.player.sessions_title", target.Name));
        var load = new SessionsLoad();
        IMenu? self = null;

        string? Header() =>
            load.Failed ? T(viewer, "menu.load_failed")
            : load.Account is not { } account ? T(viewer, "menu.loading")
            : account.Sessions.Count == 0 ? T(viewer, "menu.player.sessions_none")
            : account.Sessions.Count > ListLimit ? T(viewer, "menu.list.newest", ListLimit)
            : null;

        builder.AddHeader(Line(_ => Header(), _ => Header() is not null));

        string Length(AdminSessionRow s) => s.Online ? T(viewer, "sessions.online", Durations.Format(s.Played)) : Durations.Format(s.Played);

        List<IMenuComponent> Rows()
        {
            if (load.Account is not { Sessions.Count: > 0 } account) return [];

            var issuer = Issuer.From(viewer);
            return
            [
                .. account.Playtime.Select(p => new Row(T(viewer, $"menu.sessions.{p.Period.ToString().ToLowerInvariant()}", Durations.Format(p.Played)), _ => { }) { Tag = p }),
                .. account.Ips.Take(ListLimit).Select(ip => new Row(T(viewer, "menu.sessions.ip", ip.Ip, ip.Sessions), _ => _sessions.ShowIp(issuer, ip.Ip)) { Tag = ip }),
                .. account.Sessions.Take(ListLimit).Select(s =>
                    new Row(T(viewer, "menu.sessions.row", _messenger.Timestamp(viewer, s.ConnectedAt.ToUnixTimeMilliseconds()), Length(s)), _ => { }) { Tag = s }),
            ];
        }

        // Only while it is on screen: the viewer may have left, and showing the page again loads it anew.
        void Fill()
        {
            if (_open.Any(s => s.IsOpen && ReferenceEquals(s.Menu, self))) ShowRows(self!, page, Rows());
        }

        void Reload()
        {
            var version = ++load.Version;
            var steamId = target.SteamId;

            _ = Task.Run(async () =>
            {
                AccountSessions? account = null;
                try
                {
                    account = await _sessions.LoadAccountAsync(steamId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Loading the sessions of {SteamId} failed.", steamId);
                }

                _core.Scheduler.NextTick(() =>
                {
                    if (version != load.Version) return;

                    load.Failed = account is null;
                    if (account is not null) load.Account = account;
                    Fill();
                });
            });
        }

        builder.Add(new Row(T(viewer, "menu.retry"), _ => Reload()) { VisibleWhen = _ => load.Failed, Tag = new AdminActionRow(AdminAction.Retry) });

        self = Build(builder, page, Rows);
        self.Tag = RowOf(target);
        self.Opened += _ => Reload();
        return self;
    }
}
