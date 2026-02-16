using Admins.Comms.Contract;
using Admins.Comms.Database.Models;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Admins.Comms.Manager;

public class CommsManager : ICommsManager
{
    private ISwiftlyCore Core = null!;
    private ServerComms _serverComms = null!;
    private Core.Contract.IConfigurationManager _configurationManager = null!;

    public event Action<ISanction>? OnAdminSanctionAdded;
    public event Action<ISanction>? OnAdminSanctionUpdated;
    public event Action<ISanction>? OnAdminSanctionRemoved;

    public CommsManager(ISwiftlyCore core, ServerComms serverComms)
    {
        Core = core;
        _serverComms = serverComms;
    }

    public void SetConfigurationManager(Core.Contract.IConfigurationManager configurationManager)
    {
        _configurationManager = configurationManager;
    }

    public void AddSanction(ISanction sanction)
    {
        Task.Run(async () =>
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            sanction.CreatedAt = timestamp;
            sanction.UpdatedAt = timestamp;

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                sanction.Id = Convert.ToInt64(await db.InsertAsync((Sanction)sanction));
            }

            // Add to online cache if the target player is online
            var players = Core.PlayerManager.GetAllPlayers();
            foreach (var player in players)
            {
                if (player.IsFakeClient || !player.IsValid)
                    continue;

                if ((long)player.SteamID == sanction.SteamId64 || (!string.IsNullOrEmpty(sanction.PlayerIp) && player.IPAddress == sanction.PlayerIp))
                {
                    _serverComms.AddToOnlineCache(player.SteamID, sanction);
                }
            }

            OnAdminSanctionAdded?.Invoke(sanction);
        });
    }

    public void ClearSanctions()
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAllAsync<Sanction>();
            }

            ServerComms.OnlinePlayerSanctions.Clear();
        });
    }

    public List<ISanction> FindSanctions(long? steamId64 = null, string? playerIp = null, SanctionKind? sanctionKind = null, SanctionType? sanctionType = null, RecordStatus status = RecordStatus.All)
    {
        try
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var hasPlayerIp = !string.IsNullOrEmpty(playerIp);

                IEnumerable<Sanction> sanctions;

                // Query DB with simple filters only (player filters)
                if (steamId64 == null && !hasPlayerIp)
                {
                    // No player filter - get all sanctions
                    sanctions = db.GetAllAsync<Sanction>().GetAwaiter().GetResult();
                }
                else if (steamId64 != null && !hasPlayerIp)
                {
                    // SteamID only
                    sanctions = db.SelectAsync<Sanction>(s => s.SteamId64 == steamId64).GetAwaiter().GetResult();
                }
                else if (steamId64 == null && hasPlayerIp)
                {
                    // IP only
                    sanctions = db.SelectAsync<Sanction>(s => s.PlayerIp == playerIp).GetAwaiter().GetResult();
                }
                else
                {
                    // Both SteamID and IP (OR logic)
                    sanctions = db.SelectAsync<Sanction>(s => s.SteamId64 == steamId64 || s.PlayerIp == playerIp).GetAwaiter().GetResult();
                }

                // Apply sanctionKind, sanctionType, and status filters in-memory
                var filtered = sanctions.Where(s =>
                    (sanctionKind == null || s.SanctionKind == sanctionKind) &&
                    (sanctionType == null || s.SanctionType == sanctionType) &&
                    (status == RecordStatus.All ||
                     (status == RecordStatus.Active ? (s.ExpiresAt == 0 || s.ExpiresAt > currentTime) :
                      (s.ExpiresAt != 0 && s.ExpiresAt <= currentTime)))
                );

                return [.. filtered.Select(s => (ISanction)s)];
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError($"[Comms] Error fetching sanctions from database: {ex.Message}");
        }

        return [];
    }

    public void RemoveSanction(ISanction sanction)
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAsync((Sanction)sanction);
            }

            // Remove from online cache for all online players who might match
            var players = Core.PlayerManager.GetAllPlayers();
            foreach (var player in players)
            {
                if (player.IsFakeClient || !player.IsValid)
                    continue;

                if ((long)player.SteamID == sanction.SteamId64 || (!string.IsNullOrEmpty(sanction.PlayerIp) && player.IPAddress == sanction.PlayerIp))
                {
                    _serverComms.RemoveFromOnlineCache(player.SteamID, sanction.Id);
                }
            }

            OnAdminSanctionRemoved?.Invoke(sanction);
        });
    }

    public void SetSanctions(List<ISanction> sanctions)
    {
        Task.Run(async () =>
        {
            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.DeleteAllAsync<Sanction>();
                await db.InsertAsync(sanctions.Select(s => (Sanction)s).ToList());
            }

            await _serverComms.RefreshOnlinePlayerSanctionsAsync();
        });
    }

    public void UpdateSanction(ISanction sanction)
    {
        Task.Run(async () =>
        {
            sanction.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.UpdateAsync((Sanction)sanction);
            }

            // Refresh the affected player's cache
            var players = Core.PlayerManager.GetAllPlayers();
            foreach (var player in players)
            {
                if (player.IsFakeClient || !player.IsValid)
                    continue;

                if ((long)player.SteamID == sanction.SteamId64 || (!string.IsNullOrEmpty(sanction.PlayerIp) && player.IPAddress == sanction.PlayerIp))
                {
                    await _serverComms.LoadPlayerSanctionsAsync(player.SteamID, player.IPAddress);
                }
            }

            OnAdminSanctionUpdated?.Invoke(sanction);
        });
    }
}
