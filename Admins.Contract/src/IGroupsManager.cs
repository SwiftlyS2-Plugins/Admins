using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

/// <summary>
/// Shared interface key: <c>Admins.Groups.V2</c>.
/// </summary>
public interface IGroupsManager
{
    /// <summary>
    /// Gets a cached group by name (case-insensitive).
    /// </summary>
    IGroup? GetGroup(string groupName);

    /// <summary>
    /// Gets every group known to the plugin, including groups of other servers.
    /// </summary>
    IReadOnlyList<IGroup> GetAllGroups();

    /// <summary>
    /// Gets the groups of an admin that apply to this server.
    /// </summary>
    IReadOnlyList<IGroup> GetAdminGroups(IAdmin admin);

    /// <summary>
    /// Gets the groups of an online player that apply to this server.
    /// </summary>
    IReadOnlyList<IGroup> GetPlayerGroups(IPlayer player);

    /// <summary>
    /// Reads a group directly from the database, bypassing the cache.
    /// </summary>
    Task<IGroup?> GetGroupFromDatabaseAsync(string groupName);

    /// <summary>
    /// Inserts the group, or updates the existing row with the same name, then refreshes.
    /// </summary>
    Task AddOrUpdateGroupAsync(IGroup group);

    /// <summary>
    /// Deletes the group row with the group's name, then refreshes.
    /// </summary>
    Task RemoveGroupAsync(IGroup group);

    /// <summary>
    /// Replaces the cached group list. Intended for <c>UseDatabase = false</c> setups.
    /// </summary>
    void SetGroups(IEnumerable<IGroup> groups);

    /// <summary>
    /// Reloads admins and groups from the database and re-applies permissions to online players.
    /// </summary>
    Task RefreshAsync();
}
