using Admins.Contract;
using Admins.Database.Models;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;

namespace Admins.Services.Bans;

public sealed partial class BansService
{
    [Command("ban", permission: "admins.commands.ban")]
    public void BanCommand(ICommandContext context) => HandleBan(context, "ban", BanType.SteamID, global: false);

    [Command("globalban", permission: "admins.commands.globalban")]
    public void GlobalBanCommand(ICommandContext context) => HandleBan(context, "globalban", BanType.SteamID, global: true);

    [Command("banip", permission: "admins.commands.ban")]
    public void BanIpCommand(ICommandContext context) => HandleBan(context, "banip", BanType.IP, global: false);

    [Command("globalbanip", permission: "admins.commands.globalban")]
    public void GlobalBanIpCommand(ICommandContext context) => HandleBan(context, "globalbanip", BanType.IP, global: true);

    [Command("unban", permission: "admins.commands.unban")]
    public void UnbanCommand(ICommandContext context) => HandleUnban(context, "unban", byIp: false, global: false);

    [Command("unbanip", permission: "admins.commands.unban")]
    public void UnbanIpCommand(ICommandContext context) => HandleUnban(context, "unbanip", byIp: true, global: false);

    [Command("globalunban", permission: "admins.commands.globalunban")]
    public void GlobalUnbanCommand(ICommandContext context) => HandleUnban(context, "globalunban", byIp: false, global: true);

    [Command("globalunbanip", permission: "admins.commands.globalunban")]
    public void GlobalUnbanIpCommand(ICommandContext context) => HandleUnban(context, "globalunbanip", byIp: true, global: true);

    private void HandleBan(ICommandContext context, string command, BanType type, bool global)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (args.Length == 0 && issuer.Player is { } viewer && _shortcuts.OpenBan is { } open)
        {
            open(viewer, type, global);
            return;
        }

        if (args.Length < 3)
        {
            _messenger.Syntax(issuer, command, type == BanType.IP ? "<ip> <time> <reason>" : "<player|steamid64|ip> <time> <reason>");
            return;
        }

        if (!Durations.TryParse(args[1], out var duration))
        {
            _messenger.Reply(issuer, "command.invalid_time_format", args[1]);
            return;
        }

        var reason = string.Join(' ', args.Skip(2)).Trim();

        // An address is read before names, so no player is banned by IP because their name looks like one, nor by name
        // because it contains an address.
        if (type == BanType.IP || Net.IsIp(args[0]))
        {
            BanIp(issuer, args[0], duration, reason, global);
            return;
        }

        var player = _targets.FindOne(issuer.Player, args[0], out var steamId, out var error);
        if (_messenger.ReportTargetError(issuer, error, args[0])) return;

        // Bots, and humans Steam has not authorized yet.
        if (player is { SteamID: 0 })
        {
            _messenger.Reply(issuer, "command.target_no_steamid", Names.Of(player));
            return;
        }

        var subject = player?.SteamID ?? steamId;

        if (!_access.CanTarget(issuer.Player, subject))
        {
            _messenger.Reply(issuer, "command.target_has_immunity", player is null ? subject : Names.Of(player), _access.GetImmunity(subject));
            return;
        }

