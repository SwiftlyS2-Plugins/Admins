using Admins.Comms.Commands;
using Admins.Comms.Configuration;
using Admins.Comms.Manager;
using Admins.Core.Contract;
using Admins.Menu.Contract;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Comms.Menus;

public partial class AdminMenu
{
    private ISwiftlyCore Core = null!;
    private IAdminMenuAPI? _adminMenuAPI;
    private IOptionsMonitor<CommsConfiguration> _commsConfiguration;
    private ServerCommands _serverCommands;
    private CommsManager _commsManager = null!;
    private IServerManager? ServerManager;
    private IConfigurationManager ConfigurationManager = null!;

    public AdminMenu(ISwiftlyCore core, IOptionsMonitor<CommsConfiguration> commsConfiguration, ServerCommands serverCommands, CommsManager commsManager)
    {
        Core = core;
        _commsConfiguration = commsConfiguration;
        _serverCommands = serverCommands;
        _commsManager = commsManager;
        core.Registrator.Register(this);
    }

    public void SetAdminMenuAPI(IAdminMenuAPI adminMenuAPI)
    {
        _adminMenuAPI = adminMenuAPI;
    }

    public void SetServerManager(IServerManager serverManager)
    {
        ServerManager = serverManager;
    }

    public void SetConfigurationManager(IConfigurationManager configurationManager)
    {
        ConfigurationManager = configurationManager;
    }

    public string TranslateString(IPlayer player, string key)
    {
        var localizer = Core.Translation.GetPlayerLocalizer(player);
        return localizer[key];
    }

    public void LoadAdminMenu()
    {
        if (_adminMenuAPI == null) return;

        _adminMenuAPI.RegisterSubmenu("menu.comms.title", ["admins.menu.comms"], TranslateString, (player) =>
        {
            var menuBuilder = Core.MenusAPI.CreateBuilder();

            menuBuilder
                .Design.SetMenuTitle(TranslateString(player, "menu.comms.title"))
                .Design.SetMenuFooterColor(_adminMenuAPI.GetMenuColor())
                .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
                .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
                .AddOption(new SubmenuMenuOption(
                    TranslateString(player, "menu.comms.sanctions.give"),
                    () => BuildSanctionGiveMenu(player)
                ))
                .AddOption(new SubmenuMenuOption(
                    TranslateString(player, "menu.comms.sanctions.view"),
                    () => BuildSanctionViewMenu(player)
                ));

            return menuBuilder.Build();
        });
    }

    public void UnloadAdminMenu()
    {
        if (_adminMenuAPI == null) return;

        _adminMenuAPI.UnregisterSubmenu("menu.comms.title");
    }
}