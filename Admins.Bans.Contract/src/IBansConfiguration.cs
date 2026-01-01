namespace Admins.Bans.Contract;

public interface IBansConfiguration
{
    /// <summary>
    /// The reasons for bans.
    /// </summary>
    public List<string> BansReasons { get; set; }
    /// <summary>
    /// The durations for bans in seconds.
    /// </summary>
    public List<int> BansDurationsInSeconds { get; set; }
}