        if (player is not null) BanPlayer(issuer, player, BanType.SteamID, duration, reason, global);
        else BanOffline(issuer, steamId, null, BanType.SteamID, duration, reason, global);
    }

    // An IP ban kicks everyone connected from the address, so each of them has to be targetable.
    private void BanIp(Issuer issuer, string address, TimeSpan duration, string reason, bool global)
    {
        if (!Net.IsIp(address))
        {
            _messenger.Reply(issuer, "command.invalid_ip", address);
            return;
        }

        var ip = Net.NormalizeIp(address);
        var players = _targets.OnIp(ip).Where(p => !_ipBanExcluded.Contains(p.SteamID)).ToList();

        if (players.FirstOrDefault(p => !_access.CanTarget(issuer.Player, p.SteamID)) is { } immune)
        {
            _messenger.Reply(issuer, "command.target_has_immunity", Names.Of(immune), _access.GetImmunity(immune.SteamID));
            return;
        }

        var known = players.Count == 1 ? players[0] : null;
        BanOffline(issuer, known?.SteamID ?? 0, ip, BanType.IP, duration, reason, global, known is null ? null : Names.Of(known));
    }

    // unban lifts this server's own bans and globalunban the global ones, as ban and globalban gave them. Another
    // server's local ban is not this server's to lift.
    private void HandleUnban(ICommandContext context, string command, bool byIp, bool global)
    {
        var issuer = Issuer.From(context);
        var query = string.Join(' ', context.Args).Trim();

        if (query.Length == 0)
        {
            _messenger.Syntax(issuer, command, byIp ? "<ip>" : "<player|steamid64|ip>");
            return;
        }

        if (!_database.Enabled)
        {
            _messenger.Reply(issuer, "command.database_disabled");
            return;
        }

        if (byIp || Net.IsIp(query))
        {
            if (!Net.IsIp(query))
            {
                _messenger.Reply(issuer, "command.invalid_ip", query);
                return;
            }

            var ip = Net.NormalizeIp(query);
            UnbanFound(issuer, ip, global, () => FindApplicableActiveAsync(null, ip));
            return;
        }

        var player = _targets.FindOne(issuer.Player, query, out var steamId, out var error);

        if (error == TargetError.Ambiguous)
        {
            _messenger.ReportTargetError(issuer, error, query);
            return;
        }

        if (player is { SteamID: 0 })
        {
            _messenger.Reply(issuer, "command.target_no_steamid", Names.Of(player));
            return;
        }

        if (player is not null || error == TargetError.None)
        {
            var id = player?.SteamID ?? steamId;
            UnbanFound(issuer, player is null ? id.ToString() : Names.Of(player), global, () => FindApplicableActiveAsync(id, null));
            return;
        }

        // Nobody on the server: whoever was banned under that name.
        UnbanFound(issuer, query, global, async () =>
        {
            using var db = _database.Connect();
            return TargetResolver.Named(await db.SelectAsync(Here(RecordStatus.Active)).ConfigureAwait(false), query, b => b.PlayerName ?? string.Empty);
        }, byName: true);
    }

    /// <param name="find">The active bans that apply here, of both scopes; those of the other one only steer the reply.</param>
    private void UnbanFound(Issuer issuer, string display, bool global, Func<Task<IReadOnlyList<Ban>>> find, bool byName = false)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var found = await find().ConfigureAwait(false);
                var bans = found.Where(b => b.GlobalBan == global).ToList();

                if (byName)
                {
                    var players = bans.GroupBy(b => b.SteamId64 != 0 ? b.SteamId64.ToString() : Net.NormalizeIp(b.PlayerIp)).ToList();

                    if (players.Count > 1)
                    {
                        var list = string.Join(", ", players.Take(5).Select(p => $"{p.First().PlayerName} ({p.Key})"));
                        _messenger.ReplyLater(issuer, "command.multiple_records", display, list);
                        return;
                    }

                    if (players.Count == 1) display = $"{bans[0].PlayerName} ({players[0].Key})";
                }

                if (bans.Count == 0 && found.Count > 0)
                {
                    var other = global ? "unban" : "globalunban";
                    _messenger.ReplyLater(issuer, global ? "ban.unban_only_local" : "ban.unban_only_global", display, (issuer.Context?.Prefix ?? string.Empty) + other);
                    return;
                }

                var lifted = await LiftAsync(bans).ConfigureAwait(false);
                _messenger.ReplyLater(issuer, UnbanKey(lifted, global), issuer.Name, lifted, display);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unban {Target}.", display);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    private static string UnbanKey(int lifted, bool global) =>
        lifted == 0 ? global ? "ban.unban_none_global" : "ban.unban_none" : global ? "ban.unban_success_global" : "ban.unban_success";

    /// <summary>
    /// Lifts one ban picked from the menu. <paramref name="completed"/> runs on the game thread afterwards.
    /// </summary>
    public void Unban(Issuer issuer, Ban ban, Action? completed = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var lifted = await LiftAsync([ban]).ConfigureAwait(false);
                var display = ban.BanType == BanType.IP ? ban.PlayerIp : ban.SteamId64.ToString();
                _messenger.ReplyLater(issuer, UnbanKey(lifted, ban.GlobalBan), issuer.Name, lifted, display);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to lift ban #{Id}.", ban.Id);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
            finally
            {
                if (completed is not null) _core.Scheduler.NextTick(completed);
            }
        });
    }
}
