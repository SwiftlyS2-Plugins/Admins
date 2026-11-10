using Admins.Contract;
using Admins.Database.Models;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;
using Punishment = Admins.Contract.AdminPunishment;

namespace Admins.Services.Menus;

public sealed partial class AdminMenuService
{
    private sealed record PickRules(bool HumansOnly, bool NeedSteamId, bool BlockImmune);

    /// <param name="Run">
    /// Gets the player, or null when only their SteamID is left to act on, and returns the page to open, or null to stay.
    /// </param>
    private sealed record Act(
        AdminPlayerAction Action,
        Func<IPlayer?, string> Label,
        Func<IPlayer?, bool> Visible,
        Func<IPlayer?, IMenuSession, IMenu?> Run,
        bool Risky = false);

    private sealed class HistoryLoad
    {
        public int Version { get; set; }

        public bool Failed { get; set; }

        /// <summary>Newest first; null until loaded.</summary>
        public IReadOnlyList<object>? Records { get; set; }
    }

    private static readonly PickRules AnyPlayer = new(HumansOnly: false, NeedSteamId: false, BlockImmune: false);

    // Null is everyone; spectators include players still picking a team.
    private static readonly Team?[] TeamFilters = [null, Team.CT, Team.T, Team.Spectator];

    private sealed class PlayerListView : IAdminPlayerList
    {
        public int Filter { get; set; }

        public Team? Team => TeamFilters[Filter];
    }

    private static AdminPlayerRow RowOf(PlayerRef target) => new(target.Slot, target.SessionId, target.SteamId, target.Name);

    #region Player lists

    /// <summary>Everyone on the server. Players above the viewer's immunity are listed, but locked.</summary>
    private IMenu PlayersPage(IPlayer viewer)
    {
        var (builder, page) = NewPage(AdminMenuIds.Players, T(viewer, "menu.players.title"));
        var rules = AnyPlayer with { BlockImmune = true };
        var view = new PlayerListView();
        IMenu? self = null;

        builder.AddHeader(Line(_ => T(viewer, "menu.players.empty"), _ => page.Rows.Count == 0));

        bool Shown(IPlayer player) => view.Team switch
        {
            null => true,
            Team.Spectator => TeamOf(player) is Team.Spectator or Team.None,
            var team => TeamOf(player) == team,
        };

        List<IMenuComponent> Rows() => PlayerRows(viewer, rules, target => PlayerPage(viewer, target, self), Shown);

        builder.Add(new Row(string.Empty, session =>
        {
            view.Filter = (view.Filter + 1) % TeamFilters.Length;
            ReplaceRows(self!, page, Rows());
            session.Invalidate();
        })
        {
            TextProvider = _ => T(viewer, $"menu.players.show_{FilterKey(view.Team)}", Candidates(viewer, AnyPlayer).Count(Shown)),
            Tag = new AdminActionRow(AdminAction.TeamFilter),
        });
        page.RowsAt = 1;

        self = Build(builder, page, Rows);
        self.Tag = view;
        return self;
    }

    private static string FilterKey(Team? team) => team switch
    {
        Team.CT => "ct",
        Team.T => "t",
        Team.Spectator => "spec",
        _ => "all",
    };

    /// <param name="open">Opens the page for a picked player; the player may have left since the list was built.</param>
    private List<IMenuComponent> PlayerRows(IPlayer viewer, PickRules rules, Func<PlayerRef, IMenu?> open, Func<IPlayer, bool>? where = null)
    {
        var players = Candidates(viewer, rules)
            .Where(p => where is null || where(p))
            .Select(p => (Player: p, Name: Names.Of(p)))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (players.Count == 0) return [];

        var duplicates = players.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<IMenuComponent>(players.Count);

        foreach (var (player, name) in players)
        {
            var target = PlayerRef.Of(player);
            var label = PlayerLabel(viewer, player, name, duplicates.Contains(name));

            rows.Add(new Link(label, _ => open(target))
            {
                TextProvider = _ => Live(target) is null ? T(viewer, "menu.players.tag_left", label) : label,
                Enabled = !(rules.BlockImmune && Immune(viewer, player)),
                Tag = RowOf(target),
            });
        }

        return rows;
    }

