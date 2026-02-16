using System.Collections.Concurrent;
using Admins.Core.Config;
using Admins.Core.Database.Models;
using Admins.Core.Groups;
using Admins.Core.Server;
using Dommel;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Core.Admins;

public partial class ServerAdmins
{
    private ISwiftlyCore Core = null!;
    public static ConcurrentDictionary<ulong, Admin> AllAdmins { get; set; } = [];
    public static ConcurrentDictionary<IPlayer, Admin> OnlineAdmins { get; set; } = [];
    private IOptionsMonitor<CoreConfiguration>? _config;
    private AdminsManager? _adminsManager;

    public ServerAdmins(IOptionsMonitor<CoreConfiguration> config, ISwiftlyCore core)
    {
        core.Registrator.Register(this);
        _config = config;
        Core = core;
    }

    public void SetAdminsManager(AdminsManager adminsManager)
    {
        _adminsManager = adminsManager;
    }

    public void Load()
    {
        Task.Run(async () =>
        {
            foreach (var (adminPlayer, adminObject) in OnlineAdmins)
            {
                UnassignAdmin(adminPlayer, adminObject);
            }

            if (_config!.CurrentValue.UseDatabase == true)
            {
                var db = Core.Database.GetConnection("admins");
                var admins = await db.GetAllAsync<Admin>();
                AllAdmins = new ConcurrentDictionary<ulong, Admin>(admins.ToDictionary(a => (ulong)a.SteamId64, a => a));
            }

            AssignAdmins();
        });
    }

    public void AssignAdmins()
    {
        var players = Core.PlayerManager.GetAllPlayers();
        foreach (var player in players)
        {
            if (!AllAdmins.ContainsKey(player.SteamID)) continue;

            var adminObject = AllAdmins[player.SteamID];
            if (!adminObject.Servers.Contains(ServerLoader.ServerGUID)) continue;

            if (!player.IsAuthorized) continue;

            AssignAdmin(player, adminObject);
        }
    }

    public void AssignAdmin(IPlayer player, Admin admin)
    {
        OnlineAdmins.TryAdd(player, admin);

        foreach (var permission in admin.Permissions)
        {
            Core.Permission.AddPermission(player.SteamID, permission);
        }

        foreach (var group in admin.Groups)
        {
            var groupObject = ServerGroups.AllGroups.FirstOrDefault(p => p.Value.Name == group && p.Value.Servers.Contains(ServerLoader.ServerGUID));
            if (groupObject.Value != null)
            {
                foreach (var permission in groupObject.Value.Permissions)
                {
                    Core.Permission.AddPermission(player.SteamID, permission);
                }
            }
        }

        _adminsManager?.TriggerOnAdminLoad(player, admin);
    }

    public void UnassignAdmin(IPlayer player, Admin admin)
    {
        foreach (var permission in admin.Permissions)
        {
            Core.Permission.RemovePermission(player.SteamID, permission);
        }

        foreach (var group in admin.Groups)
        {
            var groupObject = ServerGroups.AllGroups.FirstOrDefault(p => p.Value.Name == group && p.Value.Servers.Contains(ServerLoader.ServerGUID));
            if (groupObject.Value != null)
            {
                foreach (var permission in groupObject.Value.Permissions)
                {
                    Core.Permission.RemovePermission(player.SteamID, permission);
                }
            }
        }

        OnlineAdmins.TryRemove(player, out _);
    }
}