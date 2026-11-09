namespace Admins.Contract;

/// <summary>
/// A ban record. Timestamps are Unix milliseconds; <see cref="ExpiresAt"/> 0 means permanent.
/// </summary>
public interface IBan
{
    long Id { get; set; }
    long SteamId64 { get; set; }
    string PlayerName { get; set; }
    string PlayerIp { get; set; }
    BanType BanType { get; set; }
    long ExpiresAt { get; set; }
    long Length { get; set; }
    string Reason { get; set; }
    long AdminSteamId64 { get; set; }
    string AdminName { get; set; }
    string Server { get; set; }
    bool GlobalBan { get; set; }
    long CreatedAt { get; set; }
    long UpdatedAt { get; set; }

    /// <summary>
    /// Whether it is in force, was lifted by an admin or ran out. In force is decided by <see cref="ExpiresAt"/> alone:
    /// lifting a ban also ends it now. An implementation of your own reports only Active or Expired.
    /// </summary>
    PunishmentStatus Status =>
        ExpiresAt == 0 || ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ? PunishmentStatus.Active : PunishmentStatus.Expired;
}
