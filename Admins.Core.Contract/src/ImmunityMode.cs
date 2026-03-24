namespace Admins.Core.Contract;

/// <summary>
/// Defines how immunity checks are performed between admins.
/// </summary>
public enum ImmunityMode
{
    /// <summary>
    /// Mode 0: Ignore immunity levels (except for specific group immunities).
    /// Any admin can affect any other admin.
    /// </summary>
    IgnoreImmunity = 0,

    /// <summary>
    /// Mode 1: Protect from admins of lower access only.
    /// Admins can only affect admins with lower or equal immunity.
    /// </summary>
    ProtectFromLowerAccess = 1,

    /// <summary>
    /// Mode 2: Protect from admins of equal to or lower access.
    /// Admins can only affect admins with strictly lower immunity.
    /// </summary>
    ProtectFromEqualOrLowerAccess = 2,

    /// <summary>
    /// Mode 3: Same as 2, except admins with no immunity can affect each other.
    /// Admins with immunity protect from equal-or-lower access, but admins with 0 immunity bypass this.
    /// </summary>
    ProtectWithNoImmunityBypass = 3,
}
