using System.Collections.Concurrent;
using Admins.API;
using Admins.Database.Models;
using Dommel;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.ServerAdmins;

public partial class ServerAdmins
{
    private static ISwiftlyCore Core = null!;

    public static List<Admin> AllAdmins { get; private set; } = new();
    public static ConcurrentDictionary<IPlayer, Admin> PlayerAdmins { get; private set; } = new();

    public static void Load()
    {
        Core = Admins.SwiftlyCore;

        Task.Run(() =>
        {
            foreach (var (player, admin) in PlayerAdmins)
            {
                if (!admin.Servers.Contains(Admins.ServerGUID)) continue;

                UnassignAdmin(player, admin);
            }

            var database = Core.Database.GetConnection("admins");
            AllAdmins = [.. database.GetAll<Admin>()];

            AssignAdmins();
        });
    }

    public static void AssignAdmins()
    {
        var players = Core.PlayerManager.GetAllPlayers();
        foreach (var player in players)
        {
            if (AllAdmins.Find(a => (ulong)a.SteamId64 == player.SteamID && a.Servers.Contains(Admins.ServerGUID)) is not Admin admin)
                continue;
            if (!player.IsAuthorized)
                continue;

            AssignAdmin(player, admin);
        }
    }

    public static void AssignAdmin(IPlayer player, Admin admin)
    {
        PlayerAdmins.TryAdd(player, admin);

        foreach (var permission in admin.Permissions)
        {
            Core.Permission.AddPermission(player.SteamID, permission);
        }

        foreach (var group in admin.Groups)
        {
            var obj = Groups.Groups.AllGroups.Find(p => p.Name == group && p.Servers.Contains(Admins.ServerGUID));
            if (obj == null) continue;

            foreach (var permission in obj.Permissions)
            {
                Core.Permission.AddPermission(player.SteamID, permission);
            }
        }

        Admins.AdminAPI.TriggerLoadAdmin(player, admin);
    }

    public static void UnassignAdmin(IPlayer player, Admin admin)
    {
        foreach (var permission in admin.Permissions)
        {
            Core.Permission.RemovePermission(player.SteamID, permission);
        }

        foreach (var group in admin.Groups)
        {
            var obj = Groups.Groups.AllGroups.Find(p => p.Name == group && p.Servers.Contains(Admins.ServerGUID));
            if (obj == null) continue;

            foreach (var permission in obj.Permissions)
            {
                Core.Permission.RemovePermission(player.SteamID, permission);
            }
        }

        PlayerAdmins.TryRemove(player, out _);
    }

    public static void RemoveAdmin(Admin admin)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            database.Delete(admin);
            Load();
        });
    }

    public static void AddAdmin(Admin admin)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            database.Insert(admin);
            Load();
        });
    }
}