namespace Admins.Bans.Contract;

public interface IBansManager
{
    /// <summary>
    /// Sets the list of admin bans.
    /// </summary>
    /// <param name="bans">The list of admin bans to set.</param>
    public void SetBans(List<IBan> bans);

    /// <summary>
    /// Gets all admin bans.
    /// </summary>
    /// <returns>A list of all admin bans.</returns>
    public List<IBan> GetBans();

    /// <summary>
    /// Adds an admin ban to the database.
    /// </summary>
    /// <param name="ban">The admin ban to add.</param>
    public void AddBan(IBan ban);

    /// <summary>
    /// Updates an existing admin ban in the database.
    /// </summary>
    /// <param name="ban">The admin ban to update.</param>
    public void UpdateBan(IBan ban);

    /// <summary>
    /// Removes an admin ban from the database.
    /// </summary>
    /// <param name="ban">The admin ban to remove.</param>
    public void RemoveBan(IBan ban);

    /// <summary>
    /// Clears all admin bans from the database.
    /// </summary>
    public void ClearBans();

    /// <summary>
    /// Finds an active ban by SteamID64 or player IP.
    /// </summary>
    /// <param name="steamId64">The SteamID64 of the player.</param>
    /// <param name="playerIp">The IP address of the player.</param>
    /// <returns>The active admin ban if found; otherwise, null.</returns>
    public IBan? FindActiveBan(ulong steamId64, string playerIp);

    /// <summary>
    /// Gets all admin bans from the database.
    /// </summary>
    event Action<IBan>? OnAdminBanAdded;
    /// <summary>
    /// Event fired when an admin ban is updated.
    /// </summary>
    event Action<IBan>? OnAdminBanUpdated;
    /// <summary>
    /// Event fired when an admin ban is removed.
    /// </summary>
    event Action<IBan>? OnAdminBanRemoved;
}