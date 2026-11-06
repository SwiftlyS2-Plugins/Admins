using Admins.Contract;
using Admins.Database.Models;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;

namespace Admins.Services.Comms;

public sealed partial class CommsService
{
    [Command("gag", permission: "admins.commands.gag")]
    public void GagCommand(ICommandContext context) => HandleIssue(context, "gag", CommsAction.Gag, SanctionType.SteamID, global: false);

    [Command("globalgag", permission: "admins.commands.globalgag")]
    public void GlobalGagCommand(ICommandContext context) => HandleIssue(context, "globalgag", CommsAction.Gag, SanctionType.SteamID, global: true);

    [Command("mute", permission: "admins.commands.mute")]
    public void MuteCommand(ICommandContext context) => HandleIssue(context, "mute", CommsAction.Mute, SanctionType.SteamID, global: false);

    [Command("globalmute", permission: "admins.commands.globalmute")]
    public void GlobalMuteCommand(ICommandContext context) => HandleIssue(context, "globalmute", CommsAction.Mute, SanctionType.SteamID, global: true);

    [Command("silence", permission: "admins.commands.silence")]
    public void SilenceCommand(ICommandContext context) => HandleIssue(context, "silence", CommsAction.Silence, SanctionType.SteamID, global: false);

    [Command("globalsilence", permission: "admins.commands.globalsilence")]
    public void GlobalSilenceCommand(ICommandContext context) => HandleIssue(context, "globalsilence", CommsAction.Silence, SanctionType.SteamID, global: true);

    // The "o" variants used to be the only way to reach an offline SteamID; the plain commands accept one now too.
    [Command("gago", permission: "admins.commands.gag")]
    public void GagOfflineCommand(ICommandContext context) => HandleIssue(context, "gago", CommsAction.Gag, SanctionType.SteamID, global: false);

    [Command("globalgago", permission: "admins.commands.globalgag")]
    public void GlobalGagOfflineCommand(ICommandContext context) => HandleIssue(context, "globalgago", CommsAction.Gag, SanctionType.SteamID, global: true);

    [Command("muteo", permission: "admins.commands.mute")]
    public void MuteOfflineCommand(ICommandContext context) => HandleIssue(context, "muteo", CommsAction.Mute, SanctionType.SteamID, global: false);

    [Command("globalmuteo", permission: "admins.commands.globalmute")]
    public void GlobalMuteOfflineCommand(ICommandContext context) => HandleIssue(context, "globalmuteo", CommsAction.Mute, SanctionType.SteamID, global: true);

    [Command("silenceo", permission: "admins.commands.silence")]
    public void SilenceOfflineCommand(ICommandContext context) => HandleIssue(context, "silenceo", CommsAction.Silence, SanctionType.SteamID, global: false);

    [Command("globalsilenceo", permission: "admins.commands.globalsilence")]
    public void GlobalSilenceOfflineCommand(ICommandContext context) => HandleIssue(context, "globalsilenceo", CommsAction.Silence, SanctionType.SteamID, global: true);

    [Command("gagip", permission: "admins.commands.gag")]
    public void GagIpCommand(ICommandContext context) => HandleIssue(context, "gagip", CommsAction.Gag, SanctionType.IP, global: false);

    [Command("globalgagip", permission: "admins.commands.globalgag")]
    public void GlobalGagIpCommand(ICommandContext context) => HandleIssue(context, "globalgagip", CommsAction.Gag, SanctionType.IP, global: true);

    [Command("muteip", permission: "admins.commands.mute")]
    public void MuteIpCommand(ICommandContext context) => HandleIssue(context, "muteip", CommsAction.Mute, SanctionType.IP, global: false);

    [Command("globalmuteip", permission: "admins.commands.globalmute")]
    public void GlobalMuteIpCommand(ICommandContext context) => HandleIssue(context, "globalmuteip", CommsAction.Mute, SanctionType.IP, global: true);

    [Command("silenceip", permission: "admins.commands.silence")]
    public void SilenceIpCommand(ICommandContext context) => HandleIssue(context, "silenceip", CommsAction.Silence, SanctionType.IP, global: false);

    [Command("globalsilenceip", permission: "admins.commands.globalsilence")]
    public void GlobalSilenceIpCommand(ICommandContext context) => HandleIssue(context, "globalsilenceip", CommsAction.Silence, SanctionType.IP, global: true);

    [Command("gagipo", permission: "admins.commands.gag")]
    public void GagIpOfflineCommand(ICommandContext context) => HandleIssue(context, "gagipo", CommsAction.Gag, SanctionType.IP, global: false);

    [Command("globalgagipo", permission: "admins.commands.globalgag")]
    public void GlobalGagIpOfflineCommand(ICommandContext context) => HandleIssue(context, "globalgagipo", CommsAction.Gag, SanctionType.IP, global: true);

    [Command("muteipo", permission: "admins.commands.mute")]
    public void MuteIpOfflineCommand(ICommandContext context) => HandleIssue(context, "muteipo", CommsAction.Mute, SanctionType.IP, global: false);

    [Command("globalmuteipo", permission: "admins.commands.globalmute")]
    public void GlobalMuteIpOfflineCommand(ICommandContext context) => HandleIssue(context, "globalmuteipo", CommsAction.Mute, SanctionType.IP, global: true);

    [Command("silenceipo", permission: "admins.commands.silence")]
    public void SilenceIpOfflineCommand(ICommandContext context) => HandleIssue(context, "silenceipo", CommsAction.Silence, SanctionType.IP, global: false);

