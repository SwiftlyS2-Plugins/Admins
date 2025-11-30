using Admins.Contract;
using Admins.Database.Models;
using Dommel;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Bans;

public partial class ServerBans
{
    [SwiftlyInject]
    private static ISwiftlyCore Core = null!;

    public static List<IBan> Bans { get; set; } = [];

    public static void Load()
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            Bans = [.. database.GetAll<Ban>()];
        });
    }

    public static void AddBan(IBan ban)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            var id = database.Insert((Ban)ban);
            ban.Id = (ulong)id;
            Bans.Add(ban);
        });
    }

    public static void RemoveBan(IBan ban)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            database.Delete((Ban)ban);
            Bans.Remove(ban);
        });
    }

    public static void UpdateBan(IBan ban)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            database.Update((Ban)ban);
            Bans.RemoveAt(Bans.FindIndex(b => b.Id == ban.Id));
            Bans.Add(ban);
        });
    }

    public static IBan? FindActiveBan(ulong steamId64, string playerIp)
    {
        var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Bans.Find(ban =>
            ((ban.SteamId64 == steamId64 && ban.BanType == BanType.SteamID) || (!string.IsNullOrEmpty(playerIp) && ban.PlayerIp == playerIp && ban.BanType == BanType.IP)) &&
            (ban.ExpiresAt == 0 || ban.ExpiresAt > currentTime) &&
            (ban.Server == Admins.ServerGUID || ban.GlobalBan)
        );
    }

    public static bool CheckPlayer(IPlayer player)
    {
        var ban = FindActiveBan(player.SteamID, player.IPAddress);
        if (ban != null)
        {
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            string kickMessage = localizer[
                "ban.kick_message",
                ban.Reason,
                ban.ExpiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds((long)ban.ExpiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                ban.AdminName,
                ban.AdminSteamId64.ToString()
            ];
            player.SendMessage(MessageType.Console, kickMessage);
            player.Kick(kickMessage, SwiftlyS2.Shared.ProtobufDefinitions.ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            return false;
        }

        return true;
    }
}