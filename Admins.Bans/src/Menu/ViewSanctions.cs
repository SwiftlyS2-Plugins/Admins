using Admins.Bans.Contract;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Admins.Bans.Menus;

public partial class AdminMenu
{
    public IMenuAPI BuildBanRemovalConfirmation(IPlayer player, IBan ban)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        var confirmButton = new ButtonMenuOption(TranslateString(player, "menu.bans.ban.confirm"));
        confirmButton.Click += async (_, args) =>
        {
            await Core.Scheduler.NextTickAsync(() =>
            {
                _bansManager.RemoveBan(ban);

                var localizer = Core.Translation.GetPlayerLocalizer(player);
                player.SendChat(localizer[
                    "menu.bans.bans.removeban.success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    ban.Id
                ]);
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
                        "menu.bans.bans.remove_summary_in_chat",
                        ban.BanType.ToString(),
                        ban.BanType == BanType.SteamID ? ban.SteamId64 : ban.PlayerIp,
                        TimeSpan.FromMilliseconds(ban.Length),
                        ban.Reason,
                        ban.GlobalBan ? localizer["yes"] : localizer["no"]
                    ]
                );
            });
        };

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.bans.remove"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        menuBuilder
            .AddOption(new TextMenuOption(TranslateString(player, "menu.bans.bans.remove_message")))
            .AddOption(confirmButton)
            .AddOption(summaryInChatButton);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildBanDetailsMenu(IPlayer player, IBan ban)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.bans.view"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var expiresAt = _serverCommands.CalculateExpiresAt(TimeSpan.FromMilliseconds(ban.Length));

        menuBuilder
            .AddOption(new TextMenuOption($"ID: {ban.Id}"))
            .AddOption(new TextMenuOption($"Type: {ban.BanType}"))
            .AddOption(new TextMenuOption($"Target SteamID64: {ban.SteamId64}"))
            .AddOption(new TextMenuOption($"Target IP: {ban.PlayerIp}"))
            .AddOption(new TextMenuOption($"Issued By Admin: {ban.AdminName}"))
            .AddOption(new TextMenuOption($"Reason: {ban.Reason}"))
            .AddOption(new TextMenuOption($"Global: {(ban.GlobalBan ? localizer["yes"] : localizer["no"])}"))
            .AddOption(new TextMenuOption($"Duration: {TimeSpan.FromMilliseconds(ban.Length)}"))
            .AddOption(new TextMenuOption($"Expires At: {(expiresAt == 0 ? localizer["never"] : _serverCommands.FormatTimestampInTimeZone(expiresAt))}"))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.bans.bans.remove"), () => BuildBanRemovalConfirmation(player, ban)));

        return menuBuilder.Build();
    }

    public IMenuAPI BuildBanViewMenu(IPlayer player)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.bans.bans.view"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var bans = _bansManager.FindBans(status: RecordStatus.Active).Where(ban =>
            ban.Server == ServerManager!.GetServerGUID() || ban.GlobalBan
        );

        foreach (var ban in bans)
        {
            menuBuilder.AddOption(
                new SubmenuMenuOption(
                    $"#{ban.Id} | {ban.BanType}",
                    () => BuildBanDetailsMenu(player, ban)
                )
            );
        }

        return menuBuilder.Build();
    }
}