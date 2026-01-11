using SwiftlyS2.Shared.Players;

namespace Admins.Core.Contract;

public interface IGroupsManager
{
    /// <summary>
    /// Sets the list of groups.
    /// </summary>
    /// <param name="groups">The list of groups to set.</param>
    public void SetGroups(List<IGroup> groups);
    /// <summary>
    /// Gets the group by its name.
    /// </summary>
    /// <param name="groupName">The name of the group to get.</param>
    /// <returns>The group associated with the given name.</returns>
    public IGroup? GetGroup(string groupName);
    /// <summary>
    /// Gets the groups an admin belongs to.
    /// </summary>
    /// <param name="admin">The admin to get groups for.</param>
    /// <returns>A list of groups the admin belongs to.</returns>
    public List<IGroup> GetAdminGroups(IAdmin admin);
    /// <summary>
    /// Gets the groups a player belongs to.
    /// </summary>
    /// <param name="player">The player to get groups for.</param>
    /// <returns>A list of groups the player belongs to.</returns>
    public List<IGroup> GetPlayerGroups(IPlayer player);
    /// <summary>
    /// Gets all groups from the database.
    /// </summary>
    /// <returns>A list of all groups.</returns>
    public List<IGroup> GetAllGroups();
    /// <summary>
    /// Refreshes the groups from the database. It will also reload admins.
    /// </summary>
    public void RefreshGroups();

    /// <summary>
    /// Gets a group by name from the database asynchronously.
    /// </summary>
    /// <param name="groupName">The group name to search for.</param>
    /// <returns>The group if found, otherwise null.</returns>
    public Task<IGroup?> GetGroupByNameAsync(string groupName);

    /// <summary>
    /// Updates an existing group in the database asynchronously.
    /// </summary>
    /// <param name="group">The group to update.</param>
    public Task UpdateGroupAsync(IGroup group);

    /// <summary>
    /// Adds a new group or updates an existing one in the database asynchronously.
    /// </summary>
    /// <param name="group">The group to add or update.</param>
    public Task AddOrUpdateGroupAsync(IGroup group);

    /// <summary>
    /// Removes a group from the database asynchronously.
    /// </summary>
    /// <param name="group">The group to remove.</param>
    public Task RemoveGroupAsync(IGroup group);
}