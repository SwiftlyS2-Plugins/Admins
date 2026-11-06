namespace Admins.Contract;

/// <summary>
/// Defines how immunity is compared when an admin targets another player.
/// A player without an admin record has immunity 0.
/// </summary>
public enum ImmunityMode
{
    /// <summary>
    /// Immunity is ignored: anyone can target anyone.
    /// </summary>
    IgnoreImmunity = 0,

    /// <summary>
    /// The target is protected from admins with lower immunity.
    /// An admin can target players with lower or equal immunity.
    /// </summary>
    ProtectFromLowerAccess = 1,

    /// <summary>
    /// The target is protected from admins with lower or equal immunity.
    /// An admin can only target players with strictly lower immunity.
    /// </summary>
    ProtectFromEqualOrLowerAccess = 2,

    /// <summary>
    /// Same as <see cref="ProtectFromEqualOrLowerAccess"/>, except that two players
    /// who both have immunity 0 can target each other.
    /// </summary>
    ProtectWithNoImmunityBypass = 3,
}

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

public enum BanType
{
    SteamID = 1,
    IP = 2
}

public enum SanctionType
{
    SteamID = 1,
    IP = 2
}

public enum SanctionKind
{
    /// <summary>
    /// Blocks text chat.
    /// </summary>
    Gag = 1,

    /// <summary>
    /// Blocks voice chat.
    /// </summary>
    Mute = 2
}
