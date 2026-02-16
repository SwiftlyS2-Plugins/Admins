namespace Admins.Core.Contract;

public interface ICoreConfiguration
{
    /// <summary>
    /// The prefix used in admin messages.
    /// </summary>
    public string Prefix { get; set; }
    /// <summary>
    /// Indicates whether to use the database to load/store data.
    /// </summary>
    public bool UseDatabase { get; set; }
    /// <summary>
    /// The time zone used for logging and timestamps.
    /// </summary>
    public string TimeZone { get; set; }
    /// <summary>
    /// The interval in seconds to sync admins from the database. Set to 0 to disable automatic sync.
    /// </summary>
    public float AdminsDatabaseSyncIntervalSeconds { get; set; }
    /// <summary>
    /// The interval in seconds to sync bans from the database. Set to 0 to disable automatic sync.
    /// </summary>
    public float BansDatabaseSyncIntervalSeconds { get; set; }
    /// <summary>
    /// The interval in seconds to sync sanctions from the database. Set to 0 to disable automatic sync.
    /// </summary>
    public float SanctionsDatabaseSyncIntervalSeconds { get; set; }
}