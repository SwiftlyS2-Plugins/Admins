using System.Collections.Concurrent;
using Admins.Core.Config;
using Admins.Core.Contract;
using Admins.Core.Database.Models;
using Admins.Core.Server;
using Dommel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Core.Admins;

public class AdminsManager : IAdminsManager
{
    private ISwiftlyCore _core;
    private ServerAdmins? _serverAdmins;
    private IOptionsMonitor<CoreConfiguration>? _confMonitor;

    public event Action<IPlayer, IAdmin>? OnAdminLoad;

    public AdminsManager(ISwiftlyCore core, IOptionsMonitor<CoreConfiguration> confMonitor)
    {
        _core = core;
        _confMonitor = confMonitor;
    }

    public void SetServerAdmins(ServerAdmins serverAdmins)
    {
        _serverAdmins = serverAdmins;
    }

    public void StartSyncTimer()
    {
        if (_confMonitor == null)
        {
            return;
        }

        var intervalSeconds = _confMonitor.CurrentValue.AdminsDatabaseSyncIntervalSeconds;

        if (intervalSeconds > 0 && _confMonitor.CurrentValue.UseDatabase)
        {
            _core.Logger.LogInformation("Starting admins database sync timer with interval of {IntervalSeconds} seconds", intervalSeconds);

            _core.Scheduler.RepeatBySeconds(intervalSeconds, () =>
            {
                Task.Run(() =>
                {
                    RefreshAdmins();
                });
            });
        }
        else
        {
            _core.Logger.LogInformation("Admins database sync timer is disabled. Set AdminsDatabaseSyncIntervalSeconds to a value greater than 0 to enable it.");
        }
    }

    public IAdmin? AddAdmin(ulong steamId64, string adminName, List<IGroup> groups, List<string> permissions)
    {
        Admin newAdmin = new()
        {
            SteamId64 = (long)steamId64,
            Username = adminName,
            Groups = groups.Select(g => g.Name).ToList(),
            Permissions = permissions,
            Servers = [ServerLoader.ServerGUID]
        };

        Task.Run(async () =>
        {
            if (_confMonitor!.CurrentValue.UseDatabase == true)
            {
                var db = _core.Database.GetConnection("admins");
                await db.InsertAsync(newAdmin);
                _serverAdmins?.Load();
            }
        });

        return newAdmin;
    }

    public IAdmin? GetAdmin(int playerid)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return null;

        return GetAdmin(player);
    }

    public IAdmin? GetAdmin(IPlayer player)
    {
        return ServerAdmins.OnlineAdmins.TryGetValue(player, out var admin) ? admin : null;
    }

    public IAdmin? GetAdmin(ulong steamId64)
    {
        var players = _core.PlayerManager.GetAllPlayers();
        foreach (var player in players)
        {
            if (player.SteamID == steamId64)
            {
                return GetAdmin(player);
            }
        }

        return null;
    }

    public List<IAdmin> GetAllAdmins()
    {
        return ServerAdmins.AllAdmins.Values.Cast<IAdmin>().ToList();
    }
    public void RefreshAdmins()
    {
        _serverAdmins?.Load();
    }

    public void RemoveAdmin(IAdmin admin)
    {
        Task.Run(async () =>
        {
            if (_confMonitor!.CurrentValue.UseDatabase == true)
            {
                var db = _core.Database.GetConnection("admins");
                await db.DeleteAsync((Admin)admin);
            }
            _serverAdmins?.Load();
        });
    }

    public void TriggerOnAdminLoad(IPlayer player, IAdmin admin)
    {
        OnAdminLoad?.Invoke(player, admin);
    }

    public void SetAdmins(List<IAdmin> admins)
    {
        ServerAdmins.AllAdmins = new ConcurrentDictionary<ulong, Admin>(admins.Cast<Admin>().ToDictionary(a => (ulong)a.SteamId64, a => a));
    }
    public void AssignAdmin(IPlayer player, IAdmin admin)
    {
        _serverAdmins?.AssignAdmin(player, (Admin)admin);
    }

    public void UnassignAdmin(IPlayer player, IAdmin admin)
    {
        _serverAdmins?.UnassignAdmin(player, (Admin)admin);
    }

    public async Task<IAdmin?> GetAdminBySteamId64Async(ulong steamId64)
    {
        if (_confMonitor!.CurrentValue.UseDatabase == true)
        {
            var db = _core.Database.GetConnection("admins");
            var admins = await db.GetAllAsync<Admin>();
            return admins.FirstOrDefault(a => a.SteamId64 == (long)steamId64);
        }
        return null;
    }

    public async Task UpdateAdminAsync(IAdmin admin)
    {
        if (_confMonitor!.CurrentValue.UseDatabase == true)
        {
            var db = _core.Database.GetConnection("admins");
            await db.UpdateAsync((Admin)admin);
            _serverAdmins?.Load();
        }
    }

    public async Task AddOrUpdateAdminAsync(IAdmin admin)
    {
        if (_confMonitor!.CurrentValue.UseDatabase == true)
        {
            var db = _core.Database.GetConnection("admins");
            var existing = await GetAdminBySteamId64Async((ulong)admin.SteamId64);

            if (existing != null)
            {
                await db.UpdateAsync((Admin)admin);
            }
            else
            {
                await db.InsertAsync((Admin)admin);
            }
            _serverAdmins?.Load();
        }
    }
}