using Admins.Contract;
using Admins.Database.Models;
using Admins.Shared;
using Dommel;
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

    [Command("ungag", permission: "admins.commands.ungag")]
    public void UngagCommand(ICommandContext context) => HandleLift(context, "ungag", CommsAction.Gag, global: false);

    [Command("unmute", permission: "admins.commands.unmute")]
    public void UnmuteCommand(ICommandContext context) => HandleLift(context, "unmute", CommsAction.Mute, global: false);

    [Command("unsilence", permission: "admins.commands.unsilence")]
    public void UnsilenceCommand(ICommandContext context) => HandleLift(context, "unsilence", CommsAction.Silence, global: false);

    [Command("globalungag", permission: "admins.commands.globalungag")]
    public void GlobalUngagCommand(ICommandContext context) => HandleLift(context, "globalungag", CommsAction.Gag, global: true);

    [Command("globalunmute", permission: "admins.commands.globalunmute")]
    public void GlobalUnmuteCommand(ICommandContext context) => HandleLift(context, "globalunmute", CommsAction.Mute, global: true);

    [Command("globalunsilence", permission: "admins.commands.globalunsilence")]
    public void GlobalUnsilenceCommand(ICommandContext context) => HandleLift(context, "globalunsilence", CommsAction.Silence, global: true);

    private void HandleIssue(ICommandContext context, string command, CommsAction action, SanctionType type, bool global)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (args.Length == 0 && issuer.Player is { } viewer && _shortcuts.OpenComms is { } open)
        {
            open(viewer, action, type, global);
            return;
        }

        if (args.Length < 3)
        {
            _messenger.Syntax(issuer, command, type == SanctionType.IP ? "<ip> <time> <reason>" : "<player|steamid64|ip> <time> <reason>");
            return;
        }

        if (!Durations.TryParse(args[1], out var duration))
        {
            _messenger.Reply(issuer, "command.invalid_time_format", args[1]);
            return;
        }

        var reason = string.Join(' ', args.Skip(2)).Trim();

        // As for bans: an address first, so a name and an address are never mistaken for each other.
        if (type == SanctionType.IP || Net.IsIp(args[0]))
        {
            SanctionIp(issuer, args[0], action, duration, reason, global);
            return;
        }

        var player = _targets.FindOne(issuer.Player, args[0], out var steamId, out var error);
        if (_messenger.ReportTargetError(issuer, error, args[0])) return;

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

        if (player is not null) SanctionPlayer(issuer, player, action, SanctionType.SteamID, duration, reason, global);
        else SanctionOffline(issuer, steamId, null, action, SanctionType.SteamID, duration, reason, global);
    }

    // Reaches everyone connected from the address, so each of them has to be targetable.
    private void SanctionIp(Issuer issuer, string address, CommsAction action, TimeSpan duration, string reason, bool global)
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
        SanctionOffline(issuer, known?.SteamID ?? 0, ip, action, SanctionType.IP, duration, reason, global, known is null ? null : Names.Of(known));
    }

    // The un commands lift this server's own sanctions and the globalun ones the global sanctions. An address lifts every
    // sanction recorded with it; a player on the server, their SteamID's and those by IP on their address; a SteamID,
    // its own; anything else, the sanctions recorded under that name.
    private void HandleLift(ICommandContext context, string command, CommsAction action, bool global)
    {
        var issuer = Issuer.From(context);
        var query = string.Join(' ', context.Args).Trim();
        var scope = global ? LiftScope.Global : LiftScope.Local;

        if (query.Length == 0)
        {
            _messenger.Syntax(issuer, command, "<player|steamid64|ip>");
            return;
        }

        if (Net.IsIp(query))
        {
            var ip = Net.NormalizeIp(query);
            LiftFound(issuer, action, ip, scope, s => Net.NormalizeIp(s.PlayerIp) == ip, kinds => FindApplicableActiveAsync([(0, ip)], kinds, anyTypeOnIp: true));
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

        if (player is not null)
        {
            Lift(issuer, [(player.SteamID, Net.NormalizeIp(player.IPAddress))], action, Names.Of(player), scope);
        }
        else if (error == TargetError.None)
        {
            Lift(issuer, [(steamId, string.Empty)], action, steamId.ToString(), scope);
        }
        else if (!_database.Enabled)
        {
            Report(issuer, action, scope, [], query);
        }
        else
        {
            LiftFound(issuer, action, query, scope, _ => false, async kinds =>
            {
                using var db = _database.Connect();
                var active = await db.SelectAsync(Here(RecordStatus.Active, null)).ConfigureAwait(false);
                return TargetResolver.Named(active.Where(s => kinds.Contains(s.SanctionKind)), query, s => s.PlayerName ?? string.Empty);
            }, byName: true);
        }
    }

    /// <summary>
    /// Lifts the active sanctions of a kind for the given players: this server's, the global ones or both. Game thread.
    /// </summary>
    /// <param name="completed">Runs on the game thread once the lift went through; not on failure.</param>
    public void Lift(Issuer issuer, IReadOnlyList<(ulong SteamId, string Ip)> subjects, CommsAction action, string display, LiftScope scope, Action? completed = null) =>
        LiftFound(issuer, action, display, scope, s => subjects.Any(x => Matches(s, x.SteamId, x.Ip)), kinds => FindApplicableActiveAsync(subjects, kinds), completed: completed);

    /// <param name="cached">Picks from memory what <paramref name="find"/> would, for when there is no database.</param>
    /// <param name="find">The active sanctions of the kinds that apply here, of both scopes; those of the other scope only
    /// steer the reply.</param>
    private void LiftFound(
        Issuer issuer,
        CommsAction action,
        string display,
        LiftScope scope,
        Func<Sanction, bool> cached,
        Func<IReadOnlyList<SanctionKind>, Task<IReadOnlyList<Sanction>>> find,
        bool byName = false,
        Action? completed = null)
    {
        var kinds = KindsOf(action);

        if (!_database.Enabled)
        {
            var rows = _cache.Values
                .SelectMany(e => e.Sanctions)
                .Where(s => kinds.Contains(s.SanctionKind) && InScope(s, scope) && cached(s))
                .Distinct()
                .ToList();

            Forget(rows);
            Report(issuer, action, scope, rows, display);
            completed?.Invoke();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var found = await find(kinds).ConfigureAwait(false);
                var rows = found.Where(s => InScope(s, scope)).ToList();

                if (byName)
                {
                    var players = rows.GroupBy(s => s.SteamId64 != 0 ? s.SteamId64.ToString() : Net.NormalizeIp(s.PlayerIp)).ToList();

                    if (players.Count > 1)
                    {
                        var list = string.Join(", ", players.Take(5).Select(p => $"{p.First().PlayerName} ({p.Key})"));
                        _messenger.ReplyLater(issuer, "command.multiple_records", display, list);
                        return;
                    }

                    if (players.Count == 1) display = $"{rows[0].PlayerName} ({players[0].Key})";
                }

                var otherScopeOnly = rows.Count == 0 && found.Count > 0;
                await LiftAsync(rows).ConfigureAwait(false);

                _core.Scheduler.NextTick(() =>
                {
                    if (_messenger.IsConnected(issuer))
                    {
                        if (otherScopeOnly) ReportOtherScope(issuer, action, scope, display);
                        else Report(issuer, action, scope, rows, display);
                    }

                    completed?.Invoke();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to lift sanctions for {Target}.", display);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    private static bool InScope(Sanction sanction, LiftScope scope) => scope switch
    {
        LiftScope.Local => !sanction.GlobalSanction,
        LiftScope.Global => sanction.GlobalSanction,
        _ => true,
    };

    /// <summary>
    /// Lifts one sanction picked from the menu. <paramref name="completed"/> runs on the game thread afterwards.
    /// </summary>
    public void LiftOne(Issuer issuer, Sanction sanction, Action? completed = null)
    {
        var action = sanction.SanctionKind == SanctionKind.Gag ? CommsAction.Gag : CommsAction.Mute;
        var display = sanction.SanctionType == SanctionType.IP ? sanction.PlayerIp : sanction.SteamId64.ToString();
        var scope = sanction.GlobalSanction ? LiftScope.Global : LiftScope.Local;

        _ = Task.Run(async () =>
        {
            try
            {
                await LiftAsync([sanction]).ConfigureAwait(false);
                _core.Scheduler.NextTick(() =>
                {
                    if (_messenger.IsConnected(issuer)) Report(issuer, action, scope, [sanction], display);
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

    private void Report(Issuer issuer, CommsAction action, LiftScope scope, IReadOnlyList<Sanction> lifted, string display)
    {
        var label = ActionLabel(issuer.Player, action, global: scope == LiftScope.Global);

        if (lifted.Count == 0)
        {
            _messenger.Reply(issuer, "comms.lift_none", label, display);
            return;
        }

        _messenger.Reply(issuer, "comms.lifted", issuer.Name, lifted.Count, label, display);
    }

    // Only the other scope's sanctions matched: names the command that lifts them.
    private void ReportOtherScope(Issuer issuer, CommsAction action, LiftScope scope, string display)
    {
        var name = action.ToString().ToLowerInvariant();
        var command = (issuer.Context?.Prefix ?? string.Empty) + (scope == LiftScope.Global ? "un" : "globalun") + name;
        var key = scope == LiftScope.Global ? "comms.lift_only_local" : "comms.lift_only_global";
        _messenger.Reply(issuer, key, display, ActionLabel(issuer.Player, action, global: false), command);
    }
}
