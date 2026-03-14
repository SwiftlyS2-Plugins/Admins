using Admins.Core.Database.Models;
using Admins.Core.Server;
using SwiftlyS2.Shared.Commands;

namespace Admins.Core.Commands;

public partial class ServerCommands
{
    [Command("groups", permission: "admins.command.groups")]
    public async void GroupsCommand(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 1, "groups", ["<give/edit/remove/list>"]))
            return;

        var args = context.Args;
        var subCommand = args[0].ToLower();

        switch (subCommand)
        {
            case "give":
                HandleGiveGroup(context);
                break;
            case "edit":
                HandleEditGroup(context);
                break;
            case "remove":
                HandleRemoveGroup(context);
                break;
            case "list":
                HandleListGroups(context);
                break;
            default:
                await SendSyntaxAsync(context, "groups", ["<give/edit/remove/list>"]);
                break;
        }
    }

    private async void HandleGiveGroup(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 3, "groups give", ["<group_name>", "<immunity>", "[permissions]", "[additional_servers]"]))
            return;

        var args = context.Args;
        var localizer = GetPlayerLocalizer(context);

        var groupName = args[1];

        if (!uint.TryParse(args[2], out var immunity))
        {
            await context.ReplyAsync(localizer[
                "command.groups.invalid_immunity",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                args[2]
            ]);
            return;
        }

        var permissions = args.Length > 3 && !string.IsNullOrEmpty(args[3])
            ? args[3].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList()
            : new List<string>();

        var additionalServers = args.Length > 4 && !string.IsNullOrEmpty(args[4])
            ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            : new List<string>();

        var existingGroup = await _groupsManager!.GetGroupByNameAsync(groupName);

        if (existingGroup != null)
        {
            if (!existingGroup.Servers.Contains(ServerLoader.ServerGUID))
            {
                existingGroup.Servers.Add(ServerLoader.ServerGUID);
            }

            foreach (var server in additionalServers)
            {
                if (!existingGroup.Servers.Contains(server))
                {
                    existingGroup.Servers.Add(server);
                }
            }

            existingGroup.Immunity = (int)immunity;
            existingGroup.Permissions = permissions;

            await _groupsManager.UpdateGroupAsync(existingGroup);

            await context.ReplyAsync(localizer[
                "command.groups.give.updated",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                groupName
            ]);
        }
        else
        {
            var servers = new List<string> { ServerLoader.ServerGUID };
            servers.AddRange(additionalServers);

            var newGroup = new Group
            {
                Name = groupName,
                Immunity = (int)immunity,
                Permissions = permissions,
                Servers = servers.Distinct().ToList()
            };

            await _groupsManager.AddOrUpdateGroupAsync(newGroup);

            await context.ReplyAsync(localizer[
                "command.groups.give.success",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                groupName
            ]);
        }
    }

    private async void HandleEditGroup(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 3, "groups edit", ["<group_name>", "<immunity>", "[permissions]", "[additional_servers]"]))
            return;

        var args = context.Args;
        var localizer = GetPlayerLocalizer(context);

        var groupName = args[1];

        if (!uint.TryParse(args[2], out var immunity))
        {
            await context.ReplyAsync(localizer[
                "command.groups.invalid_immunity",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                args[2]
            ]);
            return;
        }

        var permissions = args.Length > 3 && !string.IsNullOrEmpty(args[3])
            ? args[3].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList()
            : new List<string>();

        var additionalServers = args.Length > 4 && !string.IsNullOrEmpty(args[4])
            ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            : new List<string>();

        var existingGroup = await _groupsManager!.GetGroupByNameAsync(groupName);

        if (existingGroup == null)
        {
            await context.ReplyAsync(localizer[
                "command.groups.not_found",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                groupName
            ]);
            return;
        }

        existingGroup.Immunity = (int)immunity;
        existingGroup.Permissions = permissions;

        foreach (var server in additionalServers)
        {
            if (!existingGroup.Servers.Contains(server))
            {
                existingGroup.Servers.Add(server);
            }
        }

        await _groupsManager.UpdateGroupAsync(existingGroup);

        await context.ReplyAsync(localizer[
            "command.groups.edit.success",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            groupName
        ]);
    }

    private async void HandleRemoveGroup(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 2, "groups remove", ["<group_name>"]))
            return;

        var args = context.Args;
        var localizer = GetPlayerLocalizer(context);
        var groupName = args[1];

        var existingGroup = await _groupsManager!.GetGroupByNameAsync(groupName);

        if (existingGroup == null)
        {
            await context.ReplyAsync(localizer[
                "command.groups.not_found",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                groupName
            ]);
            return;
        }

        existingGroup.Servers.Remove(ServerLoader.ServerGUID);

        if (existingGroup.Servers.Count == 0)
        {
            await _groupsManager.RemoveGroupAsync(existingGroup);

            await context.ReplyAsync(localizer[
                "command.groups.remove.deleted",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                groupName
            ]);
        }
        else
        {
            await _groupsManager.UpdateGroupAsync(existingGroup);

            await context.ReplyAsync(localizer[
                "command.groups.remove.success",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                groupName
            ]);
        }
    }

    private async void HandleListGroups(ICommandContext context)
    {
        var localizer = GetPlayerLocalizer(context);
        var allGroups = _groupsManager!.GetAllGroups();

        if (allGroups.Count == 0)
        {
            await context.ReplyAsync(localizer[
                "command.groups.list.empty",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix
            ]);
            return;
        }

        var serverGroups = allGroups.Where(g => g.Servers.Contains(ServerLoader.ServerGUID)).ToList();

        if (serverGroups.Count == 0)
        {
            await context.ReplyAsync(localizer[
                "command.groups.list.no_server_groups",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix
            ]);
            return;
        }

        await context.ReplyAsync(localizer[
            "command.groups.list.header",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            serverGroups.Count
        ]);

        foreach (var group in serverGroups.OrderByDescending(g => g.Immunity).ThenBy(g => g.Name))
        {
            var permissions = group.Permissions.Count > 0 ? string.Join(", ", group.Permissions) : localizer["none"];
            var servers = group.Servers.Count;

            await context.ReplyAsync(localizer[
                "command.groups.list.entry",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                group.Name,
                group.Immunity,
                permissions,
                servers
            ]);
        }
    }
}
