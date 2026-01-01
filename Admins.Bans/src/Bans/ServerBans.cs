using System.Collections.Concurrent;
using Admins.Bans.Contract;
using Admins.Bans.Database.Models;
using Admins.Core.Contract;
using Dommel;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Bans.Manager;

public class ServerBans
{
    private ISwiftlyCore Core = null!;
    private IConfigurationManager _configurationManager = null!;
    private IServerManager _serverManager = null!;

    public static ConcurrentDictionary<ulong, IBan> AllBans { get; set; } = [];

    public ServerBans(ISwiftlyCore core)
    {
        Core = core;
    }

    public void SetServerManager(IServerManager serverManager)
    {
        _serverManager = serverManager;
    }

    public void SetConfigurationManager(IConfigurationManager configurationManager)
    {
        _configurationManager = configurationManager;
    }

    public void Load()
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                var bans = await db.GetAllAsync<Ban>();
                AllBans = new ConcurrentDictionary<ulong, IBan>(bans.ToDictionary(b => b.Id, b => (IBan)b));
            }
        });
    }

    public IBan? FindActiveBan(ulong steamId64, string playerIp)
    {
        var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return AllBans.Values.FirstOrDefault(ban =>
            ((ban.SteamId64 == steamId64 && ban.BanType == BanType.SteamID) || (!string.IsNullOrEmpty(playerIp) && ban.PlayerIp == playerIp && ban.BanType == BanType.IP)) &&
            (ban.ExpiresAt == 0 || ban.ExpiresAt > currentTime) &&
            (ban.Server == _serverManager.GetServerGUID() || ban.GlobalBan)
        );
    }

    public void CheckPlayer(IPlayer player)
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

            Core.Scheduler.NextTick(() =>
            {
                player.Kick(kickMessage, ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            });
        }
    }
}