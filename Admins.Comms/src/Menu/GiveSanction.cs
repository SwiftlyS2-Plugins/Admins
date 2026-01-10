using System.Net;
using Admins.Comms.Contract;
using Admins.Comms.Database.Models;
using Microsoft.IdentityModel.Tokens;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using TimeSpanParserUtil;

namespace Admins.Comms.Menus;

public partial class AdminMenu
{
    public IMenuAPI BuildSanctionConfirmationMenu(IPlayer player, bool online, SanctionType sanctionType, SanctionKind sanctionKind, object playerQuery, string reason, string durationInput, bool global)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        var confirmButton = new ButtonMenuOption(TranslateString(player, "menu.comms.sanction.confirm")) { CloseAfterClick = true };
        confirmButton.Click += async (_, args) =>
        {
            await Core.Scheduler.NextTickAsync(() =>
            {
                var durationTimeSpan = TimeSpanParser.Parse(durationInput);

                if (online)
                {
                    var expiresAt = _serverCommands.CalculateExpiresAt(durationTimeSpan);
                    var adminName = player.Controller.PlayerName;

                    var players = (List<IPlayer>)playerQuery;
                    foreach (var p in players)
                    {
                        var sanction = new Sanction
                        {
                            SteamId64 = player.SteamID,
                            SanctionKind = sanctionKind,
                            SanctionType = sanctionType,
                            Reason = reason,
                            PlayerName = player.Controller.PlayerName,
                            PlayerIp = player.IPAddress,
                            ExpiresAt = (ulong)expiresAt,
                            Length = (ulong)durationTimeSpan.TotalMilliseconds,
                            AdminSteamId64 = player.SteamID,
                            AdminName = adminName,
                            Server = ServerManager!.GetServerGUID(),
                            GlobalSanction = global
                        };

                        _commsManager.AddSanction(sanction);
                    }

                    _serverCommands.NotifySanctionApplied(players, player, sanctionKind, expiresAt, adminName, reason);
                }
                else
                {
                    if (sanctionType == SanctionType.SteamID)
                    {
                        var steamId = new CSteamID(playerQuery.ToString()!);
                        var expiresAt = _serverCommands.CalculateExpiresAt(durationTimeSpan);
                        var adminName = player.Controller.PlayerName;

                        var sanction = new Sanction
                        {
                            SteamId64 = steamId.GetSteamID64(),
                            SanctionType = sanctionType,
                            SanctionKind = sanctionKind,
                            Reason = reason,
                            PlayerName = "Unknown",
                            PlayerIp = "",
                            ExpiresAt = (ulong)expiresAt,
                            Length = (ulong)durationTimeSpan.TotalMilliseconds,
                            AdminSteamId64 = player.SteamID,
                            AdminName = adminName,
                            Server = ServerManager!.GetServerGUID(),
                            GlobalSanction = global
                        };

                        _commsManager.AddSanction(sanction);

                        var localizer = Core.Translation.GetPlayerLocalizer(player);
                        var messageKey = sanctionKind == SanctionKind.Gag ? "command.gago_success" : "command.muteo_success";
                        var expiryText = expiresAt == 0
                            ? localizer["never"]
                            : _serverCommands.FormatTimestampInTimeZone(expiresAt);

                        var sanctionTypeKey = sanctionKind == SanctionKind.Gag
                            ? (global ? "global_gag" : "gag")
                            : (global ? "global_mute" : "mute");
                        var sanctionTypeText = localizer[sanctionTypeKey];

                        var message = localizer[
                            messageKey,
                            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                            adminName,
                            sanctionTypeText,
                            steamId.GetSteamID64(),
                            expiryText,
                            reason
                        ];
                        player.SendChat(message);
                    }
                    else if (sanctionType == SanctionType.IP)
                    {
                        var expiresAt = _serverCommands.CalculateExpiresAt(durationTimeSpan);
                        var adminName = player.Controller.PlayerName;

                        var sanction = new Sanction
                        {
                            SteamId64 = 0,
                            SanctionType = SanctionType.IP,
                            SanctionKind = sanctionKind,
                            Reason = reason,
                            PlayerName = "Unknown",
                            PlayerIp = playerQuery.ToString()!,
                            ExpiresAt = (ulong)expiresAt,
                            Length = (ulong)durationTimeSpan.TotalMilliseconds,
                            AdminSteamId64 = player.SteamID,
                            AdminName = adminName,
                            Server = ServerManager!.GetServerGUID(),
                            GlobalSanction = global
                        };

                        _commsManager.AddSanction(sanction);

                        var localizer = Core.Translation.GetPlayerLocalizer(player);
                        var messageKey = sanctionKind == SanctionKind.Gag ? "command.gagipo_success" : "command.muteipo_success";
                        var expiryText = expiresAt == 0
                            ? localizer["never"]
                            : _serverCommands.FormatTimestampInTimeZone(expiresAt);

                        var sanctionTypeKey = sanctionKind == SanctionKind.Gag
                            ? (global ? "global_gag" : "gag")
                            : (global ? "global_mute" : "mute");

                        var sanctionTypeText = localizer[sanctionTypeKey];

                        var message = localizer[
                            messageKey,
                            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                            adminName,
                            sanctionTypeText,
                            playerQuery.ToString()!,
                            expiryText,
                            reason
                        ];
                        player.SendChat(message);
                    }
                }
            });
        };

