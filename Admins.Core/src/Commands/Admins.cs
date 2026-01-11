using Admins.Core.Database.Models;
using Admins.Core.Server;
using SwiftlyS2.Shared.Commands;

namespace Admins.Core.Commands;

public partial class ServerCommands
{
    [Command("admins", permission: "admins.command.admins")]
    public async void AdminsCommand(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 1, "admins", ["<give/edit/remove/list>"]))
            return;

        var args = context.Args;
        var subCommand = args[0].ToLower();

        switch (subCommand)
        {
            case "give":
                HandleGiveAdmin(context);
                break;
            case "edit":
                HandleEditAdmin(context);
                break;
            case "remove":
                HandleRemoveAdmin(context);
                break;
            case "list":
                HandleListAdmins(context);
                break;
            default:
                await SendSyntaxAsync(context, "admins", ["<give/edit/remove/list>"]);
                break;
        }
    }

    private async void HandleGiveAdmin(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 4, "admins give", ["<steamid64>", "<username>", "<immunity>", "[group_names]", "[permissions]", "[server_guids]"]))
            return;

        var args = context.Args;
        var localizer = GetPlayerLocalizer(context);

        if (!TryParseSteamID(context, args[1], out var steamId64))
            return;

        var username = args[2];

        if (!uint.TryParse(args[3], out var immunity))
        {
            await context.ReplyAsync(localizer[
                "command.admins.invalid_immunity",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                args[3]
            ]);
            return;
        }

        var groups = args.Length > 4 && !string.IsNullOrEmpty(args[4])
            ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
            : new List<string>();

        var permissions = args.Length > 5 && !string.IsNullOrEmpty(args[5])
            ? args[5].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList()
            : new List<string>();

        var additionalServers = args.Length > 6 && !string.IsNullOrEmpty(args[6])
            ? args[6].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            : new List<string>();

        // Validate server GUIDs
        foreach (var serverGuid in additionalServers)
        {
            if (!Guid.TryParse(serverGuid, out _))
            {
                await context.ReplyAsync(localizer[
                    "command.admins.invalid_server_guid",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    serverGuid
                ]);
                return;
            }
        }

        // Validate group names exist
        foreach (var groupName in groups)
        {
            var group = _groupsManager!.GetGroup(groupName);
            if (group == null)
            {
                await context.ReplyAsync(localizer[
                    "command.admins.group_not_found",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    groupName
                ]);
                return;
            }
        }

        // Check if admin already exists
        var existingAdmin = await _adminsManager!.GetAdminBySteamId64Async(steamId64);

        if (existingAdmin != null)
        {
            // Admin exists, add current server to their servers list if not already present
            if (!existingAdmin.Servers.Contains(ServerLoader.ServerGUID))
            {
                existingAdmin.Servers.Add(ServerLoader.ServerGUID);
            }

            // Add additional servers
            foreach (var server in additionalServers)
            {
                if (!existingAdmin.Servers.Contains(server))
                {
                    existingAdmin.Servers.Add(server);
                }
            }

            // Update other properties
            existingAdmin.Username = username;
            existingAdmin.Immunity = immunity;
            existingAdmin.Groups = groups;
            existingAdmin.Permissions = permissions;

            await _adminsManager.UpdateAdminAsync(existingAdmin);

            await context.ReplyAsync(localizer[
                "command.admins.give.updated",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                username,
                steamId64
            ]);
        }
        else
        {
            // Create new admin
            var servers = new List<string> { ServerLoader.ServerGUID };
            servers.AddRange(additionalServers);

            var newAdmin = new Admin
            {
                SteamId64 = (long)steamId64,
                Username = username,
                Immunity = immunity,
                Groups = groups,
                Permissions = permissions,
                Servers = servers.Distinct().ToList()
            };

            await _adminsManager.AddOrUpdateAdminAsync(newAdmin);

            await context.ReplyAsync(localizer[
                "command.admins.give.success",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                username,
                steamId64
            ]);
        }
    }

    private async void HandleEditAdmin(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 4, "admins edit", ["<steamid64>", "<username>", "<immunity>", "[group_names]", "[permissions]", "[server_guids]"]))
            return;

        var args = context.Args;
        var localizer = GetPlayerLocalizer(context);

        if (!TryParseSteamID(context, args[1], out var steamId64))
            return;

        var username = args[2];

        if (!uint.TryParse(args[3], out var immunity))
        {
            await context.ReplyAsync(localizer[
                "command.admins.invalid_immunity",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                args[3]
            ]);
            return;
        }

        var groups = args.Length > 4 && !string.IsNullOrEmpty(args[4])
            ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
            : new List<string>();

        var permissions = args.Length > 5 && !string.IsNullOrEmpty(args[5])
            ? args[5].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList()
            : new List<string>();

        var additionalServers = args.Length > 6 && !string.IsNullOrEmpty(args[6])
            ? args[6].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            : new List<string>();

        // Validate server GUIDs
        foreach (var serverGuid in additionalServers)
        {
            if (!Guid.TryParse(serverGuid, out _))
            {
                await context.ReplyAsync(localizer[
                    "command.admins.invalid_server_guid",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    serverGuid
                ]);
                return;
            }
        }

        // Validate group names exist
        foreach (var groupName in groups)
        {
            var group = _groupsManager!.GetGroup(groupName);
            if (group == null)
            {
                await context.ReplyAsync(localizer[
                    "command.admins.group_not_found",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    groupName
                ]);
                return;
            }
        }

        var existingAdmin = await _adminsManager!.GetAdminBySteamId64Async(steamId64);

        if (existingAdmin == null)
        {
            await context.ReplyAsync(localizer[
                "command.admins.not_found",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                steamId64
            ]);
            return;
        }

        // Update admin properties
        existingAdmin.Username = username;
        existingAdmin.Immunity = immunity;
        existingAdmin.Groups = groups;
        existingAdmin.Permissions = permissions;

        // Add additional servers if provided
        foreach (var server in additionalServers)
        {
            if (!existingAdmin.Servers.Contains(server))
            {
                existingAdmin.Servers.Add(server);
            }
        }

        await _adminsManager.UpdateAdminAsync(existingAdmin);

        await context.ReplyAsync(localizer[
            "command.admins.edit.success",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            username,
            steamId64
        ]);
    }

    private async void HandleRemoveAdmin(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 2, "admins remove", ["<steamid64>"]))
            return;

        var args = context.Args;
        var localizer = GetPlayerLocalizer(context);

        if (!TryParseSteamID(context, args[1], out var steamId64))
            return;

        var existingAdmin = await _adminsManager!.GetAdminBySteamId64Async(steamId64);

        if (existingAdmin == null)
        {
            await context.ReplyAsync(localizer[
                "command.admins.not_found",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                steamId64
            ]);
            return;
        }

        // Remove current server from the admin's servers list
        existingAdmin.Servers.Remove(ServerLoader.ServerGUID);

        if (existingAdmin.Servers.Count == 0)
        {
            // No servers left, delete the admin from database
            _adminsManager.RemoveAdmin(existingAdmin);

            await context.ReplyAsync(localizer[
                "command.admins.remove.deleted",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                existingAdmin.Username,
                steamId64
            ]);
        }
        else
        {
            // Update the admin with the new servers list
            await _adminsManager.UpdateAdminAsync(existingAdmin);

            await context.ReplyAsync(localizer[
                "command.admins.remove.success",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                existingAdmin.Username,
                steamId64
            ]);
        }
    }

    private async void HandleListAdmins(ICommandContext context)
    {
        var localizer = GetPlayerLocalizer(context);
        var allAdmins = _adminsManager!.GetAllAdmins();

        if (allAdmins.Count == 0)
        {
            await context.ReplyAsync(localizer[
                "command.admins.list.empty",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix
            ]);
            return;
        }

        // Filter admins who have access to this server
        var serverAdmins = allAdmins.Where(a => a.Servers.Contains(ServerLoader.ServerGUID)).ToList();

        if (serverAdmins.Count == 0)
        {
            await context.ReplyAsync(localizer[
                "command.admins.list.no_server_admins",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix
            ]);
            return;
        }

        await context.ReplyAsync(localizer[
            "command.admins.list.header",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            serverAdmins.Count
        ]);

        foreach (var admin in serverAdmins.OrderByDescending(a => a.Immunity).ThenBy(a => a.Username))
        {
            var groups = admin.Groups.Count > 0 ? string.Join(", ", admin.Groups) : localizer["none"];
            var permissions = admin.Permissions.Count > 0 ? string.Join(", ", admin.Permissions) : localizer["none"];
            var servers = admin.Servers.Count;

            await context.ReplyAsync(localizer[
                "command.admins.list.entry",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                admin.Username,
                admin.SteamId64,
                admin.Immunity,
                groups,
                permissions,
                servers
            ]);
        }
    }
}