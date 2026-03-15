using System.Net;
using Admins.Bans.Contract;
using Admins.Bans.Database.Models;
using Microsoft.IdentityModel.Tokens;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using TimeSpanParserUtil;

namespace Admins.Bans.Menus;

public partial class AdminMenu
{
    public IMenuAPI BuildBanConfirmationMenu(IPlayer player, bool online, BanType banType, object playerQuery, string reason, string durationInput, bool global)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        var confirmButton = new ButtonMenuOption(TranslateString(player, "menu.bans.ban.confirm")) { CloseAfterClick = true };
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
                    var applicablePlayers = new List<IPlayer>();

                    foreach (var p in players)
                    {
                        if (!_serverCommands.CanAdminApplyActionToPlayer(player, p))
                        {
                            var targetImmunity = _serverCommands.GetPlayerImmunityLevel(p);
                            _serverCommands.NotifyAdminOfImmunityProtection(player, p.Controller.PlayerName, targetImmunity);
                        }
                        else
                        {
                            applicablePlayers.Add(p);
                        }
                    }

                    if (!applicablePlayers.Any())
                    {
                        return;
                    }

                    foreach (var p in applicablePlayers)
                    {
                        var Ban = new Ban
                        {
                            SteamId64 = (long)p.SteamID,
                            BanType = banType,
                            Reason = reason,
                            PlayerName = p.Controller.PlayerName,
                            PlayerIp = p.IPAddress,
                            ExpiresAt = expiresAt,
                            Length = (long)durationTimeSpan.TotalMilliseconds,
                            AdminSteamId64 = (long)player.SteamID,
                            AdminName = adminName,
                            Server = ServerManager!.GetServerGUID(),
                            GlobalBan = global
                        };

                        _bansManager.AddBan(Ban);
                    }

