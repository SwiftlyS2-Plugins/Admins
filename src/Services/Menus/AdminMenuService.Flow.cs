using Admins.Contract;
using Admins.Database;
using Admins.Database.Models;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

public sealed partial class AdminMenuService
{
    /// <summary>
    /// One punishment being put together, a page per step: the player unless it started from one, the kind when it started
    /// from the Comms page, the reason, the length, where it applies when the admin may choose, and a last look. A kick is
    /// issued as soon as the reason is picked.
    /// </summary>
    private sealed class Draft
    {
        public required Punishment Kind { get; set; }

        /// <summary>SessionId 0 when they are not on the server.</summary>
        public PlayerRef Target { get; set; }

        public bool HasTarget { get; set; }

        public bool PicksTarget { get; init; }

        public bool PicksKind { get; init; }

        public bool HasKind { get; set; }

        /// <summary>The command it came from already said where it applies.</summary>
        public bool ScopeFixed { get; init; }

        /// <summary>The command it came from asked to match by IP.</summary>
        public bool WantsIp { get; init; }

        /// <summary>The page shown once it is issued; null closes the menu.</summary>
        public IMenu? LandOn { get; init; }

        /// <summary>The ban or sanction whose length is being changed, instead of issuing a new one.</summary>
        public object? Record { get; init; }

        public string? Reason { get; set; }

        public bool HasReason { get; set; }

        public TimeSpan Length { get; set; }

        public bool HasLength { get; set; }

        public bool Global { get; set; }

        public bool ByIp { get; set; }

        public bool IpOnly => Target.SteamId == 0;
    }

    /// <summary>The flow behind a punishment command typed without arguments.</summary>
    private void OpenShortcut(IPlayer viewer, Punishment kind, bool byIp, bool global) =>
        TargetPage(viewer, NewDraft(viewer, kind, null, null, global, byIp)).Open(viewer);

    /// <param name="global">The scope the command asked for; null lets the admin pick when they may do both.</param>
    private Draft NewDraft(IPlayer viewer, Punishment kind, PlayerRef? target, IMenu? landOn, bool? global = null, bool byIp = false, bool picksKind = false)
    {
        var draft = new Draft
        {
            Kind = kind,
            LandOn = landOn,
            PicksTarget = target is null,
            PicksKind = picksKind,
            ScopeFixed = global is not null,
            WantsIp = byIp,
            Global = global ?? false,
        };

        if (target is { } picked) SetTarget(draft, picked);
        DefaultScope(viewer, draft);
        return draft;
    }

    private static void SetTarget(Draft draft, PlayerRef target)
    {
        draft.Target = target;
        draft.HasTarget = true;
        draft.ByIp = draft.Kind != Punishment.Kick && ((draft.WantsIp && target.Ip.Length > 0) || target.SteamId == 0);
    }

    // As the commands do: this server, unless only all servers is allowed or the command asked for all servers.
    private void DefaultScope(IPlayer viewer, Draft draft) =>
        draft.Global = draft.Kind != Punishment.Kick && (draft.ScopeFixed && draft.Global ? CanPunish(viewer, draft.Kind, true) : !CanPunish(viewer, draft.Kind, false));

    private static Draft LengthDraft(Punishment kind, PlayerRef target, object record, bool global, IMenu? landOn) =>
        new() { Kind = kind, Target = target, HasTarget = true, Record = record, Global = global, LandOn = landOn };

    private bool AsksScope(IPlayer viewer, Draft draft) =>
        !draft.ScopeFixed && draft.Kind != Punishment.Kick && CanPunish(viewer, draft.Kind, false) && CanPunish(viewer, draft.Kind, true);

    // Going back to a step reopens it, so the picks after it no longer stand.
    private static void Rewind(Draft draft, string step)
    {
        if (step == "target") draft.HasTarget = false;
        if (step is "target" or "kind") draft.HasKind = false;
        if (step is "target" or "kind" or "reason") draft.HasReason = false;
        if (step is "target" or "kind" or "reason" or "length") draft.HasLength = false;
    }

    private string FlowTitle(IPlayer viewer, Draft draft) =>
        T(viewer, draft.Record is null ? $"menu.flow.title_{Key(draft.Kind)}" : "menu.flow.title_length", Name(draft));