        var summaryInChatButton = new ButtonMenuOption(TranslateString(player, "menu.comms.sanction.summary_in_chat"));
        summaryInChatButton.Click += async (_, args) =>
        {
            await Core.Scheduler.NextTickAsync(() =>
            {
                var localizer = Core.Translation.GetPlayerLocalizer(player);
                player.SendChat(
                    localizer[
                        "menu.comms.sanctions.give_summary_in_chat",
                        sanctionType.ToString(),
                        sanctionKind.ToString(),
                        online ? string.Join(", ", ((List<IPlayer>)playerQuery).Select(p => p.Controller!.PlayerName)) : playerQuery.ToString()!,
                        durationInput,
                        reason,
                        global ? localizer["yes"] : localizer["no"]
                    ]
                );
            });
        };

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanction.confirm"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new TextMenuOption(TranslateString(player, "menu.comms.sanctions.confirmation_message")))
            .AddOption(confirmButton)
            .AddOption(summaryInChatButton);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildGlobalSanctionMenu(IPlayer player, bool online, SanctionType sanctionType, SanctionKind sanctionKind, object playerQuery, string reason, string durationInput, IMenuAPI? parentMenu)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanction.give.global"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanction.give.global.yes"), () => BuildSanctionConfirmationMenu(player, online, sanctionType, sanctionKind, playerQuery, reason, durationInput, true)))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanction.give.global.no"), () => BuildSanctionConfirmationMenu(player, online, sanctionType, sanctionKind, playerQuery, reason, durationInput, false)));

        if (parentMenu != null) menuBuilder.BindToParent(parentMenu);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildDurationsMenu(IPlayer player, bool online, SanctionType sanctionType, SanctionKind sanctionKind, object playerQuery, string reason, IMenuAPI? parentMenu)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanction.give.duration"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var customDuration = new InputMenuOption(
            TranslateString(player, "menu.comms.sanction.give.custom_duration"),
            64, (input) => TimeSpanParser.TryParse(input.Trim(), out var _)
        );

        customDuration.ValueChanged += (_, args) =>
        {
            Core.MenusAPI.OpenMenuForPlayer(player, BuildGlobalSanctionMenu(player, online, sanctionType, sanctionKind, playerQuery, reason, args.NewValue.Trim(), args.Option.Menu));
        };

        foreach (var commDuration in _commsConfiguration.CurrentValue.CommsDurationsInSeconds)
        {
            menuBuilder.AddOption(
                new SubmenuMenuOption(TimeSpan.FromSeconds(commDuration).ToString(), () => BuildGlobalSanctionMenu(player, online, sanctionType, sanctionKind, playerQuery, reason, TimeSpan.FromSeconds(commDuration).ToString(), null))
            );
        }

        if (parentMenu != null) menuBuilder.BindToParent(parentMenu);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildReasonsMenu(IPlayer player, bool online, SanctionType sanctionType, SanctionKind sanctionKind, object playerQuery)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanction.give.reason"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var customReason = new InputMenuOption(
            TranslateString(player, "menu.comms.sanction.give.custom_reason"),
            64, (input) => input.Trim().IsNullOrEmpty()
        );

        customReason.ValueChanged += (_, args) =>
        {
            Core.MenusAPI.OpenMenuForPlayer(player, BuildDurationsMenu(player, online, sanctionType, sanctionKind, playerQuery, args.NewValue.Trim(), args.Option.Menu));
        };

        menuBuilder.AddOption(customReason);

        foreach (var commReason in _commsConfiguration.CurrentValue.CommsReasons)
        {
            menuBuilder.AddOption(
                new SubmenuMenuOption(commReason, () => BuildDurationsMenu(player, online, sanctionType, sanctionKind, playerQuery, commReason, null))
            );
        }

        return menuBuilder.Build();
    }

    public IMenuAPI BuildPlayerListMenu(IPlayer player, bool online, SanctionType sanctionType, SanctionKind sanctionKind)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();
        var selectedPlayers = new List<IPlayer>();
        var searchInput = "";

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanctions.give.playertitle"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        if (online)
        {
            var submenuOption = new SubmenuMenuOption(
                TranslateString(player, "menu.continue"),
                () => BuildReasonsMenu(player, online, sanctionType, sanctionKind, selectedPlayers))
            { Enabled = false };

            var players = Core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient);
            if (players.Any())
            {
                foreach (var p in players)
                {
                    var option = new ToggleMenuOption(
                        $"{p.Controller!.PlayerName} (#{p.PlayerID})",
                        false
                    );

                    option.ValueChanged += (_, args) =>
                    {
                        if (args.NewValue)
                        {
                            selectedPlayers.Add(p);
                        }
                        else
                        {
                            selectedPlayers.Remove(p);
                        }

                        submenuOption.Enabled = selectedPlayers.Count > 0;
                    };

                    menuBuilder.AddOption(option);
                }
            }
            else
            {
                menuBuilder.AddOption(new TextMenuOption(TranslateString(player, "menu.comms.sanctions.give.no_players")));
            }

            menuBuilder.AddOption(submenuOption);
        }
        else
        {
            var submenuOption = new SubmenuMenuOption(
                TranslateString(player, "menu.continue"),
                () => BuildReasonsMenu(player, online, sanctionType, sanctionKind, searchInput))
            { Enabled = false };

            var inputOption = new InputMenuOption(
                sanctionType == SanctionType.SteamID ? TranslateString(player, "menu.comms.sanctions.give.enter_steamid") : TranslateString(player, "menu.comms.sanctions.give.enter_ip"),
                64, (input) =>
                {
                    input = input.Trim();
                    if (input.IsNullOrEmpty())
                    {
                        return false;
                    }

                    if (sanctionType == SanctionType.SteamID)
                    {
                        var steamId = new CSteamID(input);
                        return steamId.IsValid();
                    }
                    else if (sanctionType == SanctionType.IP)
                    {
                        return IPAddress.TryParse(input, out var ipAddress) && ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
                    }
                    else return false;
                }
            );

            inputOption.ValueChanged += (_, args) =>
            {
                searchInput = args.NewValue.Trim();
                submenuOption.Enabled = true;
            };

            menuBuilder.AddOption(inputOption);

            menuBuilder.AddOption(submenuOption);
        }

        return menuBuilder.Build();
    }

    public IMenuAPI BuildSanctionKindMenu(IPlayer player, bool online, SanctionType sanctionType)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanction.give.kind"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanction.give.type.gag"), () => BuildPlayerListMenu(player, online, sanctionType, SanctionKind.Gag)))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanction.give.type.mute"), () => BuildPlayerListMenu(player, online, sanctionType, SanctionKind.Mute)));

        return menuBuilder.Build();
    }

    public IMenuAPI BuildSanctionTypeMenu(IPlayer player, bool online)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanctions.give.type"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanction.give.kind.ip"), () => BuildSanctionKindMenu(player, online, SanctionType.IP)))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanction.give.kind.steamid"), () => BuildSanctionKindMenu(player, online, SanctionType.SteamID)));

        return menuBuilder.Build();
    }

    public IMenuAPI BuildSanctionGiveMenu(IPlayer player)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanctions.give"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanctions.give.online"), () => BuildSanctionTypeMenu(player, true)))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanctions.give.offline"), () => BuildSanctionTypeMenu(player, false)));

        return menuBuilder.Build();
    }
}