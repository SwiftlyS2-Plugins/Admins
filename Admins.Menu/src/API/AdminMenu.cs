using Admins.Menu.Contract;
using Admins.Menu.Menu;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Admins.Menu.API;

public class AdminMenuAPI : IAdminMenuAPI
{
    public AdminMenu? _menuManager;

    public AdminMenuAPI(AdminMenu menuManager)
    {
        _menuManager = menuManager;
    }

    public void RegisterSubmenu(string translationKey, string[] permission, Func<IPlayer, string, string> getPlayerTranslationFromConsumer, Func<IPlayer, IMenuAPI> submenu)
    {
        _menuManager!.RegisterSubmenu(translationKey, permission, getPlayerTranslationFromConsumer, submenu);
    }

    public void UnregisterSubmenu(string translationKey)
    {
        _menuManager!.UnregisterSubmenu(translationKey);
    }

    public IMenuAPI CreateAdminMenu(IPlayer player)
    {
        return _menuManager!.CreateAdminMenu(player);
    }
}