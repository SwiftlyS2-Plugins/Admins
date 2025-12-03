using Admins.Contract;
using Admins.Menu;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Admins.API;

public class AdminMenuAPIv1 : IAdminMenuAPIv1
{
    public IMenuAPI GenerateMenu(IPlayer player)
    {
        return AdminMenuAPI.GenerateMenu(player);
    }

    public void RegisterSubmenu(string translationKey, string[] permission, Func<IPlayer, string, string> getPlayerTranslationFromConsumer, IMenuAPI submenu)
    {
        AdminMenuAPI.RegisterSubmenu(translationKey, permission, getPlayerTranslationFromConsumer, submenu);
    }

    public void UnregisterSubmenu(string translationKey)
    {
        AdminMenuAPI.UnregisterSubmenu(translationKey);
    }
}