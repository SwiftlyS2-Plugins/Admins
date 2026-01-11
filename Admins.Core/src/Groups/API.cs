using System.Collections.Concurrent;
using Admins.Core.Admins;
using Admins.Core.Config;
using Admins.Core.Contract;
using Admins.Core.Database.Models;
using Dommel;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Core.Groups;

public class GroupsManager : IGroupsManager
{
    private ServerGroups _groups;
    private ISwiftlyCore _core;
    private IOptionsMonitor<CoreConfiguration>? _config;

    public GroupsManager(ServerGroups groups, ISwiftlyCore core, IOptionsMonitor<CoreConfiguration> config)
    {
        _groups = groups;
        _core = core;
        _config = config;
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

    public async Task<IGroup?> GetGroupByNameAsync(string groupName)
    {
        if (_config!.CurrentValue.UseDatabase == true)
        {
            var db = _core.Database.GetConnection("admins");
            var groups = await db.GetAllAsync<Group>();
            return groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    public async Task UpdateGroupAsync(IGroup group)
    {
        if (_config!.CurrentValue.UseDatabase == true)
        {
            var db = _core.Database.GetConnection("admins");
            await db.UpdateAsync((Group)group);
            _groups.Load();
        }
    }

    public async Task AddOrUpdateGroupAsync(IGroup group)
    {
        if (_config!.CurrentValue.UseDatabase == true)
        {
            var db = _core.Database.GetConnection("admins");
            var existing = await GetGroupByNameAsync(group.Name);

            if (existing != null)
            {
                await db.UpdateAsync((Group)group);
            }
            else
            {
                await db.InsertAsync((Group)group);
            }
            _groups.Load();
        }
    }

    public async Task RemoveGroupAsync(IGroup group)
    {
        if (_config!.CurrentValue.UseDatabase == true)
        {
            var db = _core.Database.GetConnection("admins");
            await db.DeleteAsync((Group)group);
            _groups.Load();
        }
    }
}