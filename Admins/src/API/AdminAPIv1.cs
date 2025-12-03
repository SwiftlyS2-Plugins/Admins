using Admins.Contract;
using Admins.Database.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.API;

public class AdminAPIv1 : IAdminAPIv1
{
    public IAdminBansAPIv1 AdminBansAPI => Admins.AdminBansAPI;

    public IAdminSanctionsAPIv1 AdminSanctionsAPI => Admins.AdminSanctionsAPI;

    public IAdminMenuAPIv1 AdminMenuAPI => Admins.AdminsMenuAPI;

    public event Action<IPlayer, IAdmin>? OnAdminLoad;

    public IAdmin? AddAdmin(ulong steamId64, string adminName, List<IGroup> groups, List<string> permissions)
    {
        var admin = new Admin
        {
            SteamId64 = (long)steamId64,
            Username = adminName,
            Groups = groups.Select(g => g.Name).ToList(),
            Permissions = permissions,
            Servers = [Admins.ServerGUID]
        };
        ServerAdmins.ServerAdmins.AddAdmin(admin);
        return admin;
    }

    public IAdmin? GetAdmin(int playerid)
    {
        var player = Admins.SwiftlyCore.PlayerManager.GetPlayer(playerid);
        if (player == null) return null;

        return GetAdmin(player);
    }

    public IAdmin? GetAdmin(IPlayer player)
    {
        ServerAdmins.ServerAdmins.PlayerAdmins.TryGetValue(player, out var admin);
        return admin;
    }

    public IAdmin? GetAdmin(ulong steamId64)
    {
        var player = Admins.SwiftlyCore.PlayerManager.GetAllPlayers().ToList().Find(p => p.SteamID == steamId64);
        if (player == null) return null;

        return GetAdmin(player);
    }

    public List<IGroup> GetAdminGroups(IAdmin admin)
    {
        return Groups.Groups.AllGroups.Where(g => admin.Groups.Contains(g.Name)).Cast<IGroup>().ToList();
    }

    public List<IAdmin> GetAllAdmins()
    {
        return [.. ServerAdmins.ServerAdmins.AllAdmins.Cast<IAdmin>()];
    }

    public List<IGroup> GetAllGroups()
    {
        return [.. Groups.Groups.AllGroups.Cast<IGroup>()];
    }

    public IGroup? GetGroup(string groupName)
    {
        return Groups.Groups.AllGroups.Find(g => g.Name == groupName);
    }

    public List<IGroup> GetPlayerGroups(IPlayer player)
    {
        var admin = GetAdmin(player);
        if (admin != null) return GetAdminGroups(admin);

        return [];
    }

    public void RefreshAdmins()
    {
        ServerAdmins.ServerAdmins.Load();
    }

    public void RefreshGroups()
    {
        Groups.Groups.Load();
    }

    public void RemoveAdmin(IAdmin admin)
    {
        ServerAdmins.ServerAdmins.RemoveAdmin((Admin)admin);
    }

    public void TriggerLoadAdmin(IPlayer player, IAdmin admin)
    {
        OnAdminLoad?.Invoke(player, admin);
    }
}