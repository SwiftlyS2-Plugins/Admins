using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Shared;

/// <summary>
/// Identity of an online player captured on the game thread, safe to carry across awaits.
/// </summary>
public readonly record struct PlayerRef(int Slot, ulong SessionId, ulong SteamId, string Name, string Ip)
{
    public static PlayerRef Of(IPlayer player) =>
        new(player.Slot, player.SessionId, player.SteamID, Names.Of(player), Net.NormalizeIp(player.IPAddress));

    /// <summary>
    /// Returns the live player if the same connection is still on the server. Game thread only.
    /// </summary>
    public IPlayer? Resolve(ISwiftlyCore core) =>
        core.PlayerManager.IsSessionIdValid(SessionId) ? core.PlayerManager.GetPlayerFromSessionId(SessionId) : null;
}

/// <summary>
/// Lets the ban and comms commands open the admin menu without depending on it (the menu depends on them).
/// </summary>
public sealed class MenuShortcuts
{
    public Action<IPlayer, Contract.BanType, bool>? OpenBan { get; set; }

    public Action<IPlayer, Services.Comms.CommsAction, Contract.SanctionType, bool>? OpenComms { get; set; }

    public Action<IPlayer>? OpenKick { get; set; }
}
