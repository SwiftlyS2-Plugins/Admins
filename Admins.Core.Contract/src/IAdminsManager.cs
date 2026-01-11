using SwiftlyS2.Shared.Players;

namespace Admins.Core.Contract;

public interface IAdminsManager
{
    /// <summary>
    /// Sets the list of admins.
    /// </summary>
    /// <param name="admins">The list of admins to set.</param>
    public void SetAdmins(List<IAdmin> admins);
    /// <summary>
    /// Assigns an admin to a player.
    /// </summary>
    /// <param name="player">The player to assign the admin to.</param>
    /// <param name="admin">The admin to assign to the player.</param>
    public void AssignAdmin(IPlayer player, IAdmin admin);
    /// <summary>
    /// Unassigns an admin from a player.
    /// </summary>
    /// <param name="player">The player to unassign the admin from.</param>
    /// <param name="admin">The admin to unassign from the player.</param>
    public void UnassignAdmin(IPlayer player, IAdmin admin);
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
    /// Refreshes the admins from the database.
    /// </summary>
    public void RefreshAdmins();

    /// <summary>
    /// Event fired when an admin is loaded.
    /// </summary>
    event Action<IPlayer, IAdmin>? OnAdminLoad;

    /// <summary>
    /// Gets an admin by SteamID64 from the database asynchronously.
    /// </summary>
    /// <param name="steamId64">The SteamID64 to search for.</param>
    /// <returns>The admin if found, otherwise null.</returns>
    public Task<IAdmin?> GetAdminBySteamId64Async(ulong steamId64);

    /// <summary>
    /// Updates an existing admin in the database asynchronously.
    /// </summary>
    /// <param name="admin">The admin to update.</param>
    public Task UpdateAdminAsync(IAdmin admin);

    /// <summary>
    /// Adds a new admin or updates an existing one in the database asynchronously.
    /// </summary>
    /// <param name="admin">The admin to add or update.</param>
    public Task AddOrUpdateAdminAsync(IAdmin admin);
}