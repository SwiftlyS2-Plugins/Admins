using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

/// <summary>
/// Shared interface key: <c>Admins.Comms.V2</c>. Events are raised on the game thread.
/// </summary>
public interface ICommsManager
{
    /// <summary>
    /// Finds sanctions for a SteamID64 and/or IP (a row matching either is returned), narrowed by kind, type and status.
    /// With neither given, every sanction is returned. Results are not limited to this server.
    /// </summary>
    Task<IReadOnlyList<ISanction>> FindSanctionsAsync(long? steamId64 = null, string? playerIp = null, SanctionKind? sanctionKind = null, SanctionType? sanctionType = null, RecordStatus status = RecordStatus.All);

    /// <summary>
    /// Gets the active sanction of a kind that applies to an online player on this server, if any.
    /// Reads the in-memory cache; call from the game thread.
    /// </summary>
    ISanction? GetActiveSanction(IPlayer player, SanctionKind kind);

    /// <summary>
    /// Stores a new sanction and applies it to every online player it matches. Returns the stored sanction.
    /// </summary>
    Task<ISanction> AddSanctionAsync(ISanction sanction);

    /// <summary>
    /// Updates an existing sanction row.
    /// </summary>
    Task UpdateSanctionAsync(ISanction sanction);

    /// <summary>
    /// Lifts a sanction: marks it <see cref="PunishmentStatus.Lifted"/> and ends it now. The row is kept as history.
    /// </summary>
    Task RemoveSanctionAsync(ISanction sanction);

    /// <summary>
    /// Counts the sanctions of a kind (null: both) that apply to this server, as the Comms page shows them. Zero without
    /// a database.
    /// </summary>
    Task<AdminRecordCounts> CountSanctionsAsync(SanctionKind? sanctionKind = null);

    event Action<ISanction>? OnAdminSanctionAdded;
    event Action<ISanction>? OnAdminSanctionUpdated;
    event Action<ISanction>? OnAdminSanctionRemoved;
}
