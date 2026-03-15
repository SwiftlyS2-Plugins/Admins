using Admins.Bans.Commands;
using Admins.Bans.Configuration;
using Admins.Bans.Manager;
using Admins.Core.Contract;
using Admins.Menu.Contract;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Bans.Menus;

public partial class AdminMenu
{
    private ISwiftlyCore Core = null!;
    private IAdminMenuAPI? _adminMenuAPI;
    private IOptionsMonitor<BansConfiguration> _bansConfiguration;
    private ServerCommands _serverCommands;
    private BansManager _bansManager = null!;
    private IServerManager? ServerManager;
    private IConfigurationManager ConfigurationManager = null!;
    private IAdminsManager? AdminsManager;

    public AdminMenu(ISwiftlyCore core, IOptionsMonitor<BansConfiguration> bansConfiguration, ServerCommands serverCommands, BansManager bansManager)
    {
        Core = core;
        _bansConfiguration = bansConfiguration;
        _serverCommands = serverCommands;
        _bansManager = bansManager;
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

    public void SetAdminsManager(IAdminsManager adminsManager)
    {
        AdminsManager = adminsManager;
    }

    public string TranslateString(IPlayer player, string key)
    {
        var localizer = Core.Translation.GetPlayerLocalizer(player);
        return localizer[key];
    }

    public void LoadAdminMenu()
    {
        if (_adminMenuAPI == null) return;

        _adminMenuAPI.RegisterSubmenu("menu.bans.title", ["admins.menu.bans"], TranslateString, (player) =>
        {
            var menuBuilder = Core.MenusAPI.CreateBuilder();

            menuBuilder
                .Design.SetMenuTitle(TranslateString(player, "menu.bans.title"))
                .Design.SetMenuFooterColor(_adminMenuAPI.GetMenuColor())
                .Design.SetVisualGuideLineColor(_adminMenuAPI.GetMenuColor())
                .Design.SetNavigationMarkerColor(_adminMenuAPI.GetMenuColor())
                .AddOption(new SubmenuMenuOption(
                    TranslateString(player, "menu.bans.bans.give"),
                    () => BuildBanGiveMenu(player)
                ))
                .AddOption(new SubmenuMenuOption(
                    TranslateString(player, "menu.bans.bans.view"),
                    () => BuildBanViewMenu(player)
                ));

            return menuBuilder.Build();
        });
    }

    public void UnloadAdminMenu()
    {
        if (_adminMenuAPI == null) return;

        _adminMenuAPI.UnregisterSubmenu("menu.bans.title");
    }
}