    // The ids a menu skin keys its layouts on, one per kind of flow.
    private static string FlowId(Draft draft, string step) => draft.Kind switch
    {
        Punishment.Ban => $"admins.bans.give.{step}",
        Punishment.Kick => $"admins.kick.{step}",
        _ => $"admins.comms.give.{step}",
    };

    private static string Name(Draft draft)
    {
        var name = draft.Target.Name;
        if (!string.IsNullOrWhiteSpace(name) && name != "Unknown") return name;
        return draft.IpOnly ? draft.Target.Ip : draft.Target.SteamId.ToString();
    }

    #region Steps

    private IMenu TargetPage(IPlayer viewer, Draft draft)
    {
        var kick = draft.Kind == Punishment.Kick;
        var (builder, page) = NewPage(FlowId(draft, "target"), T(viewer, draft.PicksKind ? "menu.pick.title_comms" : $"menu.pick.title_{Key(draft.Kind)}"));
        AddWarnings(builder, viewer, draft);
        builder.AddHeader(Line(_ => T(viewer, "menu.players.empty"), _ => page.Rows.Count == 0));

        var rules = kick
            ? new PickRules(ExcludeSelf: true, HumansOnly: false, NeedSteamId: false, BlockImmune: true)
            : new PickRules(ExcludeSelf: true, HumansOnly: true, NeedSteamId: true, BlockImmune: true);
        var offline = !kick && _database.Enabled;

        IMenu? Pick(PlayerRef target)
        {
            SetTarget(draft, target);
            return draft.PicksKind ? KindPage(viewer, draft) : ReasonPage(viewer, draft);
        }

        if (offline)
        {
            builder.Add(new Link(string.Empty, _ => RecentPage(viewer, draft, Pick))
            {
                TextProvider = _ => T(viewer, "menu.recent.link", _recent.List().Count),
                VisibleWhen = _ => _recent.List().Count > 0,
            });

            builder.Add(new Row(T(viewer, "menu.target.by_id"), session => PrintUsage(session, ByIdUsage(viewer, draft), "menu.usage_time")));
        }

        var menu = Build(builder, page, () => PlayerRows(viewer, rules, Pick));
        menu.Opened += _ => Rewind(draft, "target");
        return menu;
    }

    private IMenu RecentPage(IPlayer viewer, Draft draft, Func<PlayerRef, IMenu?> pick)
    {
        var (builder, page) = NewPage(FlowId(draft, "target.recent"), T(viewer, "menu.recent.title"));
        AddWarnings(builder, viewer, draft);
        builder.AddHeader(Line(_ => T(viewer, "menu.recent.none"), _ => page.Rows.Count == 0));

        var menu = Build(builder, page, () => RecentRows(viewer, pick));
        menu.Opened += _ => Rewind(draft, "target");
        return menu;
    }

    private IMenu KindPage(IPlayer viewer, Draft draft)
    {
        var (builder, page) = NewPage(FlowId(draft, "kind"), T(viewer, "menu.flow.title_comms", Name(draft)));
        AddWarnings(builder, viewer, draft);

        foreach (var kind in CommsKinds)
        {
            if (!CanPunishAny(viewer, kind)) continue;

            builder.Add(new Link(T(viewer, $"menu.punish.{Key(kind)}"), _ =>
            {
                draft.Kind = kind;
                draft.HasKind = true;
                DefaultScope(viewer, draft);
                return ReasonPage(viewer, draft);
            })
            {
                Validating = page.Guard,
            });
        }

        var menu = Build(builder, page);
        menu.Opened += _ => Rewind(draft, "kind");
        return menu;
    }

