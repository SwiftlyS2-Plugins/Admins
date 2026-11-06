using Admins.Contract;
using Admins.Database.Models;
using Admins.Services.Comms;
using Admins.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

public sealed partial class AdminMenuService
{
    private const int ListLimit = 100;

    #region Bans

    private IMenu BuildBansMenu(IPlayer viewer, IMenu parent)
    {
        var builder = _core.Menu.CreateMenu("admins.bans").WithTitle(T(viewer, "menu.bans")).WithParent(parent);

        if (CanBan(viewer, global: false) || CanBan(viewer, global: true))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.bans.offline"), s => BuildBanForm(viewer, null, s.Menu, BanType.SteamID, global: false)));
        }

        builder.Add(new SubmenuComponent(T(viewer, "menu.bans.active"), s => BuildActiveBans(viewer, s.Menu)));
        return builder.Build();
    }

    /// <param name="target">The online player to ban, or null for a form that asks for a SteamID64 or IP.</param>
    private IMenu BuildBanForm(IPlayer viewer, PlayerRef? target, IMenu parent, BanType type, bool global)
    {
        var config = _bans.Config;
        var canLocal = CanBan(viewer, global: false);
        var canGlobal = CanBan(viewer, global: true);

        var builder = _core.Menu.CreateMenu("admins.ban")
            .WithTitle(target is { } t ? T(viewer, "menu.ban.title", t.Name) : T(viewer, "menu.bans.offline"))
            .WithParent(parent);

        var typeChoice = new ChoiceComponent<BanType>(T(viewer, "menu.form.type"), [BanType.SteamID, BanType.IP], type == BanType.IP ? 1 : 0)
        {
            Formatter = value => T(viewer, value == BanType.IP ? "menu.form.type_ip" : "menu.form.type_steamid"),
        };
        builder.Add(typeChoice);

        var identity = target is null ? IdentityInput(viewer) : null;
        if (identity is not null) builder.Add(identity);

        var (reason, customReason) = ReasonInputs(viewer, config.Reasons);
        builder.Add(reason).Add(customReason);

        var (duration, customDuration) = DurationInputs(viewer, config.DurationsInSeconds);
        builder.Add(duration).Add(customDuration);

        var globalToggle = canLocal && canGlobal ? new ToggleComponent(T(viewer, "menu.form.global"), global) : null;
        if (globalToggle is not null) builder.Add(globalToggle);

        builder.Add(new ButtonComponent(T(viewer, "menu.form.confirm"), ctx =>
        {
            var session = ctx.Session;
            var issuer = Issuer.From(viewer);
            var chosenType = typeChoice.GetItem(session);
            var isGlobal = globalToggle?.GetValue(session) ?? !canLocal;

            if (!TryReadCommon(viewer, session, reason, customReason, duration, customDuration, out var reasonText, out var length)) return ValueTask.CompletedTask;

            if (target is { } online)
            {
                if (Resolve(viewer, online, session) is not { } player) return ValueTask.CompletedTask;

                if (!_access.CanTarget(viewer, player.SteamID))
                {
                    _messenger.Chat(viewer, "command.target_has_immunity", Names.Of(player), _access.GetImmunity(player.SteamID));
                    return ValueTask.CompletedTask;
                }

                _bans.BanPlayer(issuer, player, chosenType, length, reasonText, isGlobal);

                // The player is about to leave, so skip their (now empty) menu on the way back.
                session.Menu.Parent = session.Menu.Parent?.Parent;
                session.Close();
                return ValueTask.CompletedTask;
            }

            var value = identity!.GetValue(session)?.Trim() ?? string.Empty;
            if (chosenType == BanType.IP)
            {
                if (!Net.IsIp(value))
                {
                    _messenger.Chat(viewer, "menu.form.invalid_ip", value);
                    return ValueTask.CompletedTask;
                }

                _bans.BanOffline(issuer, 0, value, BanType.IP, length, reasonText, isGlobal);
            }
            else
            {
                if (!SteamIds.TryParse(value, out var steamId))
                {
                    _messenger.Chat(viewer, "command.invalid_steamid", value);
                    return ValueTask.CompletedTask;
                }

                if (!_access.CanTarget(viewer, steamId))
                {
                    _messenger.Chat(viewer, "command.target_has_immunity", steamId, _access.GetImmunity(steamId));
                    return ValueTask.CompletedTask;
                }

                _bans.BanOffline(issuer, steamId, null, BanType.SteamID, length, reasonText, isGlobal);
            }

            session.Close();
            return ValueTask.CompletedTask;
        }));

        return builder.Build();
    }

    private IMenu BuildActiveBans(IPlayer viewer, IMenu parent)
    {
        PagedCollectionComponent<Ban>? list = null;
        list = new PagedCollectionComponent<Ban>(() => _bans.ListActiveAsync(ListLimit))
        {
            Formatter = ban => T(viewer, "menu.record.row", ban.Id, Subject(ban.PlayerName, ban.SteamId64, ban.PlayerIp, ban.BanType == BanType.IP)),
            LoadingText = T(viewer, "menu.loading"),
            EmptyText = T(viewer, "menu.bans.none"),
            FailedText = T(viewer, "menu.load_failed"),
            OnItemSelected = ctx => BuildBanDetails(viewer, ctx.Item, ctx.Session.Menu, () => list?.Reload()).Open(viewer),
        };

        return _core.Menu.CreateMenu("admins.bans.active").WithTitle(T(viewer, "menu.bans.active")).WithParent(parent).Add(list).Build();
    }

    private IMenu BuildBanDetails(IPlayer viewer, Ban ban, IMenu parent, Action reload)
    {
        var builder = _core.Menu.CreateMenu("admins.ban.details").WithTitle(T(viewer, "menu.record.title", ban.Id)).WithParent(parent);

        AddRecordLines(builder, viewer, Subject(ban.PlayerName, ban.SteamId64, ban.PlayerIp, ban.BanType == BanType.IP), ban.Reason, ban.AdminName, ban.ExpiresAt, ban.Length, ban.GlobalBan);

        if (Has(viewer, "admins.commands.unban"))
        {
            builder.Add(new ButtonComponent(T(viewer, "menu.unban"), ctx =>
            {
                _bans.Unban(Issuer.From(viewer), ban, reload);
                ctx.Session.Close();
                return ValueTask.CompletedTask;
            }));
        }

        return builder.Build();
    }

    #endregion

    #region Comms

    private IMenu BuildCommsMenu(IPlayer viewer, IMenu parent)
    {
        var builder = _core.Menu.CreateMenu("admins.comms").WithTitle(T(viewer, "menu.comms")).WithParent(parent);

        if (Enum.GetValues<CommsAction>().Any(a => CanComms(viewer, a, global: false) || CanComms(viewer, a, global: true)))
        {
            builder.Add(new SubmenuComponent(T(viewer, "menu.comms.offline"), s => BuildCommsForm(viewer, null, s.Menu, CommsAction.Gag, global: false)));
        }

        builder.Add(new SubmenuComponent(T(viewer, "menu.comms.active"), s => BuildActiveComms(viewer, s.Menu)));
        return builder.Build();
    }

    /// <param name="target">The online player to sanction, or null for a form that asks for a SteamID64 or IP.</param>
    private IMenu BuildCommsForm(IPlayer viewer, PlayerRef? target, IMenu parent, CommsAction action, bool global)
    {
        var config = _comms.Config;
        var actions = Enum.GetValues<CommsAction>().Where(a => CanComms(viewer, a, global: false) || CanComms(viewer, a, global: true)).ToList();
        if (actions.Count == 0) return Notice(viewer, parent, T(viewer, "menu.no_permission"));

        var builder = _core.Menu.CreateMenu("admins.comms.form")
            .WithTitle(target is { } t ? T(viewer, "menu.comms.title", t.Name) : T(viewer, "menu.comms.offline"))
            .WithParent(parent);

        var actionChoice = new ChoiceComponent<CommsAction>(T(viewer, "menu.form.action"), actions, Math.Max(0, actions.IndexOf(action)))
        {
            Formatter = value => T(viewer, $"menu.form.action_{value.ToString().ToLowerInvariant()}"),
        };
        builder.Add(actionChoice);

        var typeChoice = new ChoiceComponent<SanctionType>(T(viewer, "menu.form.type"), [SanctionType.SteamID, SanctionType.IP])
        {
            Formatter = value => T(viewer, value == SanctionType.IP ? "menu.form.type_ip" : "menu.form.type_steamid"),
        };
        builder.Add(typeChoice);

        var identity = target is null ? IdentityInput(viewer) : null;
        if (identity is not null) builder.Add(identity);

        var (reason, customReason) = ReasonInputs(viewer, config.Reasons);
        builder.Add(reason).Add(customReason);

        var (duration, customDuration) = DurationInputs(viewer, config.DurationsInSeconds);
        builder.Add(duration).Add(customDuration);

        var anyGlobal = actions.Any(a => CanComms(viewer, a, global: true));
        var globalToggle = anyGlobal ? new ToggleComponent(T(viewer, "menu.form.global"), global) : null;
        if (globalToggle is not null) builder.Add(globalToggle);

        builder.Add(new ButtonComponent(T(viewer, "menu.form.confirm"), ctx =>
        {
            var session = ctx.Session;
            var issuer = Issuer.From(viewer);
            var chosen = actionChoice.GetItem(session);
            var chosenType = typeChoice.GetItem(session);
            var isGlobal = globalToggle?.GetValue(session) ?? false;

            if (!CanComms(viewer, chosen, isGlobal))
            {
                _messenger.Chat(viewer, "menu.no_permission");
                return ValueTask.CompletedTask;
            }

            if (!TryReadCommon(viewer, session, reason, customReason, duration, customDuration, out var reasonText, out var length)) return ValueTask.CompletedTask;

            if (target is { } online)
            {
                if (Resolve(viewer, online, session) is not { } player) return ValueTask.CompletedTask;

                if (!_access.CanTarget(viewer, player.SteamID))
                {
                    _messenger.Chat(viewer, "command.target_has_immunity", Names.Of(player), _access.GetImmunity(player.SteamID));
                    return ValueTask.CompletedTask;
                }

                _comms.SanctionPlayer(issuer, player, chosen, chosenType, length, reasonText, isGlobal);
                session.Close();
                return ValueTask.CompletedTask;
            }

            var value = identity!.GetValue(session)?.Trim() ?? string.Empty;
            if (chosenType == SanctionType.IP)
            {
                if (!Net.IsIp(value))
                {
                    _messenger.Chat(viewer, "menu.form.invalid_ip", value);
                    return ValueTask.CompletedTask;
                }

                _comms.SanctionOffline(issuer, 0, value, chosen, SanctionType.IP, length, reasonText, isGlobal);
            }
            else
            {
                if (!SteamIds.TryParse(value, out var steamId))
                {
                    _messenger.Chat(viewer, "command.invalid_steamid", value);
                    return ValueTask.CompletedTask;
                }

                if (!_access.CanTarget(viewer, steamId))
                {
                    _messenger.Chat(viewer, "command.target_has_immunity", steamId, _access.GetImmunity(steamId));
                    return ValueTask.CompletedTask;
                }

                _comms.SanctionOffline(issuer, steamId, null, chosen, SanctionType.SteamID, length, reasonText, isGlobal);
            }

            session.Close();
            return ValueTask.CompletedTask;
        }));

        return builder.Build();
    }

    private IMenu BuildActiveComms(IPlayer viewer, IMenu parent)
    {
        PagedCollectionComponent<Sanction>? list = null;
        list = new PagedCollectionComponent<Sanction>(() => _comms.ListActiveAsync(ListLimit))
        {
            Formatter = s => T(viewer, "menu.record.row_kind", s.Id, T(viewer, s.SanctionKind == SanctionKind.Gag ? "gag" : "mute"), Subject(s.PlayerName, s.SteamId64, s.PlayerIp, s.SanctionType == SanctionType.IP)),
            LoadingText = T(viewer, "menu.loading"),
            EmptyText = T(viewer, "menu.comms.none"),
            FailedText = T(viewer, "menu.load_failed"),
            OnItemSelected = ctx => BuildSanctionDetails(viewer, ctx.Item, ctx.Session.Menu, () => list?.Reload()).Open(viewer),
        };

        return _core.Menu.CreateMenu("admins.comms.active").WithTitle(T(viewer, "menu.comms.active")).WithParent(parent).Add(list).Build();
    }

    private IMenu BuildSanctionDetails(IPlayer viewer, Sanction sanction, IMenu parent, Action reload)
    {
        var builder = _core.Menu.CreateMenu("admins.comms.details").WithTitle(T(viewer, "menu.record.title", sanction.Id)).WithParent(parent);

        builder.Add(new TextComponent(T(viewer, "menu.record.kind", T(viewer, sanction.SanctionKind == SanctionKind.Gag ? "gag" : "mute"))));
        AddRecordLines(builder, viewer, Subject(sanction.PlayerName, sanction.SteamId64, sanction.PlayerIp, sanction.SanctionType == SanctionType.IP), sanction.Reason, sanction.AdminName, sanction.ExpiresAt, sanction.Length, sanction.GlobalSanction);

        if (Has(viewer, sanction.SanctionKind == SanctionKind.Gag ? "admins.commands.ungag" : "admins.commands.unmute"))
        {
            builder.Add(new ButtonComponent(T(viewer, "menu.lift"), ctx =>
            {
                _comms.LiftOne(Issuer.From(viewer), sanction, reload);
                ctx.Session.Close();
                return ValueTask.CompletedTask;
            }));
        }

        return builder.Build();
    }

    #endregion

    #region Server

    private IMenu BuildServerMenu(IPlayer viewer, IMenu parent)
    {
        var builder = _core.Menu.CreateMenu("admins.server").WithTitle(T(viewer, "menu.server")).WithParent(parent);

        if (Has(viewer, "admins.commands.restartround"))
        {
            builder.Add(new ButtonComponent(T(viewer, "menu.server.restart"), _ =>
            {
                _server.RestartRound(Issuer.From(viewer), 1f);
                return ValueTask.CompletedTask;
            }));
        }

        if (Has(viewer, "admins.commands.clean"))
        {
            builder.Add(new ButtonComponent(T(viewer, "menu.server.clean"), _ =>
            {
                _server.CleanWeapons(Issuer.From(viewer));
                return ValueTask.CompletedTask;
            }));
        }

        if (Has(viewer, "admins.commands.map"))
        {
            var map = new InputComponent(T(viewer, "menu.server.map"), string.Empty, value => !string.IsNullOrWhiteSpace(value)) { MaxLength = 64 };
            builder.Add(map);
            builder.Add(new ButtonComponent(T(viewer, "menu.server.map_change"), ctx =>
            {
                var name = map.GetValue(ctx.Session)?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    _messenger.Chat(viewer, "menu.server.map_required");
                }
                else if (_server.ChangeMap(Issuer.From(viewer), name))
                {
                    ctx.Session.Close();
                }

                return ValueTask.CompletedTask;
            }));
        }

        return builder.Build();
    }

    #endregion

    #region Form pieces

    private InputComponent IdentityInput(IPlayer viewer) =>
        new(T(viewer, "menu.form.identity"), string.Empty, value => SteamIds.TryParse(value, out _) || Net.IsIp(value)) { MaxLength = 64 };

    private (SelectorComponent<string> Preset, InputComponent Custom) ReasonInputs(IPlayer viewer, IReadOnlyList<string> reasons) =>
    (
        new SelectorComponent<string>(T(viewer, "menu.form.reason"), reasons) { EmptyText = T(viewer, "none") },
        new InputComponent(T(viewer, "menu.form.custom_reason"), string.Empty) { MaxLength = 128 }
    );

    private (SelectorComponent<int> Preset, InputComponent Custom) DurationInputs(IPlayer viewer, IReadOnlyList<int> durations) =>
    (
        new SelectorComponent<int>(T(viewer, "menu.form.duration"), durations)
        {
            Formatter = seconds => _messenger.Duration(viewer, TimeSpan.FromSeconds(seconds)),
            EmptyText = T(viewer, "time.permanent"),
        },
        new InputComponent(T(viewer, "menu.form.custom_duration"), string.Empty, value => Durations.TryParse(value, out _)) { MaxLength = 16 }
    );

    // A typed custom value beats the preset.
    private bool TryReadCommon(
        IPlayer viewer,
        IMenuSession session,
        SelectorComponent<string> reason,
        InputComponent customReason,
        SelectorComponent<int> duration,
        InputComponent customDuration,
        out string reasonText,
        out TimeSpan length)
    {
        length = TimeSpan.Zero;
        reasonText = customReason.GetValue(session)?.Trim() is { Length: > 0 } custom ? custom : reason.Choices.Count > 0 ? reason.GetItem(session) ?? string.Empty : string.Empty;

        if (reasonText.Length == 0)
        {
            _messenger.Chat(viewer, "menu.form.reason_required");
            return false;
        }

        if (customDuration.GetValue(session)?.Trim() is { Length: > 0 } typed)
        {
            if (!Durations.TryParse(typed, out length))
            {
                _messenger.Chat(viewer, "command.invalid_time_format", typed);
                return false;
            }

            return true;
        }

        if (duration.Choices.Count > 0) length = TimeSpan.FromSeconds(Math.Max(0, duration.GetItem(session)));
        return true;
    }

    private void AddRecordLines(IMenuBuilder builder, IPlayer viewer, string subject, string reason, string admin, long expiresAt, long length, bool global)
    {
        builder.Add(new TextComponent(T(viewer, "menu.record.subject", subject)));
        builder.Add(new TextComponent(T(viewer, "menu.record.reason", reason)));
        builder.Add(new TextComponent(T(viewer, "menu.record.admin", admin)));
        builder.Add(new TextComponent(T(viewer, "menu.record.length", _messenger.Duration(viewer, length))));
        builder.Add(new TextComponent(T(viewer, "menu.record.expires", _messenger.Timestamp(viewer, expiresAt))));
        builder.Add(new TextComponent(T(viewer, global ? "menu.record.global" : "menu.record.local")));
    }

    private static string Subject(string name, long steamId64, string ip, bool byIp)
    {
        var id = byIp ? ip : steamId64.ToString();
        return string.IsNullOrWhiteSpace(name) || name == "Unknown" ? id : $"{name} ({id})";
    }

    #endregion
}