    private List<IMenuComponent> RecentRows(IPlayer viewer, Func<PlayerRef, IMenu?> open)
    {
        var entries = _recent.List();
        var rows = new List<IMenuComponent>(entries.Count);

        foreach (var entry in entries)
        {
            var target = new PlayerRef(-1, 0, entry.SteamId, entry.Name, entry.Ip);

            rows.Add(new Link(string.Empty, _ => open(target))
            {
                TextProvider = _ => T(viewer, "menu.recent.row", entry.Name, Ago(entry.LeftAt)),
                Enabled = _access.CanTarget(viewer, entry.SteamId),
                Tag = new AdminRecentRow(entry.SteamId, entry.Name, new DateTimeOffset(entry.LeftAt, TimeSpan.Zero)),
            });
        }

        return rows;
    }

    private IEnumerable<IPlayer> Candidates(IPlayer viewer, PickRules rules)
    {
        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (player.Controller is { IsValid: true, IsHLTV: true }) continue;

            var self = IsSelf(viewer, player);
            if (rules.HumansOnly && player.IsFakeClient) continue;
            if (rules.NeedSteamId && !player.IsFakeClient && player.SteamID == 0) continue;
            if (!self && _presence.IsHidden(player) && !_presence.CanSeeHidden(viewer, player)) continue;

            yield return player;
        }
    }

    private string PlayerLabel(IPlayer viewer, IPlayer player, string name, bool duplicateName)
    {
        var text = duplicateName ? $"{name} #{player.UserID}" : name;
        string? tag = IsSelf(viewer, player) ? "you"
            : player.IsFakeClient ? "bot"
            : _presence.IsHidden(player) ? "hidden"
            : Joining(player) ? "joining"
            : Immune(viewer, player) ? "immune"
            : null;

        return tag is null ? text : T(viewer, $"menu.players.tag_{tag}", text);
    }

    #endregion

    #region Player state

    private IPlayer? Live(PlayerRef target) => target.SessionId == 0 ? null : target.Resolve(_core);

    private static bool IsSelf(IPlayer viewer, IPlayer player) => player.SessionId == viewer.SessionId;

    private static Team TeamOf(IPlayer player) => player.Controller is { IsValid: true } controller ? (Team)controller.TeamNum : Team.None;

    private static bool OnTeam(IPlayer player) => TeamOf(player) is Team.T or Team.CT;

    private bool Immune(IPlayer viewer, IPlayer player) => !IsSelf(viewer, player) && !_access.CanTarget(viewer, player.SteamID);

    private bool Joining(IPlayer player) => !player.IsFakeClient && (player.SteamID == 0 || (TeamOf(player) == Team.None && !_presence.IsHidden(player)));

    private IPlayer? OnlineBySteamId(ulong steamId) =>
        steamId == 0 ? null : _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => !p.IsFakeClient && p.SteamID == steamId);

    private bool ImmuneTarget(IPlayer viewer, PlayerRef target) =>
        Live(target) is { } player ? Immune(viewer, player) : target.SteamId != 0 && !_access.CanTarget(viewer, target.SteamId);

    // Notices only: team, SteamID, ping and the like stay out of regular menus, for the CustomHUD ones.
    private void AddPlayerNotices(IMenuBuilder builder, IPlayer viewer, PlayerRef target)
    {
        builder.AddHeader(Line(_ => T(viewer, "menu.hub.left"), _ => Live(target) is null, WarnStyle));
        builder.AddHeader(Line(_ => T(viewer, "menu.hub.immune"), _ => ImmuneTarget(viewer, target), WarnStyle));
    }

    #endregion

    #region Player page

    /// <summary>
    /// One player, with what can be done to them in groups and their record, each a page of its own. Still useful once
    /// they leave, for the punishments that can be issued by SteamID.
    /// </summary>
    /// <param name="afterRemoval">Where a kick or ban lands, since this page has nobody left to show.</param>
    private IMenu PlayerPage(IPlayer viewer, PlayerRef target, IMenu? afterRemoval)
    {
        var (builder, page) = NewPage(AdminMenuIds.Player, target.Name);
        AddPlayerNotices(builder, viewer, target);

        var groups = new (AdminPlayerGroup Group, string Id, string Label, string Title, Act[] Acts)[]
        {
            (AdminPlayerGroup.Punish, AdminMenuIds.PlayerPunish, "menu.player.punish", "menu.player.punish_title", PunishActs(viewer, target, afterRemoval)),
            (AdminPlayerGroup.Control, AdminMenuIds.PlayerControl, "menu.player.control", "menu.player.control_title", ControlActs(viewer, target)),
        };

        var withHistory = target.SteamId != 0 && _database.Enabled && (BanListAvailable(viewer) || CommsListAvailable(viewer));
        var withSessions = SessionsAvailable(viewer, target);

        bool AnyShown(Act[] acts) => acts.Any(a => a.Visible(Live(target)));

        builder.AddHeader(Line(_ => T(viewer, "menu.hub.nothing"), _ => !withHistory && !withSessions && !groups.Any(g => AnyShown(g.Acts))));

        foreach (var (group, id, label, title, acts) in groups)
        {
            if (acts.Length == 0) continue;

            var link = Cached(T(viewer, label), () => GroupPage(viewer, target, id, T(viewer, title, target.Name), acts));
            link.VisibleWhen = _ => AnyShown(acts);
            link.Tag = new AdminPlayerGroupRow(group);
            builder.Add(link);
        }

        var history = new HistoryLoad();

        if (withHistory)
        {
            var link = Cached(string.Empty, () => HistoryPage(viewer, target, history));
            link.TextProvider = _ => history.Records is { } records ? T(viewer, "menu.player.history", records.Count) : T(viewer, "menu.player.history_plain");
            link.Tag = new AdminPlayerGroupRow(AdminPlayerGroup.History);
            builder.Add(link);
        }

        if (withSessions)
        {
            var link = Cached(T(viewer, "menu.player.sessions"), () => SessionsPage(viewer, target));
            link.Tag = new AdminPlayerGroupRow(AdminPlayerGroup.Sessions);
            builder.Add(link);
        }

        var self = Build(builder, page);
        self.Tag = RowOf(target);
        if (withHistory) self.Opened += _ => LoadHistory(viewer, target, history, done: null);
        return self;
    }

    private IMenu GroupPage(IPlayer viewer, PlayerRef target, string id, string title, Act[] acts)
    {
        var (builder, page) = NewPage(id, title);
        AddPlayerNotices(builder, viewer, target);
        builder.AddHeader(Line(_ => T(viewer, "menu.hub.nothing"), _ => !acts.Any(a => a.Visible(Live(target)))));

        foreach (var act in acts) builder.Add(ActRow(viewer, target, act, page));

        var menu = Build(builder, page);
        menu.Tag = RowOf(target);
        return menu;
    }

    private Link ActRow(IPlayer viewer, PlayerRef target, Act act, PageState page)
    {
        var row = new Link(string.Empty, session =>
        {
            var player = Live(target);
            if (!act.Visible(player))
            {
                if (player is null) _messenger.Chat(viewer, "menu.player_left");
                return null;
            }

            var next = act.Run(player, session);
            if (session.IsOpen) session.Invalidate();
            return next;
        })
        {
            TextProvider = _ => act.Label(Live(target)),
            VisibleWhen = _ => act.Visible(Live(target)),
            Validating = page.Guard,
            Tag = new AdminPlayerActionRow(act.Action),
        };

        if (act.Risky) row.Style = DangerStyle;
        return row;
    }

    private static Act OnServer(
        AdminPlayerAction action, Func<IPlayer, string> label, Func<IPlayer, bool> visible, Func<IPlayer, IMenuSession, IMenu?> run, bool risky = false) =>
        new(action, p => p is null ? string.Empty : label(p), p => p is not null && visible(p), (p, session) => run(p!, session), risky);

    // Kick and ban first, slay last. The by-SteamID punishments still work once the player has left.
    private Act[] PunishActs(IPlayer viewer, PlayerRef target, IMenu? afterRemoval)
    {
        var acts = new List<Act>();
        var issuer = Issuer.From(viewer);

        bool Punishable(IPlayer p) => !Immune(viewer, p);
        bool HasSteamId(IPlayer p) => !p.IsFakeClient && p.SteamID != 0;
        bool Human(IPlayer p) => Punishable(p) && HasSteamId(p);
        bool Issuable(IPlayer? p) => p is null ? target.SteamId != 0 && _database.Enabled && _access.CanTarget(viewer, target.SteamId) : Punishable(p);

        // A bot, or a player still joining, has no SteamID to hold it, so the punishment opens on its own player list.
        IMenu Issue(Punishment kind, IPlayer? p, IMenu? landOn)
        {
            if (p is null || HasSteamId(p)) return ReasonPage(viewer, NewDraft(viewer, kind, p is null ? target : PlayerRef.Of(p), landOn));

            var draft = NewDraft(viewer, kind, null, landOn);
            draft.StartedFrom = Names.Of(p);
            return TargetPage(viewer, draft);
        }

        if (CanPunishAny(viewer, Punishment.Kick))
        {
            acts.Add(OnServer(
                AdminPlayerAction.Kick,
                _ => T(viewer, "menu.punish.kick"),
                Punishable,
                (p, _) => ReasonPage(viewer, NewDraft(viewer, Punishment.Kick, PlayerRef.Of(p), afterRemoval, global: false)),
                risky: true));
        }

        if (CanPunishAny(viewer, Punishment.Ban))
        {
            acts.Add(new Act(AdminPlayerAction.Ban, _ => T(viewer, "menu.punish.ban"), Issuable, (p, _) => Issue(Punishment.Ban, p, afterRemoval), Risky: true));
        }

        foreach (var kind in new[] { Punishment.Mute, Punishment.Gag })
        {
            var sanctionKind = kind == Punishment.Gag ? SanctionKind.Gag : SanctionKind.Mute;
            var canIssue = CanPunishAny(viewer, kind);
            var liftScope = LiftScopeOf(viewer, kind);
            if (!canIssue && liftScope is null) continue;

            Sanction? Active(IPlayer? p) => p is null ? null : _comms.GetActive(p, sanctionKind);

            acts.Add(new Act(
                kind == Punishment.Gag ? AdminPlayerAction.Gag : AdminPlayerAction.Mute,
                p => Active(p) is { } active ? T(viewer, $"menu.punish.un{Key(kind)}", Remaining(viewer, active.ExpiresAt)) : T(viewer, $"menu.punish.{Key(kind)}"),
                p => Active(p) is { } active ? CanLift(viewer, kind, active.GlobalSanction) && Human(p!) : canIssue && Issuable(p),
                (p, session) =>
                {
                    if (Active(p) is not { } active) return Issue(kind, p, session.Menu);

                    // Shown before they were gagged or muted by someone else, the row can be picked by a viewer who cannot lift it.
                    if (liftScope is not { } scope || !CanLift(viewer, kind, active.GlobalSanction)) return null;

                    _comms.Lift(issuer, [(p!.SteamID, Net.NormalizeIp(p.IPAddress))], ActionOf(kind), Names.Of(p), scope);
                    return null;
                }));
        }

        if (CanPunishAny(viewer, Punishment.Silence))
        {
            acts.Add(new Act(
                AdminPlayerAction.Silence,
                _ => T(viewer, "menu.punish.silence"),
                p => Issuable(p) && (p is null || (_comms.GetActive(p, SanctionKind.Gag) is null && _comms.GetActive(p, SanctionKind.Mute) is null)),
                (p, session) => Issue(Punishment.Silence, p, session.Menu)));
        }

        if (Has(viewer, "admins.commands.slay"))
        {
            acts.Add(OnServer(AdminPlayerAction.Slay, _ => T(viewer, "menu.fun.slay"), p => !Immune(viewer, p) && p.IsAlive, (p, _) => Do(() => _control.Slay(issuer, p)), risky: true));
        }

        return [.. acts];
    }

    private Act[] ControlActs(IPlayer viewer, PlayerRef target)
    {
        var acts = new List<Act>();
        var issuer = Issuer.From(viewer);

        bool Movable(IPlayer p) => !Immune(viewer, p);
        bool Alive(IPlayer p) => Movable(p) && p.IsAlive;
        bool Dead(IPlayer p) => Movable(p) && !p.IsAlive && OnTeam(p);

        var canFreeze = Has(viewer, "admins.commands.freeze");
        var canUnfreeze = Has(viewer, "admins.commands.unfreeze");
        if (canFreeze || canUnfreeze)
        {
            acts.Add(OnServer(
                AdminPlayerAction.Freeze,
                p => OnOff(viewer, "menu.fun.freeze", _control.IsFrozen(p)),
                p => Alive(p) && (_control.IsFrozen(p) ? canUnfreeze : canFreeze),
                (p, _) => Do(() => _control.SetFrozen(issuer, p, !_control.IsFrozen(p)))));
        }

        if (Has(viewer, "admins.commands.slap")) acts.Add(OnServer(AdminPlayerAction.Slap, _ => T(viewer, "menu.fun.slap"), Alive, (p, _) => Do(() => _control.Slap(issuer, p, 0))));
        if (Has(viewer, "admins.commands.respawn")) acts.Add(OnServer(AdminPlayerAction.Respawn, _ => T(viewer, "menu.tp.respawn"), Dead, (p, _) => Do(() => _control.Respawn(issuer, p))));

        if (Has(viewer, "admins.commands.hrespawn"))
        {
            acts.Add(OnServer(
                AdminPlayerAction.RespawnWhereDied,
                _ => T(viewer, "menu.tp.hrespawn"),
                p => Dead(p) && _control.HasDeathPosition(p),
                (p, _) => Do(() => _control.RespawnAtDeath(issuer, p))));
        }

        if (Has(viewer, "admins.commands.team"))
        {
            acts.Add(OnServer(AdminPlayerAction.Team, _ => T(viewer, "menu.tp.team"), Movable, (_, _) => TeamPage(viewer, target)));
        }
        else if (Has(viewer, "admins.commands.swap"))
        {
            acts.Add(OnServer(AdminPlayerAction.Swap, _ => T(viewer, "menu.tp.swap"), p => Movable(p) && OnTeam(p), (p, _) => Do(() => _control.Swap(issuer, p))));
        }

        if (Has(viewer, "admins.commands.beacon"))
        {
            acts.Add(OnServer(
                AdminPlayerAction.Beacon,
                p => OnOff(viewer, "menu.fun.beacon", _control.HasBeacon(p)),
                Alive,
                (p, _) => Do(() => _control.SetBeacon(issuer, p, !_control.HasBeacon(p)))));
        }

        if (Has(viewer, "admins.commands.drug"))
        {
            acts.Add(OnServer(
                AdminPlayerAction.Drug,
                p => OnOff(viewer, "menu.fun.drug", _control.IsDrugged(p)),
                Alive,
                (p, _) => Do(() => _control.SetDrug(issuer, p, !_control.IsDrugged(p)))));
        }

        if (Has(viewer, "admins.commands.god"))
        {
            acts.Add(OnServer(
                AdminPlayerAction.God,
                p => OnOff(viewer, "menu.powers.god", _control.IsGod(p)),
                Alive,
                (p, _) => Do(() => _control.SetGod(issuer, p, !_control.IsGod(p)))));
        }

        if (Has(viewer, "admins.commands.noclip"))
        {
            acts.Add(OnServer(
                AdminPlayerAction.Noclip,
                p => OnOff(viewer, "menu.powers.noclip", _control.IsNoclip(p)),
                Alive,
                (p, _) => Do(() => _control.SetNoclip(issuer, p, !_control.IsNoclip(p)))));
        }

        if (Has(viewer, "admins.commands.goto"))
        {
            acts.Add(OnServer(
                AdminPlayerAction.Goto,
                _ => T(viewer, "menu.tp.goto"),
                p => !IsSelf(viewer, p) && p.IsAlive && viewer.IsAlive,
                (p, _) => Do(() => _control.Goto(issuer, p))));
        }

        if (Has(viewer, "admins.commands.bring"))
        {
            acts.Add(OnServer(
                AdminPlayerAction.Bring,
                _ => T(viewer, "menu.tp.bring"),
                p => !IsSelf(viewer, p) && !Immune(viewer, p) && p.IsAlive && viewer.IsAlive,
                (p, _) => Do(() => _control.Bring(issuer, p))));
        }

        if (Has(viewer, "admins.commands.rename"))
        {
            acts.Add(OnServer(AdminPlayerAction.Rename, _ => T(viewer, "menu.move.rename"), p => !Immune(viewer, p), (p, session) =>
            {
                PrintUsage(session, [$"!rename #{p.UserID} <new_name>"]);
                return null;
            }));
        }

        if (Has(viewer, "admins.commands.hp"))
        {
            acts.Add(OnServer(AdminPlayerAction.Health, _ => T(viewer, "menu.move.hp"), p => !Immune(viewer, p) && p.IsAlive, (_, _) => HealthPage(viewer, target)));
        }

        if (Has(viewer, "admins.commands.givemoney") || Has(viewer, "admins.commands.setmoney"))
        {
            acts.Add(OnServer(AdminPlayerAction.Money, _ => T(viewer, "menu.move.money"), p => !Immune(viewer, p) && OnTeam(p), (_, _) => MoneyPage(viewer, target)));
        }

        return [.. acts];
    }

    private static IMenu? Do(Func<bool> action)
    {
        action();
        return null;
    }

    private string OnOff(IPlayer viewer, string key, bool on) => T(viewer, key, T(viewer, on ? "menu.on" : "menu.off"));

    private IMenu TeamPage(IPlayer viewer, PlayerRef target)
    {
        var (builder, page) = NewPage(AdminMenuIds.PlayerTeam, T(viewer, "menu.team.title", target.Name));

        foreach (var team in new[] { Team.T, Team.CT, Team.Spectator })
        {
            var name = T(viewer, $"team.{team.ToString().ToLowerInvariant()}");

            builder.Add(new Row(string.Empty, session =>
            {
                if (Live(target) is not { } player)
                {
                    _messenger.Chat(viewer, "menu.player_left");
                    return;
                }

                if (_control.MoveToTeam(Issuer.From(viewer), player, team)) session.Close();
            })
            {
                TextProvider = _ => Live(target) is { } player && TeamOf(player) == team ? T(viewer, "menu.team.current", name) : name,
                Validating = page.Guard,
                Tag = new AdminTeamRow(team),
            });
        }

        var menu = Build(builder, page);
        menu.Tag = RowOf(target);
        return menu;
    }

    // The presets set armour as well, as the command does; anything else is typed.
    private IMenu HealthPage(IPlayer viewer, PlayerRef target)
    {
        var (builder, page) = NewPage(AdminMenuIds.PlayerHealth, T(viewer, "menu.hp.title", target.Name));
        var issuer = Issuer.From(viewer);

        AddPlayerNotices(builder, viewer, target);

        void Preset(string key, int health, int armor, bool helmet) => builder.Add(new Row(T(viewer, key), session =>
        {
            if (Live(target) is not { } player)
            {
                _messenger.Chat(viewer, "menu.player_left");
                return;
            }

            _control.SetHealth(issuer, player, health, armor, helmet);
            session.Invalidate();
        })
        {
            Validating = page.Guard,
            Tag = new AdminHealthRow(health, armor, helmet),
        });

        Preset("menu.hp.full_armor", 100, 100, true);
        Preset("menu.hp.full", 100, 0, false);
        Preset("menu.hp.one", 1, 0, false);

        builder.Add(new Row(T(viewer, "menu.hp.other"), session =>
        {
            if (Live(target) is { } player) PrintUsage(session, [$"!hp #{player.UserID} <health> [armor] [helmet 0|1]"]);
            else _messenger.Chat(viewer, "menu.player_left");
        })
        {
            Tag = new AdminActionRow(AdminAction.OtherAmount),
        });

        var menu = Build(builder, page);
        menu.Tag = RowOf(target);
        return menu;
    }

    private IMenu MoneyPage(IPlayer viewer, PlayerRef target)
    {
        var (builder, page) = NewPage(AdminMenuIds.PlayerMoney, T(viewer, "menu.money.title", target.Name));
        var issuer = Issuer.From(viewer);
        var give = Has(viewer, "admins.commands.givemoney");
        var set = Has(viewer, "admins.commands.setmoney");

        AddPlayerNotices(builder, viewer, target);

        void Preset(string text, int amount, bool add) => builder.Add(new Row(text, session =>
        {
            if (Live(target) is not { } player)
            {
                _messenger.Chat(viewer, "menu.player_left");
                return;
            }

            _control.SetMoney(issuer, player, amount, add);
            session.Invalidate();
        })
        {
            Validating = page.Guard,
            Tag = new AdminMoneyRow(amount, add),
        });

        if (give)
        {
            Preset(T(viewer, "menu.money.give", 1000), 1000, add: true);
            Preset(T(viewer, "menu.money.give", 5000), 5000, add: true);
        }

        if (set)
        {
            Preset(T(viewer, "menu.money.set", 16000), 16000, add: false);
            Preset(T(viewer, "menu.money.set", 0), 0, add: false);
        }

        builder.Add(new Row(T(viewer, "menu.money.other"), session =>
        {
            if (Live(target) is not { } player)
            {
                _messenger.Chat(viewer, "menu.player_left");
                return;
            }

            var commands = new List<string>(2);
            if (give) commands.Add($"!givemoney #{player.UserID} <amount>");
            if (set) commands.Add($"!setmoney #{player.UserID} <amount>");
            PrintUsage(session, commands);
        })
        {
            Tag = new AdminActionRow(AdminAction.OtherAmount),
        });

        var menu = Build(builder, page);
        menu.Tag = RowOf(target);
        return menu;
    }

    #endregion

    #region History

    // Across every server, not only this one: an admin judging a player wants their whole record.
    private void LoadHistory(IPlayer viewer, PlayerRef target, HistoryLoad history, Action? done)
    {
        var version = ++history.Version;
        var steamId = (long)target.SteamId;
        var withBans = BanListAvailable(viewer);
        var withComms = CommsListAvailable(viewer);

        _ = Task.Run(async () =>
        {
            IReadOnlyList<IBan> bans = [];
            IReadOnlyList<ISanction> sanctions = [];
            var failed = false;

            try
            {
                if (withBans) bans = await _bans.FindBansAsync(steamId).ConfigureAwait(false);
                if (withComms) sanctions = await _comms.FindSanctionsAsync(steamId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failed = true;
                _logger.LogError(ex, "Loading the punishment history of {SteamId} failed.", steamId);
            }

            _core.Scheduler.NextTick(() =>
            {
                if (version != history.Version) return;

                history.Failed = failed;
                if (!failed)
                {
                    history.Records = bans.Select(b => (object)Ban.From(b))
                        .Concat(sanctions.Select(s => (object)Sanction.From(s)))
                        .OrderByDescending(r => ViewOf(r).CreatedAt)
                        .ToList();
                }

                done?.Invoke();
            });
        });
    }

    private IMenu HistoryPage(IPlayer viewer, PlayerRef target, HistoryLoad history)
    {
        var (builder, page) = NewPage(AdminMenuIds.PlayerHistory, T(viewer, "menu.player.history_title", target.Name));
        IMenu? self = null;

        string? Header() =>
            history.Failed ? T(viewer, "menu.player.history_failed")
            : history.Records is null ? T(viewer, "menu.player.history_loading")
            : history.Records.Count == 0 ? T(viewer, "menu.player.history_none")
            : history.Records.Count > ListLimit ? T(viewer, "menu.list.newest", ListLimit)
            : null;

        builder.AddHeader(Line(_ => Header(), _ => Header() is not null));

        List<IMenuComponent> Rows() => history.Records is { } records ? [.. records.Take(ListLimit).Select(r => HistoryRow(viewer, r, self!, Reload))] : [];

        // Only while it is on screen: the viewer may have left, and showing the page again loads it anew.
        void Fill()
        {
            if (_open.Any(s => s.IsOpen && ReferenceEquals(s.Menu, self))) ShowRows(self!, page, Rows());
        }

        void Reload() => LoadHistory(viewer, target, history, Fill);

        builder.Add(new Row(T(viewer, "menu.retry"), _ => Reload()) { VisibleWhen = _ => history.Failed, Tag = new AdminActionRow(AdminAction.Retry) });

        self = Build(builder, page, Rows);
        self.Tag = RowOf(target);
        self.Opened += _ => Reload();
        return self;
    }

    #endregion
}
