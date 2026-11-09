using Admins.Contract;
using Admins.Database.Models;
using Admins.Shared;
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
    public void UnbanCommand(ICommandContext context) => HandleUnban(context, "unban", byIp: false);

    [Command("unbanip", permission: "admins.commands.unban")]
    public void UnbanIpCommand(ICommandContext context) => HandleUnban(context, "unbanip", byIp: true);

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
            _messenger.Syntax(issuer, command, type == BanType.IP ? "<ip> <time> <reason>" : "<player|steamid64> <time> <reason>");
            return;
        }

        if (!Durations.TryParse(args[1], out var duration))
        {
            _messenger.Reply(issuer, "command.invalid_time_format", args[1]);
            return;
        }

        var reason = string.Join(' ', args.Skip(2)).Trim();

        if (type == BanType.IP)
        {
            BanIp(issuer, args[0], duration, reason, global);
            return;
        }

        var players = _targets.Find(issuer.Player, args[0], out var error, excludeCaller: true);

        if (players.Count > 0)
        {
            foreach (var player in players)
            {
                if (player.IsFakeClient) continue;

                if (!_access.CanTarget(issuer.Player, player.SteamID))
                {
                    _messenger.Reply(issuer, "command.target_has_immunity", Names.Of(player), _access.GetImmunity(player.SteamID));
                    continue;
                }

                BanPlayer(issuer, player, type, duration, reason, global);
            }

            return;
        }

        if (error == TargetError.Ambiguous)
        {
            _messenger.ReportTargetError(issuer, error, args[0]);
            return;
        }

        if (!SteamIds.TryParse(args[0], out var steamId))
        {
            _messenger.Reply(issuer, "command.player_not_found", args[0]);
            return;
        }

        if (!_access.CanTarget(issuer.Player, steamId))
        {
            _messenger.Reply(issuer, "command.target_has_immunity", steamId, _access.GetImmunity(steamId));
            return;
        }

        BanOffline(issuer, steamId, null, BanType.SteamID, duration, reason, global);
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
        var players = _targets.OnIp(ip);

        if (players.FirstOrDefault(p => !_access.CanTarget(issuer.Player, p.SteamID)) is { } immune)
        {
            _messenger.Reply(issuer, "command.target_has_immunity", Names.Of(immune), _access.GetImmunity(immune.SteamID));
            return;
        }

        var known = players.Count == 1 ? players[0] : null;
        BanOffline(issuer, known?.SteamID ?? 0, ip, BanType.IP, duration, reason, global, known is null ? null : Names.Of(known));
    }

    // Lifts only bans that apply here; another server's local ban is not this server's to remove.
    private void HandleUnban(ICommandContext context, string command, bool byIp)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (args.Length < 1)
        {
            _messenger.Syntax(issuer, command, byIp ? "<ip>" : "<steamid64>");
            return;
        }

        ulong? steamId = null;
        string? ip = null;

        if (byIp)
        {
            if (!Net.IsIp(args[0]))
            {
                _messenger.Reply(issuer, "command.invalid_ip", args[0]);
                return;
            }

            ip = Net.NormalizeIp(args[0]);
        }
        else
        {
            if (!SteamIds.TryParse(args[0], out var parsed))
            {
                _messenger.Reply(issuer, "command.invalid_steamid", args[0]);
                return;
            }

            steamId = parsed;
        }

        if (!_database.Enabled)
        {
            _messenger.Reply(issuer, "command.database_disabled");
            return;
        }

        var display = ip ?? steamId.ToString()!;

        _ = Task.Run(async () =>
        {
            try
            {
                var lifted = await LiftAsync(await FindApplicableActiveAsync(steamId, ip).ConfigureAwait(false)).ConfigureAwait(false);
                _messenger.ReplyLater(issuer, lifted > 0 ? "ban.unban_success" : "ban.unban_none", issuer.Name, lifted, display);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unban {Target}.", display);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

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
                _messenger.ReplyLater(issuer, lifted > 0 ? "ban.unban_success" : "ban.unban_none", issuer.Name, lifted, display);
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
