using System.Data;
using Admins.Database;
using Admins.Database.Models;
using Admins.Services.Sessions;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;

namespace Admins.Services.Bans;

public sealed partial class BansService
{
    private const int ExclusionsShown = 20;

    /// <summary>Whether bans leave this SteamID alone where they reach it only through its IP address.</summary>
    public bool IsIpBanExcluded(ulong steamId) => _ipBanExcluded.Contains(steamId);

    // For players sharing a changing address: banning one by IP must not keep shutting the others out.
    [Command("excludeipban", permission: "admins.commands.excludeipban")]
    public void ExcludeIpBanCommand(ICommandContext context) => HandleExclusion(context, "excludeipban", exclude: true);

    [Command("unexcludeipban", permission: "admins.commands.excludeipban")]
    public void UnexcludeIpBanCommand(ICommandContext context) => HandleExclusion(context, "unexcludeipban", exclude: false);

    private void HandleExclusion(ICommandContext context, string command, bool exclude)
    {
        var issuer = Issuer.From(context);
        var query = string.Join(' ', context.Args).Trim();

        if (!_database.Enabled)
        {
            _messenger.Reply(issuer, "command.database_disabled");
            return;
        }

        if (query.Length == 0)
        {
            if (exclude) ListExclusions(issuer);
            else _messenger.Syntax(issuer, command, "<player|steamid64>");
            return;
        }

        var player = _targets.FindOne(issuer.Player, query, out var steamId, out var error);
        if (_messenger.ReportTargetError(issuer, error, query)) return;

        if (player is { SteamID: 0 })
        {
            _messenger.Reply(issuer, "command.target_no_steamid", Names.Of(player));
            return;
        }

        var id = (long)(player?.SteamID ?? steamId);
        var name = player is null ? null : Names.Of(player);

        _ = Task.Run(async () =>
        {
            try
            {
                using var db = _database.Connect();
                var existing = (await db.SelectAsync<IpBanExclusion>(e => e.SteamId64 == id).ConfigureAwait(false)).FirstOrDefault();
                name ??= existing?.PlayerName is { Length: > 0 } stored ? stored : await SessionsService.LastNameAsync(db, (ulong)id).ConfigureAwait(false);
                var display = name is null ? id.ToString() : $"{name} ({id})";

                if (exclude == (existing is not null))
                {
                    _messenger.ReplyLater(issuer, exclude ? "excludeipban.already" : "excludeipban.not_excluded", display);
                    return;
                }

                if (exclude)
                {
                    await db.InsertAsync(new IpBanExclusion
                    {
                        SteamId64 = id,
                        PlayerName = name ?? string.Empty,
                        AdminSteamId64 = (long)issuer.SteamId,
                        AdminName = issuer.Name,
                        CreatedAt = AdminsDatabase.NowMs,
                    }).ConfigureAwait(false);
                }
                else
                {
                    await db.DeleteAsync(existing!).ConfigureAwait(false);
                }

                _ipBanExcluded = await LoadExclusionsAsync(db).ConfigureAwait(false);
                _messenger.ReplyLater(issuer, exclude ? "excludeipban.added" : "excludeipban.removed", issuer.Name, display);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change the IP ban exclusion of {SteamId}.", id);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    private void ListExclusions(Issuer issuer)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                List<IpBanExclusion> rows;
                using (var db = _database.Connect())
                {
                    rows = [.. await db.FromAsync<IpBanExclusion>(q => q.Select().OrderByDescending(e => e.Id).Page(1, ExclusionsShown)).ConfigureAwait(false)];
                }

                _core.Scheduler.NextTick(() =>
                {
                    if (rows.Count == 0)
                    {
                        _messenger.Reply(issuer, "excludeipban.list_none");
                        return;
                    }

                    _messenger.Reply(issuer, "excludeipban.list", rows.Count);
                    foreach (var row in rows)
                    {
                        var who = row.PlayerName.Length > 0 ? $"{row.PlayerName} ({row.SteamId64})" : row.SteamId64.ToString();
                        _messenger.Reply(issuer, "excludeipban.list_row", who, row.AdminName, _messenger.Timestamp(issuer.Player, row.CreatedAt));
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list the IP ban exclusions.");
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    private void ReloadExclusions()
    {
        if (!_database.Enabled) return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var db = _database.Connect();
                _ipBanExcluded = await LoadExclusionsAsync(db).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load the IP ban exclusions.");
            }
        });
    }

    // Apart, so one failing stops neither the other nor the ban check that follows.
    private async Task SyncTablesAsync(IDbConnection db)
    {
        try
        {
            await AdminsDatabase.MarkExpiredAsync<Ban>(db, AdminsDatabase.NowMs).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark ended bans Expired.");
        }

        await RefreshExclusionsAsync(db).ConfigureAwait(false);
    }

    private async Task RefreshExclusionsAsync(IDbConnection db)
    {
        try
        {
            _ipBanExcluded = await LoadExclusionsAsync(db).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the IP ban exclusions.");
        }
    }

    private static async Task<IReadOnlySet<ulong>> LoadExclusionsAsync(IDbConnection db) =>
        (await db.GetAllAsync<IpBanExclusion>().ConfigureAwait(false)).Select(e => (ulong)e.SteamId64).ToHashSet();
}
