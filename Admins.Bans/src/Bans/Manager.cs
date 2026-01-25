using System.Collections.Concurrent;
using Admins.Bans.Contract;
using Admins.Bans.Database.Models;
using Dommel;
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
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ban.CreatedAt = timestamp;
            ban.UpdatedAt = timestamp;

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                ban.Id = Convert.ToUInt64(await db.InsertAsync((Ban)ban));
            }

            ServerBans.AllBans.TryAdd(ban.Id, ban);
            OnAdminBanAdded?.Invoke(ban);
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

            ServerBans.AllBans.Clear();
        });
    }

    public IBan? FindActiveBan(ulong steamId64, string playerIp)
    {
        return _serverBans.FindActiveBan(steamId64, playerIp);
    }

    public List<IBan> GetBans()
    {
        return ServerBans.AllBans.Values.ToList();
    }

    public void RemoveBan(IBan ban)
    {
        Task.Run(async () =>
        {
            var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ban.ExpiresAt = currentTime;
            ban.UpdatedAt = currentTime;

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.UpdateAsync((Ban)ban);
            }

            ServerBans.AllBans.TryRemove(ban.Id, out _);
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

            ServerBans.AllBans.Clear();
            foreach (var ban in bans)
            {
                ServerBans.AllBans.TryAdd(ban.Id, ban);
            }
        });
    }

    public void UpdateBan(IBan ban)
    {
        Task.Run(async () =>
        {
            ban.UpdatedAt = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                await db.UpdateAsync((Ban)ban);
            }

            ServerBans.AllBans.AddOrUpdate(ban.Id, ban, (key, oldValue) => ban);
            OnAdminBanUpdated?.Invoke(ban);
        });
    }
}