namespace Admins.Comms.Contract;

/// <summary>
/// Status filter for finding bans and sanctions.
/// </summary>
public enum RecordStatus
{
    /// <summary>
    /// Only active (not expired) records.
    /// </summary>
    Active,

    /// <summary>
    /// Only expired (inactive) records.
    /// </summary>
    Expired,

    /// <summary>
    /// All records regardless of status.
    /// </summary>
    All
}
