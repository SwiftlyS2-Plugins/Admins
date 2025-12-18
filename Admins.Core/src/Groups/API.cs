using System.Collections.Concurrent;
using Admins.Core.Admins;
using Admins.Core.Contract;
using Admins.Core.Database.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Core.Groups;

public class GroupsManager : IGroupsManager
{
    private ServerGroups _groups;

    public GroupsManager(ServerGroups groups)
    {
        _groups = groups;
    }

    public List<IGroup> GetAdminGroups(IAdmin admin)
    {
        return ServerGroups.AllGroups.Values.Where(g => admin.Groups.Contains(g.Name)).Cast<IGroup>().ToList();
    }

    public List<IGroup> GetAllGroups()
    {
        return ServerGroups.AllGroups.Values.Cast<IGroup>().ToList();
    }

    public IGroup? GetGroup(string groupName)
    {
        return ServerGroups.AllGroups.Values.Where(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)).Cast<IGroup>().FirstOrDefault();
    }

    public List<IGroup> GetPlayerGroups(IPlayer player)
    {
        var admin = ServerAdmins.OnlineAdmins.TryGetValue(player, out var adm) ? adm : null;
        if (admin == null) return [];

        return GetAdminGroups(admin);
    }

    public void RefreshGroups()
    {
        _groups.Load();
    }

    public void SetGroups(List<IGroup> groups)
    {
        ServerGroups.AllGroups = new ConcurrentDictionary<ulong, Group>(groups.ToDictionary(g => g.Id, g => (Group)g));
    }
}