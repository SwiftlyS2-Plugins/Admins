using Admins.Bans.Contract;
using Admins.Bans.Database.Models;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Admins.Bans.Manager;

public class BansManager : IBansManager
{
    private ISwiftlyCore Core = null!;
    private ServerBans _serverBans = null!;
    private Core.Contract.IConfigurationManager _configurationManager = null!;

    public event Action<IBan>? OnAdminBanAdded;
    public event Action<IBan>? OnAdminBanUpdated;
    public event Action<IBan>? OnAdminBanRemoved;

    public BansManager(ISwiftlyCore core, ServerBans serverBans)
    {
        Core = core;
        _serverBans = serverBans;
    }

    public void SetConfigurationManager(Core.Contract.IConfigurationManager configurationManager)
    {
        _configurationManager = configurationManager;
    }

    public void AddBan(IBan ban)
    {
        Task.Run(async () =>
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ban.CreatedAt = timestamp;
            ban.UpdatedAt = timestamp;

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                ban.Id = Convert.ToInt64(await db.InsertAsync((Ban)ban));
            }

            OnAdminBanAdded?.Invoke(ban);

            var players = Core.PlayerManager.GetAllPlayers();
            foreach (var player in players)
            {
                if (player.IsFakeClient || !player.IsValid)
                    continue;

                if ((long)player.SteamID == ban.SteamId64 || (!string.IsNullOrEmpty(ban.PlayerIp) && player.IPAddress == ban.PlayerIp))
                {
                    _serverBans.CheckPlayer(player);
                }
            }
        });
    }

    public void ClearBans()
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAllAsync<Ban>();
            }
        });
    }

    public List<IBan> FindBans(long? steamId64 = null, string? playerIp = null, BanType? banType = null, RecordStatus status = RecordStatus.All)
    {
        try
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var hasPlayerIp = !string.IsNullOrEmpty(playerIp);

                IEnumerable<Ban> bans;

                // Query DB with simple filters only (player filters)
                if (steamId64 == null && !hasPlayerIp)
                {
                    // No player filter - get all bans
                    bans = db.GetAllAsync<Ban>().GetAwaiter().GetResult();
                }
                else if (steamId64 != null && !hasPlayerIp)
                {
                    // SteamID only
                    bans = db.SelectAsync<Ban>(b => b.SteamId64 == steamId64).GetAwaiter().GetResult();
                }
                else if (steamId64 == null && hasPlayerIp)
                {
                    // IP only
                    bans = db.SelectAsync<Ban>(b => b.PlayerIp == playerIp).GetAwaiter().GetResult();
                }
                else
                {
                    // Both SteamID and IP (OR logic)
                    bans = db.SelectAsync<Ban>(b => b.SteamId64 == steamId64 || b.PlayerIp == playerIp).GetAwaiter().GetResult();
                }

                // Apply banType and status filters in-memory
                var filtered = bans.Where(b =>
                    (banType == null || b.BanType == banType) &&
                    (status == RecordStatus.All ||
                     (status == RecordStatus.Active ? (b.ExpiresAt == 0 || b.ExpiresAt > currentTime) :
                      (b.ExpiresAt != 0 && b.ExpiresAt <= currentTime)))
                );

                return [.. filtered.Select(b => (IBan)b)];
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError($"[Bans] Error fetching bans from database: {ex.Message}");
        }

        return [];
    }

    public void RemoveBan(IBan ban)
    {
        Task.Run(async () =>
        {
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ban.ExpiresAt = currentTime;
            ban.UpdatedAt = currentTime;

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.UpdateAsync((Ban)ban);
            }

            OnAdminBanRemoved?.Invoke(ban);
        });
    }

    public void SetBans(List<IBan> bans)
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAllAsync<Ban>();
                await db.InsertAsync(bans.Select(b => (Ban)b).ToList());
            }
        });
    }

    public void UpdateBan(IBan ban)
    {
        Task.Run(async () =>
        {
            ban.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.UpdateAsync((Ban)ban);
            }

            OnAdminBanUpdated?.Invoke(ban);
        });
    }
}