    [Command("globalsilenceipo", permission: "admins.commands.globalsilence")]
    public void GlobalSilenceIpOfflineCommand(ICommandContext context) => HandleIssue(context, "globalsilenceipo", CommsAction.Silence, SanctionType.IP, global: true);

    [Command("ungag", permission: "admins.commands.ungag")]
    public void UngagCommand(ICommandContext context) => HandleLift(context, "ungag", CommsAction.Gag);

    [Command("unmute", permission: "admins.commands.unmute")]
    public void UnmuteCommand(ICommandContext context) => HandleLift(context, "unmute", CommsAction.Mute);

    [Command("unsilence", permission: "admins.commands.unsilence")]
    public void UnsilenceCommand(ICommandContext context) => HandleLift(context, "unsilence", CommsAction.Silence);

    private void HandleIssue(ICommandContext context, string command, CommsAction action, SanctionType type, bool global)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (args.Length == 0 && issuer.Player is { } viewer && _shortcuts.OpenComms is { } open)
        {
            open(viewer, action, global);
            return;
        }

        if (args.Length < 3)
        {
            _messenger.Syntax(issuer, command, type == SanctionType.IP ? "<player|ip> <time> <reason>" : "<player|steamid64> <time> <reason>");
            return;
        }

        if (!Durations.TryParse(args[1], out var duration))
        {
            _messenger.Reply(issuer, "command.invalid_time_format", args[1]);
            return;
        }

        var reason = string.Join(' ', args.Skip(2)).Trim();
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

                SanctionPlayer(issuer, player, action, type, duration, reason, global);
            }

            return;
        }

        if (error == TargetError.Ambiguous)
        {
            _messenger.ReportTargetError(issuer, error, args[0]);
            return;
        }

        if (type == SanctionType.IP)
        {
            if (!Net.IsIp(args[0]))
            {
                _messenger.Reply(issuer, "command.player_not_found", args[0]);
                return;
            }

            SanctionOffline(issuer, 0, args[0], action, SanctionType.IP, duration, reason, global);
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

        SanctionOffline(issuer, steamId, null, action, SanctionType.SteamID, duration, reason, global);
    }

    // Lifts SteamID rows and, for an online target, IP rows on their current address. Rows from another
    // server's local sanction are left alone.
    private void HandleLift(ICommandContext context, string command, CommsAction action)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (args.Length < 1)
        {
            _messenger.Syntax(issuer, command, "<player|steamid64>");
            return;
        }

        List<(ulong SteamId, string Ip)> subjects;
        var players = _targets.Find(issuer.Player, args[0], out var error);

        if (players.Count > 0)
        {
            subjects = [.. players.Where(p => !p.IsFakeClient).Select(p => (p.SteamID, Net.NormalizeIp(p.IPAddress)))];
        }
        else if (error == TargetError.Ambiguous)
        {
            _messenger.ReportTargetError(issuer, error, args[0]);
            return;
        }
        else if (SteamIds.TryParse(args[0], out var steamId))
        {
            subjects = [(steamId, string.Empty)];
        }
        else
        {
            _messenger.Reply(issuer, "command.player_not_found", args[0]);
            return;
        }

        Lift(issuer, subjects, action, args[0]);
    }

    /// <summary>
    /// Lifts the active sanctions of a kind for the given players. Game thread.
    /// </summary>
    public void Lift(Issuer issuer, IReadOnlyList<(ulong SteamId, string Ip)> subjects, CommsAction action, string display)
    {
        var kinds = KindsOf(action);

        if (!_database.Enabled)
        {
            var cached = _cache.Values
                .SelectMany(e => e.Sanctions)
                .Where(s => kinds.Contains(s.SanctionKind) && subjects.Any(x => Matches(s, x.SteamId, x.Ip)))
                .Distinct()
                .ToList();

            Forget(cached);
            Report(issuer, action, cached, display);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var rows = await FindApplicableActiveAsync(subjects, kinds).ConfigureAwait(false);
                await LiftAsync(rows).ConfigureAwait(false);
                _core.Scheduler.NextTick(() =>
                {
                    if (_messenger.IsConnected(issuer)) Report(issuer, action, rows, display);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to lift sanctions for {Target}.", display);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    /// <summary>
    /// Lifts one sanction picked from the menu. <paramref name="completed"/> runs on the game thread afterwards.
    /// </summary>
    public void LiftOne(Issuer issuer, Sanction sanction, Action? completed = null)
    {
        var action = sanction.SanctionKind == SanctionKind.Gag ? CommsAction.Gag : CommsAction.Mute;
        var display = sanction.SanctionType == SanctionType.IP ? sanction.PlayerIp : sanction.SteamId64.ToString();

        _ = Task.Run(async () =>
        {
            try
            {
                await LiftAsync([sanction]).ConfigureAwait(false);
                _core.Scheduler.NextTick(() =>
                {
                    if (_messenger.IsConnected(issuer)) Report(issuer, action, [sanction], display);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to lift sanction #{Id}.", sanction.Id);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
            finally
            {
                if (completed is not null) _core.Scheduler.NextTick(completed);
            }
        });
    }

    private void Report(Issuer issuer, CommsAction action, IReadOnlyList<Sanction> lifted, string display)
    {
        var label = ActionLabel(issuer.Player, action, global: false);

        if (lifted.Count == 0)
        {
            _messenger.Reply(issuer, "comms.lift_none", label, display);
            return;
        }

        _messenger.Reply(issuer, "comms.lifted", issuer.Name, lifted.Count, label, display);
    }
}
