using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

/// <summary>
/// Shared interface key: <c>Admins.Menu.V2</c>. Call from the game thread.
/// </summary>
public interface IAdminMenuAPI
{
    /// <summary>
    /// Adds an entry to the <c>!admin</c> menu. Re-registering an id replaces the previous entry.
    /// </summary>
    /// <param name="id">Unique id of the entry, for example <c>myplugin.tools</c>.</param>
    /// <param name="permissions">The entry is shown when the player has any of these permissions. Empty shows it to every admin-menu user.</param>
    /// <param name="getTitle">Returns the entry label for the viewing player.</param>
    /// <param name="createMenu">Builds the submenu for the viewing player.</param>
    void RegisterSubmenu(string id, string[] permissions, Func<IPlayer, string> getTitle, Func<IPlayer, IMenu> createMenu);

    /// <summary>
    /// Removes a previously registered entry.
    /// </summary>
    void UnregisterSubmenu(string id);

    /// <summary>
    /// Builds the admin menu for a player without opening it.
    /// </summary>
    IMenu CreateAdminMenu(IPlayer player);

    /// <summary>
    /// Builds and opens the admin menu for a player.
    /// </summary>
    void OpenAdminMenu(IPlayer player);

    /// <summary>
    /// The players the viewer's admin menu lists: everyone except the server's TV and hidden admins the viewer may not see.
    /// </summary>
    IReadOnlyList<IPlayer> GetListedPlayers(IPlayer viewer);

    /// <summary>
    /// What the admin tools have set on a player: hidden, frozen, beacon and the like.
    /// </summary>
    AdminPlayerState GetPlayerState(IPlayer player);

    /// <summary>
    /// Players who left in the last few minutes, newest first, without anyone who has come back. Only humans are kept.
    /// </summary>
    IReadOnlyList<AdminRecentRow> GetRecentPlayers();

    /// <summary>
    /// True for a few seconds after a map change is ordered, while it is on its way.
    /// </summary>
    bool IsMapChangePending();
}
