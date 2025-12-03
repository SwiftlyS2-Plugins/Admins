using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

public interface IAdminMenuAPIv1
{
    /// <summary>
    /// Generates the admin menu for a given player.
    /// </summary>
    /// <param name="player">The player for whom the admin menu is generated.</param>
    /// <returns>The generated admin menu.</returns>
    public IMenuAPI GenerateMenu(IPlayer player);

    /// <summary>
    /// Registers a submenu to be displayed in the admin menu.
    /// </summary>
    /// <param name="translationKey">The translation key for the submenu title.</param>
    /// <param name="permission">The permissions required to access the submenu.</param>
    /// <param name="getPlayerTranslationFromConsumer">A function to get the player's localized translation for the submenu title.</param>
    /// <param name="submenu">The submenu to register.</param>
    public void RegisterSubmenu(string translationKey, string[] permission, Func<IPlayer, string, string> getPlayerTranslationFromConsumer, IMenuAPI submenu);

    /// <summary>
    /// Unregisters a submenu from the admin menu.
    /// </summary>
    /// <param name="translationKey">The translation key for the submenu title to unregister.</param>
    public void UnregisterSubmenu(string translationKey);
}