                    _serverCommands.NotifyBanApplied(applicablePlayers, player, expiresAt, adminName, reason);
                    _serverCommands.KickBannedPlayers(applicablePlayers);
                }
                else
                {
                    if (banType == BanType.SteamID)
                    {
                        var steamId = new CSteamID(playerQuery.ToString()!);
                        var targetSteamId64 = steamId.GetSteamID64();

                        if (!_serverCommands.CanAdminApplyActionToSteamId(player, targetSteamId64))
                        {
                            var targetAdmin = AdminsManager!.GetAdmin(targetSteamId64);
                            var targetImmunity = targetAdmin?.Immunity ?? 0;
                            _serverCommands.NotifyAdminOfImmunityProtection(player, "Unknown", targetImmunity);
                            return;
                        }

                        var expiresAt = _serverCommands.CalculateExpiresAt(durationTimeSpan);
                        var adminName = player.Controller.PlayerName;

                        var ban = new Ban
                        {
                            SteamId64 = (long)targetSteamId64,
                            BanType = banType,
                            Reason = reason,
                            PlayerName = "Unknown",
                            PlayerIp = "",
                            ExpiresAt = expiresAt,
                            Length = (long)durationTimeSpan.TotalMilliseconds,
                            AdminSteamId64 = (long)player.SteamID,
                            AdminName = adminName,
                            Server = ServerManager!.GetServerGUID(),
                            GlobalBan = global
                        };

                        _bansManager.AddBan(ban);

                        var localizer = Core.Translation.GetPlayerLocalizer(player);
                        var expiryText = expiresAt == 0
                            ? localizer["never"]
                            : _serverCommands.FormatTimestampInTimeZone(expiresAt);

                        var messageKey = "command.bano_success";
                        var target = $"SteamID64 [green]{targetSteamId64}[default]";
                        var globalSuffix = global ? $"([green]{localizer["global"]}[default])" : "";
                        var message = localizer[
                            messageKey,
                            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                            adminName,
                            target,
                            expiryText,
                            globalSuffix,
                            reason
                        ];
                        player.SendChat(message);
                    }
                    else if (banType == BanType.IP)
                    {
                        var expiresAt = _serverCommands.CalculateExpiresAt(durationTimeSpan);
                        var adminName = player.Controller.PlayerName;

                        var ban = new Ban
                        {
                            SteamId64 = 0,
                            BanType = banType,
                            Reason = reason,
                            PlayerName = "Unknown",
                            PlayerIp = playerQuery.ToString()!,
                            ExpiresAt = expiresAt,
                            Length = (long)durationTimeSpan.TotalMilliseconds,
                            AdminSteamId64 = (long)player.SteamID,
                            AdminName = adminName,
                            Server = ServerManager!.GetServerGUID(),
                            GlobalBan = global
                        };

                        _bansManager.AddBan(ban);

                        var localizer = Core.Translation.GetPlayerLocalizer(player);
                        var expiryText = expiresAt == 0
                            ? localizer["never"]
                            : _serverCommands.FormatTimestampInTimeZone(expiresAt);

                        var messageKey = "command.banipo_success";
                        var target = $"IP [green]{playerQuery}[default]";
                        var globalSuffix = global ? $"([green]{localizer["global"]}[default])" : "";
                        var message = localizer[
                            messageKey,
                            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                            adminName,
                            target,
                            expiryText,
                            globalSuffix,
                            reason
                        ];
                        player.SendChat(message);
                    }
                }
            });
        };

        var summaryInChatButton = new ButtonMenuOption(TranslateString(player, "menu.bans.ban.summary_in_chat"));
        summaryInChatButton.Click += async (_, args) =>
        {
            await Core.Scheduler.NextTickAsync(() =>
            {
                var localizer = Core.Translation.GetPlayerLocalizer(player);
                player.SendChat(
                    localizer[
                        "menu.bans.bans.give_summary_in_chat",
                        banType.ToString(),
                        online ? string.Join(", ", ((List<IPlayer>)playerQuery).Select(p => p.Controller!.PlayerName)) : playerQuery.ToString()!,
                        durationInput,
                        reason,
                        global ? localizer["yes"] : localizer["no"]
                    ]
                );
            });
        };

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.ban.confirm"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new TextMenuOption(TranslateString(player, "menu.bans.bans.confirmation_message")))
            .AddOption(confirmButton)
            .AddOption(summaryInChatButton);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildGlobalBanMenu(IPlayer player, bool online, BanType banType, object playerQuery, string reason, string durationInput, IMenuAPI? parentMenu)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.ban.give.global"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.bans.ban.give.global.yes"), () => BuildBanConfirmationMenu(player, online, banType, playerQuery, reason, durationInput, true)))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.bans.ban.give.global.no"), () => BuildBanConfirmationMenu(player, online, banType, playerQuery, reason, durationInput, false)));

        if (parentMenu != null) menuBuilder.BindToParent(parentMenu);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildDurationsMenu(IPlayer player, bool online, BanType banType, object playerQuery, string reason, IMenuAPI? parentMenu)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.ban.give.duration"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var customDuration = new InputMenuOption(
            TranslateString(player, "menu.bans.ban.give.custom_duration"),
            64, (input) => TimeSpanParser.TryParse(input.Trim(), out var _)
        );

        customDuration.ValueChanged += (_, args) =>
        {
            Core.MenusAPI.OpenMenuForPlayer(player, BuildGlobalBanMenu(player, online, banType, playerQuery, reason, args.NewValue.Trim(), args.Option.Menu));
        };

        foreach (var banDuration in _bansConfiguration.CurrentValue.BansDurationsInSeconds)
        {
            menuBuilder.AddOption(
                new SubmenuMenuOption(TimeSpan.FromSeconds(banDuration).ToString(), () => BuildGlobalBanMenu(player, online, banType, playerQuery, reason, TimeSpan.FromSeconds(banDuration).ToString(), null))
            );
        }

        if (parentMenu != null) menuBuilder.BindToParent(parentMenu);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildReasonsMenu(IPlayer player, bool online, BanType banType, object playerQuery)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.ban.give.reason"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var customReason = new InputMenuOption(
            TranslateString(player, "menu.bans.ban.give.custom_reason"),
            64, (input) => input.Trim().IsNullOrEmpty()
        );

        customReason.ValueChanged += (_, args) =>
        {
            Core.MenusAPI.OpenMenuForPlayer(player, BuildDurationsMenu(player, online, banType, playerQuery, args.NewValue.Trim(), args.Option.Menu));
        };

        menuBuilder.AddOption(customReason);

        foreach (var banReason in _bansConfiguration.CurrentValue.BansReasons)
        {
            menuBuilder.AddOption(
                new SubmenuMenuOption(banReason, () => BuildDurationsMenu(player, online, banType, playerQuery, banReason, null))
            );
        }

        return menuBuilder.Build();
    }

    public IMenuAPI BuildPlayerListMenu(IPlayer player, bool online, BanType banType)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();
        var selectedPlayers = new List<IPlayer>();
        var searchInput = "";

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.bans.give.playertitle"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        if (online)
        {
            var submenuOption = new SubmenuMenuOption(
                TranslateString(player, "menu.continue"),
                () => BuildReasonsMenu(player, online, banType, selectedPlayers))
            { Enabled = false };

            var players = Core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient && p.PlayerID != player.PlayerID);
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
                menuBuilder.AddOption(new TextMenuOption(TranslateString(player, "menu.bans.bans.give.no_players")));
            }

            menuBuilder.AddOption(submenuOption);
        }
        else
        {
            var submenuOption = new SubmenuMenuOption(
                TranslateString(player, "menu.continue"),
                () => BuildReasonsMenu(player, online, banType, searchInput))
            { Enabled = false };

            var inputOption = new InputMenuOption(
                banType == BanType.SteamID ? TranslateString(player, "menu.bans.bans.give.enter_steamid") : TranslateString(player, "menu.bans.bans.give.enter_ip"),
                64, (input) =>
                {
                    input = input.Trim();
                    if (input.IsNullOrEmpty())
                    {
                        return false;
                    }

                    if (banType == BanType.SteamID)
                    {
                        var steamId = new CSteamID(input);
                        return steamId.IsValid();
                    }
                    else if (banType == BanType.IP)
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

    public IMenuAPI BuildBanTypeMenu(IPlayer player, bool online)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.ban.give.kind"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.bans.ban.give.kind.ip"), () => BuildPlayerListMenu(player, online, BanType.IP)))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.bans.ban.give.kind.steamid"), () => BuildPlayerListMenu(player, online, BanType.SteamID)));

        return menuBuilder.Build();
    }

    public IMenuAPI BuildBanGiveMenu(IPlayer player)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.bans.give"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.bans.bans.give.online"), () => BuildBanTypeMenu(player, true)))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.bans.bans.give.offline"), () => BuildBanTypeMenu(player, false)));

        return menuBuilder.Build();
    }
}