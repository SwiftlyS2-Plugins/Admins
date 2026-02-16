namespace Admins.Comms.Contract;

public interface ICommsManager
{
    /// <summary>
    /// Finds all sanctions matching the specified criteria.
    /// </summary>
    /// <param name="steamId64">Optional SteamID64 to filter by.</param>
    /// <param name="playerIp">Optional IP address to filter by.</param>
    /// <param name="sanctionKind">Optional sanction kind to filter by.</param>
    /// <param name="sanctionType">Optional sanction type to filter by.</param>
    /// <param name="status">Status filter: Active for active sanctions, Expired for expired sanctions, All for all sanctions.</param>
    /// <returns>A list of sanctions matching the criteria.</returns>
    public List<ISanction> FindSanctions(long? steamId64 = null, string? playerIp = null, SanctionKind? sanctionKind = null, SanctionType? sanctionType = null, RecordStatus status = RecordStatus.All);

    /// <summary>
    /// Sets the list of communication sanctions.
    /// </summary>
    /// <param name="sanctions">The list of admin communication sanctions to set.</param>
    public void SetSanctions(List<ISanction> sanctions);

    /// <summary>
    /// Adds an admin communication sanction to the database.
    /// </summary>
    /// <param name="sanction">The admin communication sanction to add.</param>
    public void AddSanction(ISanction sanction);

    /// <summary>
    /// Updates an existing admin communication sanction in the database.
    /// </summary>
    /// <param name="sanction">The admin communication sanction to update.</param>
    public void UpdateSanction(ISanction sanction);

    /// <summary>
    /// Removes an admin communication sanction from the database.
    /// </summary>
    /// <param name="sanction">The admin communication sanction to remove.</param>
    public void RemoveSanction(ISanction sanction);

    /// <summary>
    /// Clears all admin communication sanctions from the database.
    /// </summary>
    public void ClearSanctions();

    /// <summary>
    /// Gets all admin communication sanctions from the database.
    /// </summary>
    event Action<ISanction>? OnAdminSanctionAdded;
    /// <summary>
    /// Event fired when an admin communication sanction is updated.
    /// </summary>
    event Action<ISanction>? OnAdminSanctionUpdated;
    /// <summary>
    /// Event fired when an admin communication sanction is removed.
    /// </summary>
    event Action<ISanction>? OnAdminSanctionRemoved;
}