    private IMenu ReasonPage(IPlayer viewer, Draft draft)
    {
        var kick = draft.Kind == Punishment.Kick;
        var (builder, page) = NewPage(FlowId(draft, "reason"), FlowTitle(viewer, draft));
        AddWarnings(builder, viewer, draft);

        var remembered = StateOf(viewer).LastReason.GetValueOrDefault(draft.Kind);

        TextComponent Choice(string text, string? reason)
        {
            if (kick)
            {
                return new Row(text, session =>
                {
                    draft.Reason = reason;
                    draft.HasReason = true;
                    Apply(viewer, draft, session);
                })
                {
                    Validating = page.Guard,
                };
            }

            return new Link(text, _ =>
            {
                draft.Reason = reason;
                draft.HasReason = true;
                return LengthPage(viewer, draft);
            })
            {
                Validating = page.Guard,
            };
        }

        if (kick) builder.Add(Choice(T(viewer, "menu.flow.no_reason"), null));

        foreach (var reason in Reasons(draft.Kind).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
        {
            var row = Choice(reason, reason);
            builder.Add(row);
            if (page.Start is null && string.Equals(reason, remembered, StringComparison.Ordinal)) page.Start = row;
        }

        builder.Add(new Row(T(viewer, "menu.flow.custom_reason"), session =>
            PrintUsage(session, [Usage(draft, "<time>", "<reason>")], kick ? null : "menu.usage_time")));

        var menu = Build(builder, page);
        menu.Opened += _ => Rewind(draft, "reason");
        return menu;
    }

    private IMenu LengthPage(IPlayer viewer, Draft draft)
    {
        var changing = draft.Record is not null;
        var (builder, page) = NewPage(FlowId(draft, "length"), FlowTitle(viewer, draft));

        if (changing) builder.AddHeader(Line(_ => T(viewer, "menu.flow.from_now")));
        else AddWarnings(builder, viewer, draft);

        TimeSpan? remembered = !changing && StateOf(viewer).LastLength.TryGetValue(draft.Kind, out var last) ? last : null;

        TextComponent Choice(TimeSpan length, string text)
        {
            if (changing)
            {
                return new Row(text, session =>
                {
                    draft.Length = length;
                    if (!Allowed(viewer, draft))
                    {
                        _messenger.Chat(viewer, "menu.no_permission");
                        return;
                    }

                    ChangeLength(viewer, draft);
                    Finish(viewer, draft, session);
                })
                {
                    Validating = page.Guard,
                };
            }

            return new Link(text, _ =>
            {
                draft.Length = length;
                draft.HasLength = true;
                return AsksScope(viewer, draft) ? ScopePage(viewer, draft) : ConfirmPage(viewer, draft);
            })
            {
                Validating = page.Guard,
            };
        }

        var choices = DurationChoices(draft.Kind);

        foreach (var value in choices.Where(s => s > 0).Distinct().Order())
        {
            var length = TimeSpan.FromSeconds(value);
            var row = Choice(length, Readable(viewer, length));
            builder.Add(row);
            if (remembered == length) page.Start = row;
        }

        if (choices.Contains(0))
        {
            var permanent = Choice(TimeSpan.Zero, T(viewer, "menu.flow.permanent"));
            permanent.Style = DangerStyle;
            builder.Add(permanent);
            if (remembered == TimeSpan.Zero) page.Start = permanent;
        }

        if (!changing)
        {
            builder.Add(new Row(T(viewer, "menu.flow.custom_length"), session =>
                PrintUsage(session, [Usage(draft, "<time>", draft.HasReason && draft.Reason is { } reason ? reason : "<reason>")], "menu.usage_time")));
        }

        var menu = Build(builder, page);
        if (!changing) menu.Opened += _ => Rewind(draft, "length");
        return menu;
    }

    private IMenu ScopePage(IPlayer viewer, Draft draft)
    {
        var (builder, page) = NewPage(FlowId(draft, "scope"), FlowTitle(viewer, draft));
        AddWarnings(builder, viewer, draft);

        foreach (var global in new[] { false, true })
        {
            var choice = new Link(T(viewer, global ? "menu.flow.scope_global" : "menu.flow.scope_local"), _ =>
            {
                draft.Global = global;
                return ConfirmPage(viewer, draft);
            })
            {
                Validating = page.Guard,
            };

            builder.Add(choice);
            if (draft.Global == global) page.Start = choice;
        }

        return Build(builder, page);
    }

    private IMenu ConfirmPage(IPlayer viewer, Draft draft)
    {
        var (builder, page) = NewPage(FlowId(draft, "confirm"), FlowTitle(viewer, draft));
        AddWarnings(builder, viewer, draft);

        var confirm = new Row(T(viewer, $"menu.flow.confirm_{Key(draft.Kind)}"), session => Apply(viewer, draft, session))
        {
            Style = DangerStyle,
            Validating = page.Guard,
        };

        builder.Add(confirm);
        page.Start = confirm;

        builder.Add(new Row(T(viewer, "menu.flow.summary"), _ => SummarizeDraft(viewer, draft)));

        if (draft.Target.SteamId != 0 && draft.Target.Ip.Length > 0)
        {
            builder.Add(new Row(string.Empty, session =>
            {
                draft.ByIp = !draft.ByIp;
                session.Invalidate();
            })
            {
                TextProvider = _ => T(viewer, "menu.flow.match", T(viewer, draft.ByIp ? "menu.flow.match_ip" : "menu.flow.match_steamid")),
            });
        }

        return Build(builder, page);
    }

    private void AddWarnings(IMenuBuilder builder, IPlayer viewer, Draft draft)
    {
        builder.AddHeader(Line(_ => T(viewer, "menu.flow.warn_gone"), _ => draft.Kind == Punishment.Kick && draft.HasTarget && Live(draft.Target) is null, WarnStyle));
        builder.AddHeader(Line(
            _ => T(viewer, "menu.flow.warn_left"),
            _ => draft.Kind != Punishment.Kick && draft.HasTarget && draft.Target.SessionId != 0 && Live(draft.Target) is null,
            WarnStyle));
        builder.AddHeader(Line(
            _ => T(viewer, draft.Kind == Punishment.Ban ? "menu.flow.warn_not_saved_ban" : "menu.flow.warn_not_saved_comms"),
            _ => draft.Kind != Punishment.Kick && !_database.Enabled,
            WarnStyle));
    }

    /// <summary>The chat command that issues this punishment, for a reason or length the menu does not list.</summary>
    private string Usage(Draft draft, string time, string reason)
    {
        var command = Key(draft.Kind);
        if (draft.Global) command = "global" + command;
        if (draft.ByIp) command += "ip";

        var target = Live(draft.Target) is { } player ? $"#{player.UserID}"
            : draft.ByIp ? draft.Target.Ip
            : draft.Target.SteamId.ToString();

        return draft.Kind == Punishment.Kick ? $"!{command} {target} {reason}" : $"!{command} {target} {time} {reason}";
    }

    // For someone who left before the last few minutes, or never joined: the menu only knows recent players.
    private IEnumerable<string> ByIdUsage(IPlayer viewer, Draft draft)
    {
        IEnumerable<Punishment> kinds = draft.PicksKind ? [Punishment.Gag, Punishment.Mute] : [draft.Kind];

        foreach (var kind in kinds)
        {
            if (!CanPunishAny(viewer, kind)) continue;

            var command = (CanPunish(viewer, kind, false) ? string.Empty : "global") + Key(kind);
            yield return $"!{command} <steamid64> <time> <reason>";
            if (kind == Punishment.Ban) yield return $"!{command}ip <ip> <time> <reason>";
        }
    }

    private void SummarizeDraft(IPlayer viewer, Draft draft)
    {
        var id = draft.ByIp ? draft.Target.Ip : draft.Target.SteamId.ToString();
        var name = Name(draft);
        var subject = name == id ? id : $"{name} ({id})";

        _messenger.Chat(viewer, "menu.summary.pending", KindName(viewer, draft.Kind), subject, ReasonText(viewer, draft.Reason), Readable(viewer, draft.Length), Scope(viewer, draft.Global));
        _messenger.Chat(viewer, "menu.summary.sees", Preview(viewer, draft));
    }

    // What the player is shown, in the admin's language since the admin is the one reading it.
    private string Preview(IPlayer viewer, Draft draft)
    {
        var reason = draft.Reason ?? string.Empty;
        var until = _messenger.Timestamp(viewer, Durations.ExpiresAt(draft.Length, AdminsDatabase.NowMs));

        var text = draft.Kind switch
        {
            Punishment.Ban => T(viewer, "ban.kick_message", reason, until, viewer.Name, viewer.SteamID),
            Punishment.Mute => T(viewer, "mute.message", viewer.Name, until, reason),
            _ => T(viewer, "gag.message", viewer.Name, until, reason),
        };

        return text.Replace("\n", " · ");
    }

    #endregion

    #region Issuing

    private void Apply(IPlayer viewer, Draft draft, IMenuSession session)
    {
        var kind = draft.Kind;
        var issuer = Issuer.From(viewer);
        var target = draft.Target;
        var reason = draft.Reason;

        if (!Allowed(viewer, draft))
        {
            _messenger.Chat(viewer, "menu.no_permission");
            return;
        }

        if (kind == Punishment.Kick)
        {
            if (Live(target) is { } kicked) _control.Kick(issuer, kicked, reason);
            else _messenger.Chat(viewer, "menu.player_left");

            Remember(viewer, draft);
            Finish(viewer, draft, session);
            return;
        }

        if (target.SteamId != 0 && !_access.CanTarget(viewer, target.SteamId))
        {
            _messenger.Chat(viewer, "command.target_has_immunity", Name(draft), _access.GetImmunity(target.SteamId));
            return;
        }

        var text = reason ?? string.Empty;

        void Execute()
        {
            var live = Live(target);

            if (kind == Punishment.Ban)
            {
                var type = draft.ByIp ? BanType.IP : BanType.SteamID;
                if (live is not null) _bans.BanPlayer(issuer, live, type, draft.Length, text, draft.Global);
                else _bans.BanOffline(issuer, target.SteamId, target.Ip, type, draft.Length, text, draft.Global, target.Name);
                return;
            }

            var sanctionType = draft.ByIp ? SanctionType.IP : SanctionType.SteamID;
            if (live is not null) _comms.SanctionPlayer(issuer, live, ActionOf(kind), sanctionType, draft.Length, text, draft.Global);
            else _comms.SanctionOffline(issuer, target.SteamId, target.Ip, ActionOf(kind), sanctionType, draft.Length, text, draft.Global, target.Name);
        }

        // A gag or mute already on them is replaced rather than stacked.
        if (kind != Punishment.Ban && Live(target) is { } player && Covered(kind).Any(k => _comms.GetActive(player, k) is not null))
        {
            _comms.Lift(issuer, [(player.SteamID, target.Ip)], ActionOf(kind), Name(draft), completed: Execute);
        }
        else
        {
            Execute();
        }

        Remember(viewer, draft);
        Finish(viewer, draft, session);
    }

    private bool Allowed(IPlayer viewer, Draft draft) =>
        CanPunish(viewer, draft.Kind, draft.Global) && (draft.Record is null || CanLift(viewer, draft.Kind));

    private static SanctionKind[] Covered(Punishment kind) => kind switch
    {
        Punishment.Gag => [SanctionKind.Gag],
        Punishment.Mute => [SanctionKind.Mute],
        _ => [SanctionKind.Gag, SanctionKind.Mute],
    };

    // The new length counts from now, the way a fresh punishment would.
    private void ChangeLength(IPlayer viewer, Draft draft)
    {
        var issuer = Issuer.From(viewer);
        var expiresAt = Durations.ExpiresAt(draft.Length, AdminsDatabase.NowMs);
        var label = Readable(viewer, draft.Length);
        var name = Name(draft);

        Func<Task> update;
        switch (draft.Record)
        {
            case Ban ban:
                var banRow = Ban.From(ban);
                banRow.ExpiresAt = expiresAt;
                banRow.Length = expiresAt == 0 ? 0 : expiresAt - banRow.CreatedAt;
                update = () => _bans.UpdateBanAsync(banRow);
                break;
            case Sanction sanction:
                var sanctionRow = Sanction.From(sanction);
                sanctionRow.ExpiresAt = expiresAt;
                sanctionRow.Length = expiresAt == 0 ? 0 : expiresAt - sanctionRow.CreatedAt;
                update = () => _comms.UpdateSanctionAsync(sanctionRow);
                break;
            default:
                return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await update().ConfigureAwait(false);
                _messenger.ReplyLater(issuer, "menu.length_changed", name, label);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Changing the length of a punishment on {Target} failed.", name);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    private void Remember(IPlayer viewer, Draft draft)
    {
        var state = StateOf(viewer);
        if (draft.Reason is { } reason) state.LastReason[draft.Kind] = reason;
        if (draft.Kind != Punishment.Kick) state.LastLength[draft.Kind] = draft.Length;
    }

    private void Finish(IPlayer viewer, Draft draft, IMenuSession session)
    {
        if (draft.LandOn is { } land) land.Open(viewer);
        else CloseMenu(session);
    }

    private IReadOnlyList<string> Reasons(Punishment kind) => kind switch
    {
        Punishment.Kick => _config.CurrentValue.Kick.Reasons,
        Punishment.Ban => _bans.Config.Reasons,
        _ => _comms.Config.Reasons,
    };

    private IReadOnlyList<int> DurationChoices(Punishment kind) => kind == Punishment.Ban ? _bans.Config.DurationsInSeconds : _comms.Config.DurationsInSeconds;

    #endregion
}
