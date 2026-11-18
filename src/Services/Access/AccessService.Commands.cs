using System.Data;
using Admins.Database.Models;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;
using ServerRow = Admins.Database.Models.Server;

namespace Admins.Services.Access;

public sealed partial class AccessService
{
    private const string AdminsUsage = "<give|edit|remove|list>";
    private const string GroupsUsage = "<give|edit|remove|list>";

    [Command("admins", permission: "admins.command.admins")]
    public void AdminsCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        switch (args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty)
        {
            case "give":
                GiveAdmin(issuer, context);
                break;
            case "edit":
                EditAdmin(issuer, context);
                break;
            case "remove":
                RemoveAdmin(issuer, context);
                break;
            case "list":
                ListAdmins(issuer);
                break;
            default:
                Syntax(issuer, context, "admins", AdminsUsage);
                break;
        }
    }

    [Command("groups", permission: "admins.command.groups")]
    public void GroupsCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        switch (args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty)
        {
            case "give":
                GiveGroup(issuer, context, create: true);
                break;
            case "edit":
                GiveGroup(issuer, context, create: false);
                break;
            case "remove":
                RemoveGroup(issuer, context);
                break;
            case "list":
                ListGroups(issuer);
                break;
            default:
                Syntax(issuer, context, "groups", GroupsUsage);
                break;
        }
    }

    [Command("reloadadmins", permission: "admins.command.reloadadmins")]
    public void ReloadAdminsCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);

        _ = Task.Run(async () =>
        {
            try
            {
                await ReloadAsync().ConfigureAwait(false);
                _messenger.ReplyLater(issuer, "command.reloadadmins.success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!reloadadmins failed.");
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    private void GiveAdmin(Issuer issuer, ICommandContext context)
    {
        var args = context.Args;
        if (args.Length < 4)
        {
            Syntax(issuer, context, "admins give", "<steamid64> <username> <immunity> [permissions] [groups] [servers]");
            return;
        }

        if (!SteamIds.TryParse(args[1], out var steamId))
        {
            _messenger.Reply(issuer, "command.invalid_steamid", args[1]);
            return;
        }

        var username = args[2];

        if (!TryParseImmunity(issuer, args[3], out var immunity)) return;

        var permissions = SplitList(args, 4);
        var groups = SplitList(args, 5);
        var servers = SplitList(args, 6);

        if (!ValidateGroups(issuer, groups) || !ValidateServers(issuer, servers) || !RequireDatabase(issuer)) return;

        var guid = _server.Guid;
        var id = (long)steamId;

        InBackground(issuer, async db =>
        {
            if (!await ToGuidsAsync(issuer, db, servers).ConfigureAwait(false)) return;

            if (await db.FirstOrDefaultAsync<Admin>(a => a.Username == username && a.SteamId64 != id).ConfigureAwait(false) is not null)
            {
                _messenger.ReplyLater(issuer, "command.admins.username_taken", username);
                return;
            }

            var existing = await db.FirstOrDefaultAsync<Admin>(a => a.SteamId64 == id).ConfigureAwait(false);
            if (existing is null)
            {
                await db.InsertAsync(new Admin
                {
                    SteamId64 = id,
                    Username = username,
                    Immunity = immunity,
                    Permissions = permissions,
                    Groups = groups,
                    Servers = MergeServers([], [guid, .. servers]),
                }).ConfigureAwait(false);

                _messenger.ReplyLater(issuer, "command.admins.give.success", username, steamId);
            }
            else
            {
                existing.Username = username;
                existing.Immunity = immunity;
                existing.Permissions = permissions;
                existing.Groups = groups;
                existing.Servers = MergeServers(existing.Servers, [guid, .. servers]);
                await db.UpdateAsync(existing).ConfigureAwait(false);

                _messenger.ReplyLater(issuer, "command.admins.give.updated", username, steamId);
            }

            await ReloadAsync().ConfigureAwait(false);
        });
    }

    private void EditAdmin(Issuer issuer, ICommandContext context)
    {
        var args = context.Args;
        if (args.Length < 4)
        {
            Syntax(issuer, context, "admins edit", "<steamid64> <username|groups|permissions|servers|immunity> <value>");
            return;
        }

        if (!SteamIds.TryParse(args[1], out var steamId))
        {
            _messenger.Reply(issuer, "command.invalid_steamid", args[1]);
            return;
        }

        var field = args[2].ToLowerInvariant();
        var value = string.Join(' ', args.Skip(3)).Trim();
        List<string> servers = [];
        Action<Admin> change;

        switch (field)
        {
            case "username":
                change = admin => admin.Username = value;
                break;
            case "immunity":
                if (!TryParseImmunity(issuer, value, out var immunity)) return;
                change = admin => admin.Immunity = immunity;
                break;
            case "permissions":
                var permissions = SplitList([value], 0);
                change = admin => admin.Permissions = permissions;
                break;
            case "groups":
                var groups = SplitList([value], 0);
                if (!ValidateGroups(issuer, groups)) return;
                change = admin => admin.Groups = groups;
                break;
            case "servers":
                servers = SplitList([value], 0);
                if (!ValidateServers(issuer, servers)) return;
                change = admin => admin.Servers = MergeServers([], servers);
                break;
            default:
                _messenger.Reply(issuer, "command.admins.edit.invalid_field", field);
                return;
        }

        if (!RequireDatabase(issuer)) return;

        var id = (long)steamId;

        InBackground(issuer, async db =>
        {
            if (!await ToGuidsAsync(issuer, db, servers).ConfigureAwait(false)) return;

            var existing = await db.FirstOrDefaultAsync<Admin>(a => a.SteamId64 == id).ConfigureAwait(false);
            if (existing is null)
            {
                _messenger.ReplyLater(issuer, "command.admins.not_found", steamId);
                return;
            }

            if (field == "username" && await db.FirstOrDefaultAsync<Admin>(a => a.Username == value && a.SteamId64 != id).ConfigureAwait(false) is not null)
            {
                _messenger.ReplyLater(issuer, "command.admins.username_taken", value);
                return;
            }

            change(existing);
            await db.UpdateAsync(existing).ConfigureAwait(false);
            await ReloadAsync().ConfigureAwait(false);

            _messenger.ReplyLater(issuer, "command.admins.edit.success", existing.Username, steamId, field, value);
        });
    }

    private void RemoveAdmin(Issuer issuer, ICommandContext context)
    {
        var args = context.Args;
        if (args.Length < 2)
        {
            Syntax(issuer, context, "admins remove", "<steamid64>");
            return;
        }

        if (!SteamIds.TryParse(args[1], out var steamId))
        {
            _messenger.Reply(issuer, "command.invalid_steamid", args[1]);
            return;
        }

        if (!RequireDatabase(issuer)) return;

        var guid = _server.Guid;
        var id = (long)steamId;

        InBackground(issuer, async db =>
        {
            var existing = await db.FirstOrDefaultAsync<Admin>(a => a.SteamId64 == id).ConfigureAwait(false);
            if (existing is null)
            {
                _messenger.ReplyLater(issuer, "command.admins.not_found", steamId);
                return;
            }

            existing.Servers.RemoveAll(s => string.Equals(s?.Trim(), guid, StringComparison.OrdinalIgnoreCase));

            if (existing.Servers.Count == 0)
            {
                await db.DeleteAsync(existing).ConfigureAwait(false);
                _messenger.ReplyLater(issuer, "command.admins.remove.deleted", existing.Username, steamId);
            }
            else
            {
                await db.UpdateAsync(existing).ConfigureAwait(false);
                _messenger.ReplyLater(issuer, "command.admins.remove.success", existing.Username, steamId);
            }

            await ReloadAsync().ConfigureAwait(false);
        });
    }

    private void ListAdmins(Issuer issuer)
    {
        var snapshot = _snapshot;
        if (snapshot.Admins.Count == 0)
        {
            _messenger.Reply(issuer, "command.admins.list.empty");
            return;
        }

        var admins = snapshot.Admins.Where(a => AppliesHere(a.Servers)).OrderByDescending(a => a.Immunity).ThenBy(a => a.Username).ToList();
        if (admins.Count == 0)
        {
            _messenger.Reply(issuer, "command.admins.list.no_server_admins");
            return;
        }

        var none = _messenger.Text(issuer.Player, "none");
        _messenger.Reply(issuer, "command.admins.list.header", admins.Count);

        foreach (var admin in admins)
        {
            _messenger.Reply(
                issuer,
                "command.admins.list.entry",
                admin.Username,
                admin.SteamId64,
                admin.Immunity,
                admin.Groups.Count > 0 ? string.Join(", ", admin.Groups) : none,
                admin.Permissions.Count > 0 ? string.Join(", ", admin.Permissions) : none,
                admin.Servers.Count);
        }
    }

    // "give" creates or overwrites and adds this server; "edit" requires an existing group and keeps its servers.
    private void GiveGroup(Issuer issuer, ICommandContext context, bool create)
    {
        var args = context.Args;
        var name = create ? "groups give" : "groups edit";
        if (args.Length < 3)
        {
            Syntax(issuer, context, name, "<group_name> <immunity> [permissions] [additional_servers]");
            return;
        }

        var groupName = args[1].Trim();

        if (!TryParseImmunity(issuer, args[2], out var immunity)) return;

        var permissions = SplitList(args, 3);
        var servers = SplitList(args, 4);

        if (!ValidateServers(issuer, servers) || !RequireDatabase(issuer)) return;

        var guid = _server.Guid;

        InBackground(issuer, async db =>
        {
            if (!await ToGuidsAsync(issuer, db, servers).ConfigureAwait(false)) return;

            var existing = await FindGroupAsync(db, groupName).ConfigureAwait(false);

            if (existing is null)
            {
                if (!create)
                {
                    _messenger.ReplyLater(issuer, "command.groups.not_found", groupName);
                    return;
                }

                await db.InsertAsync(new Group
                {
                    Name = groupName,
                    Immunity = immunity,
                    Permissions = permissions,
                    Servers = MergeServers([], [guid, .. servers]),
                }).ConfigureAwait(false);

                _messenger.ReplyLater(issuer, "command.groups.give.success", groupName);
            }
            else
            {
                existing.Immunity = immunity;
                existing.Permissions = permissions;
                existing.Servers = MergeServers(existing.Servers, create ? [guid, .. servers] : servers);
                await db.UpdateAsync(existing).ConfigureAwait(false);

                _messenger.ReplyLater(issuer, create ? "command.groups.give.updated" : "command.groups.edit.success", existing.Name);
            }

            await ReloadAsync().ConfigureAwait(false);
        });
    }

    private void RemoveGroup(Issuer issuer, ICommandContext context)
    {
        var args = context.Args;
        if (args.Length < 2)
        {
            Syntax(issuer, context, "groups remove", "<group_name>");
            return;
        }

        if (!RequireDatabase(issuer)) return;

        var groupName = args[1].Trim();
        var guid = _server.Guid;

        InBackground(issuer, async db =>
        {
            var existing = await FindGroupAsync(db, groupName).ConfigureAwait(false);
            if (existing is null)
            {
                _messenger.ReplyLater(issuer, "command.groups.not_found", groupName);
                return;
            }

            existing.Servers.RemoveAll(s => string.Equals(s?.Trim(), guid, StringComparison.OrdinalIgnoreCase));

            if (existing.Servers.Count == 0)
            {
                await db.DeleteAsync(existing).ConfigureAwait(false);
                _messenger.ReplyLater(issuer, "command.groups.remove.deleted", existing.Name);
            }
            else
            {
                await db.UpdateAsync(existing).ConfigureAwait(false);
                _messenger.ReplyLater(issuer, "command.groups.remove.success", existing.Name);
            }

            await ReloadAsync().ConfigureAwait(false);
        });
    }

    private void ListGroups(Issuer issuer)
    {
        var snapshot = _snapshot;
        if (snapshot.Groups.Count == 0)
        {
            _messenger.Reply(issuer, "command.groups.list.empty");
            return;
        }

        var groups = snapshot.Groups.Where(g => AppliesHere(g.Servers)).OrderByDescending(g => g.Immunity).ThenBy(g => g.Name).ToList();
        if (groups.Count == 0)
        {
            _messenger.Reply(issuer, "command.groups.list.no_server_groups");
            return;
        }

        var none = _messenger.Text(issuer.Player, "none");
        _messenger.Reply(issuer, "command.groups.list.header", groups.Count);

        foreach (var group in groups)
        {
            _messenger.Reply(
                issuer,
                "command.groups.list.entry",
                group.Name,
                group.Immunity,
                group.Permissions.Count > 0 ? string.Join(", ", group.Permissions) : none,
                group.Servers.Count);
        }
    }

    #region IAdminsManager / IGroupsManager writes

    public async Task<Contract.IAdmin?> GetAdminFromDatabaseAsync(ulong steamId64)
    {
        if (!_database.Enabled) return null;

        var id = (long)steamId64;
        using var db = _database.Connect();
        return await db.FirstOrDefaultAsync<Admin>(a => a.SteamId64 == id).ConfigureAwait(false);
    }

    public async Task AddOrUpdateAdminAsync(Contract.IAdmin admin)
    {
        if (!_database.Enabled) return;

        var row = Admin.From(admin);
        using (var db = _database.Connect())
        {
            var id = row.SteamId64;
            var existing = await db.FirstOrDefaultAsync<Admin>(a => a.SteamId64 == id).ConfigureAwait(false);
            if (existing is null)
            {
                row.Id = 0;
                await db.InsertAsync(row).ConfigureAwait(false);
            }
            else
            {
                row.Id = existing.Id;
                await db.UpdateAsync(row).ConfigureAwait(false);
            }
        }

        await ReloadAsync().ConfigureAwait(false);
    }

    public async Task RemoveAdminAsync(Contract.IAdmin admin)
    {
        if (!_database.Enabled) return;

        var id = admin.SteamId64;
        using (var db = _database.Connect())
        {
            await db.DeleteMultipleAsync<Admin>(a => a.SteamId64 == id).ConfigureAwait(false);
        }

        await ReloadAsync().ConfigureAwait(false);
    }

    public async Task<Contract.IGroup?> GetGroupFromDatabaseAsync(string groupName)
    {
        if (!_database.Enabled) return null;

        using var db = _database.Connect();
        return await FindGroupAsync(db, groupName).ConfigureAwait(false);
    }

    public async Task AddOrUpdateGroupAsync(Contract.IGroup group)
    {
        if (!_database.Enabled) return;

        var row = Group.From(group);
        using (var db = _database.Connect())
        {
            var existing = await FindGroupAsync(db, row.Name).ConfigureAwait(false);
            if (existing is null)
            {
                row.Id = 0;
                await db.InsertAsync(row).ConfigureAwait(false);
            }
            else
            {
                row.Id = existing.Id;
                await db.UpdateAsync(row).ConfigureAwait(false);
            }
        }

        await ReloadAsync().ConfigureAwait(false);
    }

    public async Task RemoveGroupAsync(Contract.IGroup group)
    {
        if (!_database.Enabled) return;

        using (var db = _database.Connect())
        {
            if (await FindGroupAsync(db, group.Name).ConfigureAwait(false) is { } existing)
            {
                await db.DeleteAsync(existing).ConfigureAwait(false);
            }
        }

        await ReloadAsync().ConfigureAwait(false);
    }

    #endregion

    #region Helpers

    // Group names are matched case-insensitively everywhere; the table is small, so this stays portable
    // instead of relying on each database's collation.
    private static async Task<Group?> FindGroupAsync(IDbConnection db, string name)
    {
        var trimmed = name.Trim();
        var groups = await db.GetAllAsync<Group>().ConfigureAwait(false);
        return groups.FirstOrDefault(g => string.Equals(g.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private void InBackground(Issuer issuer, Func<IDbConnection, Task> work) =>
        _ = Task.Run(async () =>
        {
            try
            {
                using var db = _database.Connect();
                await work(db).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin/group command failed.");
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });

    private void Syntax(Issuer issuer, ICommandContext context, string command, string usage) =>
        _messenger.Reply(issuer, "command.syntax", context.Prefix, command, usage);

    private bool TryParseImmunity(Issuer issuer, string value, out int immunity)
    {
        if (int.TryParse(value, out immunity) && immunity >= 0) return true;

        _messenger.Reply(issuer, "command.admins.invalid_immunity", value);
        return false;
    }

    private bool ValidateGroups(Issuer issuer, IEnumerable<string> groups)
    {
        if (groups.FirstOrDefault(g => GetGroup(g) is null) is not { } missing) return true;

        _messenger.Reply(issuer, "command.admins.group_not_found", missing);
        return false;
    }

    private bool ValidateServers(Issuer issuer, IEnumerable<string> servers)
    {
        if (servers.FirstOrDefault(s => !Guid.TryParse(s, out _) && !(long.TryParse(s, out var number) && number > 0)) is not { } invalid) return true;

        _messenger.Reply(issuer, "command.admins.invalid_server_guid", invalid);
        return false;
    }

    // A server is named by its number from serverid or by its GUID; the rows store the GUID, so numbers are replaced in place.
    private async Task<bool> ToGuidsAsync(Issuer issuer, IDbConnection db, List<string> servers)
    {
        for (var i = 0; i < servers.Count; i++)
        {
            if (Guid.TryParse(servers[i], out _)) continue;

            if (await db.GetAsync<ServerRow>(long.Parse(servers[i])).ConfigureAwait(false) is not { } row)
            {
                _messenger.ReplyLater(issuer, "command.serverid.not_found", servers[i]);
                return false;
            }

            servers[i] = row.GUID;
        }

        return true;
    }

    private bool RequireDatabase(Issuer issuer)
    {
        if (_database.Enabled) return true;

        _messenger.Reply(issuer, "command.database_disabled");
        return false;
    }

    private static List<string> SplitList(string[] args, int index) =>
        args.Length > index
            ? [.. args[index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)]
            : [];

    private static List<string> MergeServers(IEnumerable<string> existing, IEnumerable<string> added)
    {
        var result = new List<string>();

        foreach (var server in existing.Concat(added))
        {
            var trimmed = server?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !result.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) result.Add(trimmed);
        }

        return result;
    }

    #endregion
}
