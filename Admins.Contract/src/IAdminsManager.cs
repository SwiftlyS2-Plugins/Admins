using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

/// <summary>
/// Shared interface key: <c>Admins.Admins.V2</c>.
/// </summary>
public interface IAdminsManager
{
    /// <summary>
    /// Gets the admin record that applies to this server for a SteamID64, whether the player is online or not.
    /// </summary>
    IAdmin? GetAdmin(ulong steamId64);

    /// <summary>
    /// Gets the admin record that applies to this server for an online player.
    /// </summary>
    IAdmin? GetAdmin(IPlayer player);

    /// <summary>
    /// Gets the admin record that applies to this server for a player id.
    /// </summary>
    IAdmin? GetAdmin(int playerId);

    /// <summary>
    /// Gets every admin known to the plugin, including admins of other servers.
    /// </summary>
    IReadOnlyList<IAdmin> GetAllAdmins();

    /// <summary>
    /// Gets the effective immunity of a SteamID64 on this server: the highest of the admin's own
    /// immunity and the immunity of their groups. 0 for non-admins.
    /// </summary>
    int GetImmunity(ulong steamId64);

    /// <summary>
    /// Whether <paramref name="admin"/> may act on <paramref name="targetSteamId64"/> under the configured
    /// <see cref="ImmunityMode"/>. A null admin is the server console and may always act.
    /// </summary>
    bool CanTarget(IPlayer? admin, ulong targetSteamId64);

    /// <summary>
    /// Reloads admins and groups from the database and re-applies permissions to online players.
    /// </summary>
    Task RefreshAsync();

    /// <summary>
    /// Reads an admin directly from the database, bypassing the cache.
    /// </summary>
    Task<IAdmin?> GetAdminFromDatabaseAsync(ulong steamId64);

    /// <summary>
    /// Inserts the admin, or updates the existing row with the same SteamID64, then refreshes.
    /// </summary>
    Task AddOrUpdateAdminAsync(IAdmin admin);

    /// <summary>
    /// Deletes the admin row with the admin's SteamID64, then refreshes.
    /// </summary>
    Task RemoveAdminAsync(IAdmin admin);

    /// <summary>
    /// Replaces the cached admin list. Intended for <c>UseDatabase = false</c> setups that supply admins
    /// from another source; with a database the next refresh overwrites it.
    /// </summary>
    void SetAdmins(IEnumerable<IAdmin> admins);

    /// <summary>
    /// Raised on the game thread when an admin's permissions are applied to a player, either because they
    /// joined or because their record changed.
    /// </summary>
    event Action<IPlayer, IAdmin>? OnAdminLoad;
}
