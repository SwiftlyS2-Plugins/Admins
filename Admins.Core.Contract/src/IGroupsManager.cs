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
}