using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

public interface IAdminAPIv1
{
    /// <summary>
    /// Gets the admin associated with the given player ID.
    /// </summary>
    /// <param name="playerid">The player ID to get the admin for.</param>
    /// <returns>The admin associated with the given player ID.</returns>
    public IAdmin? GetAdmin(int playerid);
    /// <summary>
    /// Gets the admin associated with the given player.
    /// </summary>
    /// <param name="player">The player to get the admin for.</param>
    /// <returns>The admin associated with the given player.</returns>
    public IAdmin? GetAdmin(IPlayer player);
    /// <summary>
    /// Gets the admin associated with the given SteamID64.
    /// </summary>
    /// <param name="steamId64">The SteamID64 to get the admin for.</param>
    /// <returns>The admin associated with the given SteamID64.</returns>
    public IAdmin? GetAdmin(ulong steamId64);
    /// <summary>
    /// Gets all admins from the database.
    /// </summary>
    /// <returns>A list of all admins.</returns>
    public List<IAdmin> GetAllAdmins();
    /// <summary>
    /// Adds a new admin to the database.
    /// </summary>
    /// <param name="steamId64">The SteamID64 of the admin.</param>
    /// <param name="adminName">The name of the admin.</param>
    /// <param name="groups">The groups the admin belongs to.</param>
    /// <param name="permissions">The permissions the admin has.</param>
    /// <returns>The newly added admin.</returns>
    public IAdmin? AddAdmin(ulong steamId64, string adminName, List<IGroup> groups, List<string> permissions);
    /// <summary>
    /// Removes an admin from the database.
    /// </summary>
    /// <param name="admin">The admin to remove.</param>
    public void RemoveAdmin(IAdmin admin);

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
    /// Refreshes the admins from the database.
    /// </summary>
    public void RefreshAdmins();
    /// <summary>
    /// Refreshes the groups from the database. It will also reload admins.
    /// </summary>
    public void RefreshGroups();

    /// <summary>
    /// Event fired when an admin is loaded.
    /// </summary>
    event Action<IPlayer, IAdmin>? OnAdminLoad;

    /// <summary>
    /// Gets the Admin Bans API.
    /// </summary>
    public IAdminBansAPIv1 AdminBansAPI { get; }
    /// <summary>
    /// Gets the Admin Sanctions API.
    /// </summary>
    public IAdminSanctionsAPIv1 AdminSanctionsAPI { get; }

    /// <summary>
    /// Gets the Admin Menu API.
    /// </summary>
    public IAdminMenuAPIv1 AdminMenuAPI { get; }
}