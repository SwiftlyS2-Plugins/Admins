namespace Admins.Contract;

/// <summary>
/// Shared interface key: <c>Admins.Bans.V2</c>. Events are raised on the game thread.
/// </summary>
public interface IBansManager
{
    /// <summary>
    /// Finds bans for a SteamID64 and/or IP (a row matching either is returned), narrowed by type and status.
    /// With neither given, every ban is returned. Results are not limited to this server.
    /// </summary>
    Task<IReadOnlyList<IBan>> FindBansAsync(long? steamId64 = null, string? playerIp = null, BanType? banType = null, RecordStatus status = RecordStatus.All);

    /// <summary>
    /// Gets the active ban that applies to this server (local or global) for a SteamID64 or IP, if any.
    /// </summary>
    Task<IBan?> FindActiveBanAsync(long steamId64, string? playerIp);

    /// <summary>
    /// Stores a new ban and kicks every online player it matches. Returns the stored ban.
    /// </summary>
    Task<IBan> AddBanAsync(IBan ban);

    /// <summary>
    /// Updates an existing ban row.
    /// </summary>
    Task UpdateBanAsync(IBan ban);

    /// <summary>
    /// Lifts a ban: marks it <see cref="PunishmentStatus.Lifted"/> and ends it now. The row is kept as history.
    /// </summary>
    Task RemoveBanAsync(IBan ban);

    /// <summary>
    /// Counts the bans that apply to this server, as the Bans page shows them. Zero without a database.
    /// </summary>
    Task<AdminRecordCounts> CountBansAsync();

    event Action<IBan>? OnAdminBanAdded;
    event Action<IBan>? OnAdminBanUpdated;
    event Action<IBan>? OnAdminBanRemoved;
}
