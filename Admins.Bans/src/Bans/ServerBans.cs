using Admins.Bans.Contract;
using Admins.Bans.Database.Models;
using Admins.Core.Contract;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Bans.Manager;

public class ServerBans
{
    private ISwiftlyCore Core = null!;
    private IConfigurationManager _configurationManager = null!;
    private IServerManager _serverManager = null!;

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

    public async Task<IBan?> FindActiveBanAsync(long steamId64, string playerIp)
    {
        if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == false)
            return null;

        try
        {
            var db = Core.Database.GetConnection("admins");
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var serverGuid = _serverManager.GetServerGUID();

            var steamIdBans = await db.SelectAsync<Ban>(b =>
                b.SteamId64 == steamId64 &&
                b.BanType == BanType.SteamID
            );

            var activeSteamBan = steamIdBans.FirstOrDefault(b =>
                (b.ExpiresAt == 0 || b.ExpiresAt > currentTime) &&
                (b.Server == serverGuid || b.GlobalBan)
            );

            if (activeSteamBan != null)
                return activeSteamBan;

            if (!string.IsNullOrEmpty(playerIp))
            {
                var ipBans = await db.SelectAsync<Ban>(b =>
                    b.PlayerIp == playerIp &&
                    b.BanType == BanType.IP
                );

                var activeIpBan = ipBans.FirstOrDefault(b =>
                    (b.ExpiresAt == 0 || b.ExpiresAt > currentTime) &&
                    (b.Server == serverGuid || b.GlobalBan)
                );

                if (activeIpBan != null)
                    return activeIpBan;
            }

            return null;
        }
        catch (Exception ex)
        {
            Core.Logger.LogError($"Error querying active ban for SteamID {steamId64}: {ex.Message}");
            return null;
        }
    }

    public IBan? FindActiveBan(long steamId64, string playerIp)
    {
        return FindActiveBanAsync(steamId64, playerIp).GetAwaiter().GetResult();
    }

    public TimeZoneInfo GetConfiguredTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(_configurationManager.GetCurrentConfiguration()!.TimeZone);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    public string FormatTimestampInTimeZone(long unixTimeMilliseconds)
    {
        var utcTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
        var timeZone = GetConfiguredTimeZone();
        var localTime = TimeZoneInfo.ConvertTime(utcTime, timeZone);
        return localTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void CheckPlayer(IPlayer player)
    {
        Task.Run(async () =>
        {
            var ban = await FindActiveBanAsync((long)player.SteamID, player.IPAddress);
            if (ban != null)
            {
                var localizer = Core.Translation.GetPlayerLocalizer(player);
                string kickMessage = localizer[
                    "ban.kick_message",
                    ban.Reason,
                    ban.ExpiresAt == 0 ? localizer["never"] : FormatTimestampInTimeZone((long)ban.ExpiresAt),
                    ban.AdminName,
                    ban.AdminSteamId64.ToString()
                ];

                Core.Scheduler.NextTick(() =>
                {
                    player.SendMessage(MessageType.Console, kickMessage);

                    player.KickAsync(kickMessage, ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
                });
            }
        });
    }

    public void CheckAllOnlinePlayers()
    {
        var players = Core.PlayerManager.GetAllPlayers();
        foreach (var player in players)
        {
            if (player.IsFakeClient || !player.IsValid)
                continue;

            CheckPlayer(player);
        }
    }
}
