using Admins.Comms.Contract;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Admins.Comms.Menus;

public partial class AdminMenu
{
    public IMenuAPI BuildSanctionRemovalConfirmation(IPlayer player, ISanction sanction)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        var confirmButton = new ButtonMenuOption(TranslateString(player, "menu.comms.sanction.confirm"));
        confirmButton.Click += async (_, args) =>
        {
            await Core.Scheduler.NextTickAsync(() =>
            {
                _commsManager.RemoveSanction(sanction);

                var localizer = Core.Translation.GetPlayerLocalizer(player);
                player.SendChat(localizer[
                    "menu.comms.sanctions.removesanction.success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    sanction.Id
                ]);
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
                        "menu.comms.sanctions.remove_summary_in_chat",
                        sanction.SanctionType.ToString(),
                        sanction.SanctionKind.ToString(),
                        sanction.SteamId64,
                        TimeSpan.FromMilliseconds(sanction.Length),
                        sanction.Reason,
                        sanction.GlobalSanction ? localizer["yes"] : localizer["no"]
                    ]
                );
            });
        };

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanctions.remove"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        menuBuilder
            .AddOption(new TextMenuOption(TranslateString(player, "menu.comms.sanctions.remove_message")))
            .AddOption(confirmButton)
            .AddOption(summaryInChatButton);

        return menuBuilder.Build();
    }

    public IMenuAPI BuildSanctionDetailsMenu(IPlayer player, ISanction sanction)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanctions.view"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var expiresAt = _serverCommands.CalculateExpiresAt(TimeSpan.FromMilliseconds(sanction.Length));

        menuBuilder
            .AddOption(new TextMenuOption($"ID: {sanction.Id}"))
            .AddOption(new TextMenuOption($"Type: {sanction.SanctionType}"))
            .AddOption(new TextMenuOption($"Kind: {sanction.SanctionKind}"))
            .AddOption(new TextMenuOption($"Target SteamID64: {sanction.SteamId64}"))
            .AddOption(new TextMenuOption($"Target IP: {sanction.PlayerIp}"))
            .AddOption(new TextMenuOption($"Issued By Admin: {sanction.AdminName}"))
            .AddOption(new TextMenuOption($"Reason: {sanction.Reason}"))
            .AddOption(new TextMenuOption($"Global: {(sanction.GlobalSanction ? localizer["yes"] : localizer["no"])}"))
            .AddOption(new TextMenuOption($"Duration: {TimeSpan.FromMilliseconds(sanction.Length)}"))
            .AddOption(new TextMenuOption($"Expires At: {(expiresAt == 0 ? localizer["never"] : _serverCommands.FormatTimestampInTimeZone(expiresAt))}"))
            .AddOption(new SubmenuMenuOption(TranslateString(player, "menu.comms.sanctions.remove"), () => BuildSanctionRemovalConfirmation(player, sanction)));

        return menuBuilder.Build();
    }

    public IMenuAPI BuildSanctionViewMenu(IPlayer player)
    {
        var menuBuilder = Core.MenusAPI.CreateBuilder();
        var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        menuBuilder
            .Design.SetMenuTitle(TranslateString(player, "menu.comms.sanctions.view"))
            .Design.SetMenuFooterColor(_adminMenuAPI!.GetMenuColor())
            .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
            .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor());

        var sanctions = _commsManager.GetSanctions().Where(sanction =>
            (sanction.ExpiresAt == 0 || sanction.ExpiresAt > currentTime) &&
            (sanction.Server == ServerManager!.GetServerGUID() || sanction.GlobalSanction)
        );

        foreach (var sanction in sanctions)
        {
            menuBuilder.AddOption(
                new SubmenuMenuOption(
                    $"#{sanction.Id} | {sanction.SanctionKind}",
                    () => BuildSanctionDetailsMenu(player, sanction)
                )
            );
        }

        return menuBuilder.Build();
    }
}