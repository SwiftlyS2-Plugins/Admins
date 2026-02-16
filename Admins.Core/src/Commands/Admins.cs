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

    [Command("reloadadmins", permission: "admins.command.reloadadmins")]
    public async void ReloadAdminsCommand(ICommandContext context)
    {
        var localizer = GetPlayerLocalizer(context);

        _groupsManager!.RefreshGroups();
        _adminsManager!.RefreshAdmins();

        await context.ReplyAsync(localizer[
            "command.reloadadmins.success",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix
        ]);
    }

    private async void HandleGiveAdmin(ICommandContext context)
    {
        if (!await ValidateArgsCountAsync(context, 4, "admins give", ["<steamid64>", "<username>", "<immunity>", "[permissions]", "[groups]", "[server_guids]"]))
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

        var permissions = args.Length > 4 && !string.IsNullOrEmpty(args[4])
            ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList()
            : new List<string>();

        var groups = args.Length > 5 && !string.IsNullOrEmpty(args[5])
            ? args[5].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList()
            : new List<string>();

        var additionalServers = args.Length > 6 && !string.IsNullOrEmpty(args[6])
            ? args[6].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            : new List<string>();

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

        if (existingAdmin != null)
        {
            if (!existingAdmin.Servers.Contains(ServerLoader.ServerGUID))
            {
                existingAdmin.Servers.Add(ServerLoader.ServerGUID);
            }

            foreach (var server in additionalServers)
            {
                if (!existingAdmin.Servers.Contains(server))
                {
                    existingAdmin.Servers.Add(server);
                }
            }

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
        if (!await ValidateArgsCountAsync(context, 3, "admins edit", ["<steamid64>", "<username|groups|permissions|servers|immunity>", "<value>"]))
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

        var field = args[2].ToLower();
        var value = args[3];

        switch (field)
        {
            case "username":
                existingAdmin.Username = value;
                break;

            case "immunity":
                if (!uint.TryParse(value, out var immunity))
                {
                    await context.ReplyAsync(localizer[
                        "command.admins.invalid_immunity",
                        ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                        value
                    ]);
                    return;
                }
                existingAdmin.Immunity = immunity;
                break;

            case "permissions":
                var permissions = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList();
                existingAdmin.Permissions = permissions;
                break;

            case "groups":
                var groups = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim())
                    .ToList();

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
                existingAdmin.Groups = groups;
                break;

            case "servers":
                var servers = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();

                foreach (var serverGuid in servers)
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
                existingAdmin.Servers = servers;
                break;

            default:
                await context.ReplyAsync(localizer[
                    "command.admins.edit.invalid_field",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    field
                ]);
                return;
        }

        await _adminsManager.UpdateAdminAsync(existingAdmin);

        await context.ReplyAsync(localizer[
            "command.admins.edit.success",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            existingAdmin.Username,
            steamId64,
            field,
            value
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

        existingAdmin.Servers.Remove(ServerLoader.ServerGUID);

        if (existingAdmin.Servers.Count == 0)
        {
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