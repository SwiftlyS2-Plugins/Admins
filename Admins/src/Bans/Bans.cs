using Admins.Contract;
using Admins.Database.Models;
using Dommel;
using SwiftlyS2.Shared;

namespace Admins.Bans;

public partial class ServerBans
{
    [SwiftlyInject]
    private static ISwiftlyCore Core = null!;

    public static List<IBan> Bans { get; set; } = [];

    public void Load()
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

            Admins.AdminAPI.TriggerBanAdded(ban);
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
        });
    }

    public static IBan? FindActiveBan(ulong steamId64, string playerIp)
    {
        var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Bans.Find(ban =>
            ((ban.SteamId64 == steamId64 && ban.BanType == (int)BanType.SteamID) || (!string.IsNullOrEmpty(playerIp) && ban.PlayerIp == playerIp && ban.BanType == (int)BanType.IP)) &&
            (ban.ExpiresAt == 0 || ban.ExpiresAt > currentTime) &&
            (ban.Server == Admins.ServerGUID || ban.GlobalBan)
        );
    }
}