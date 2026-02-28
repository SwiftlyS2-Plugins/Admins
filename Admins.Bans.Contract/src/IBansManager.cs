namespace Admins.Bans.Contract;

public interface IBansManager
{
    /// <summary>
    /// Finds all bans matching the specified criteria.
    /// </summary>
    /// <param name="steamId64">Optional SteamID64 to filter by.</param>
    /// <param name="playerIp">Optional IP address to filter by.</param>
    /// <param name="banType">Optional ban type to filter by.</param>
    /// <param name="status">Status filter: Active for active bans, Expired for expired bans, All for all bans.</param>
    /// <returns>A list of bans matching the criteria.</returns>
    public List<IBan> FindBans(long? steamId64 = null, string? playerIp = null, BanType? banType = null, RecordStatus status = RecordStatus.All);

    /// <summary>
    /// Sets the list of admin bans.
    /// </summary>
    /// <param name="bans">The list of admin bans to set.</param>
    public void SetBans(List<IBan> bans);

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