using System.Collections.Concurrent;
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
    private ulong _lastSyncTimestamp = 0;

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

                // Set last sync timestamp to current time
                _lastSyncTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        });
    }

    public async Task SyncBansFromDatabase()
    {
        if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == false)
            return;

        try
        {
            var db = Core.Database.GetConnection("admins");

            // Query bans updated since last sync
            var newBans = await db.SelectAsync<Ban>(b => b.UpdatedAt > _lastSyncTimestamp);

            if (newBans.Any())
            {
                Core.Logger.LogInformation($"[Bans Sync] Found {newBans.Count()} new/updated bans from database");

                var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var ban in newBans)
                {
                    // If ban has expired, remove it from local cache
                    if (ban.ExpiresAt != 0 && ban.ExpiresAt <= currentTime)
                    {
                        AllBans.TryRemove(ban.Id, out _);
                    }
                    else
                    {
                        // Add or update ban in local cache
                        AllBans.AddOrUpdate(ban.Id, (IBan)ban, (key, oldValue) => (IBan)ban);
                    }
                }

                // Update last sync timestamp to the latest UpdatedAt value
                var maxUpdatedAt = newBans.Max(b => b.UpdatedAt);
                _lastSyncTimestamp = Math.Max(_lastSyncTimestamp, maxUpdatedAt);

                // Check all connected players against new/updated bans
                await CheckAllPlayersAgainstNewBans(newBans);
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError($"[Bans Sync] Error syncing bans from database: {ex.Message}");
        }
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
        var ban = FindActiveBan(player.SteamID, player.IPAddress);
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
            player.SendMessage(MessageType.Console, kickMessage);

            Core.Scheduler.NextTick(() =>
            {
                player.Kick(kickMessage, ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            });
        }
    }
}