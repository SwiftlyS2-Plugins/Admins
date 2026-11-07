using System.Globalization;
using Admins.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.PlayerControl;

public sealed partial class PlayerControlService
{
    [Command("kick", permission: "admins.commands.kick")]
    public void KickCommand(ICommandContext context)
    {
        if (!Begin(context, "kick", "<player> [reason]", 1, out var issuer, out var args)) return;

        var reason = args.Length > 1 ? string.Join(' ', args.Skip(1)) : null;
        ForTargets(issuer, args[0], t => Kick(issuer, t, reason), excludeCaller: true);
    }

    [Command("slay", permission: "admins.commands.slay")]
    public void SlayCommand(ICommandContext context)
    {
        if (!Begin(context, "slay", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => Slay(issuer, t));
    }

    [Command("slap", permission: "admins.commands.slap")]
    public void SlapCommand(ICommandContext context)
    {
        if (!Begin(context, "slap", "<player> [damage]", 1, out var issuer, out var args)) return;

        var damage = 0;
        if (args.Length > 1 && !TryInt(issuer, args[1], "damage", 0, 100, out damage)) return;

        ForTargets(issuer, args[0], t => Slap(issuer, t, damage));
    }

    [Command("freeze", permission: "admins.commands.freeze")]
    public void FreezeCommand(ICommandContext context)
    {
        if (!Begin(context, "freeze", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => SetFrozen(issuer, t, true));
    }

    [Command("unfreeze", permission: "admins.commands.unfreeze")]
    public void UnfreezeCommand(ICommandContext context)
    {
        if (!Begin(context, "unfreeze", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => SetFrozen(issuer, t, false));
    }

    [Command("bury", permission: "admins.commands.bury")]
    public void BuryCommand(ICommandContext context)
    {
        if (!Begin(context, "bury", "<player> [depth]", 1, out var issuer, out var args)) return;

        var depth = 10f;
        if (args.Length > 1 && !TryFloat(issuer, args[1], "depth", 0.1f, 1000f, out depth)) return;

        ForTargets(issuer, args[0], t => Bury(issuer, t, -depth));
    }

    [Command("unbury", permission: "admins.commands.unbury")]
    public void UnburyCommand(ICommandContext context)
    {
        if (!Begin(context, "unbury", "<player> [depth]", 1, out var issuer, out var args)) return;

        var depth = 15f;
        if (args.Length > 1 && !TryFloat(issuer, args[1], "depth", 0.1f, 1000f, out depth)) return;

        ForTargets(issuer, args[0], t => Bury(issuer, t, depth));
    }

    [Command("blind", permission: "admins.commands.blind")]
    public void BlindCommand(ICommandContext context)
    {
        if (!Begin(context, "blind", "<player> <hold_seconds>", 2, out var issuer, out var args)) return;
        if (!TryFloat(issuer, args[1], "hold_seconds", 0.1f, 60f, out var hold)) return;

        ForTargets(issuer, args[0], t => SetBlind(issuer, t, hold));
    }

    [Command("unblind", permission: "admins.commands.unblind")]
    public void UnblindCommand(ICommandContext context)
    {
        if (!Begin(context, "unblind", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => SetBlind(issuer, t, 0f));
    }

    [Command("color", permission: "admins.commands.color")]
    public void ColorCommand(ICommandContext context) => HandleColor(context, "color", glow: false);

    [Command("glow", permission: "admins.commands.glow")]
    public void GlowCommand(ICommandContext context) => HandleColor(context, "glow", glow: true);

    private void HandleColor(ICommandContext context, string command, bool glow)
    {
        if (!Begin(context, command, "<player> <r> <g> <b> [a]", 4, out var issuer, out var args)) return;

        var a = 255;
        if (!TryInt(issuer, args[1], "r", 0, 255, out var r)
            || !TryInt(issuer, args[2], "g", 0, 255, out var g)
            || !TryInt(issuer, args[3], "b", 0, 255, out var b)
            || (args.Length > 4 && !TryInt(issuer, args[4], "a", 0, 255, out a)))
        {
            return;
        }

        ForTargets(issuer, args[0], t => SetColor(issuer, t, new Color(r, g, b, a), glow));
    }

    [Command("shake", permission: "admins.commands.shake")]
    public void ShakeCommand(ICommandContext context)
    {
        if (!Begin(context, "shake", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => Shake(issuer, t));
    }

    [Command("beacon", permission: "admins.commands.beacon")]
    public void BeaconCommand(ICommandContext context)
    {
        if (!TargetOrSelf(context, "beacon", out var issuer, out var query)) return;
        ForTargets(issuer, query, t => SetBeacon(issuer, t, !HasBeacon(t)));
    }

    [Command("drug", permission: "admins.commands.drug")]
    public void DrugCommand(ICommandContext context)
    {
        if (!Begin(context, "drug", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => SetDrug(issuer, t, !IsDrugged(t)));
    }

    [Command("noclip", permission: "admins.commands.noclip")]
    public void NoclipCommand(ICommandContext context)
    {
        if (!TargetOrSelf(context, "noclip", out var issuer, out var query)) return;
        ForTargets(issuer, query, t => SetNoclip(issuer, t, !IsNoclip(t)));
    }

    [Command("god", permission: "admins.commands.god")]
    public void GodCommand(ICommandContext context)
    {
        if (!Begin(context, "god", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => SetGod(issuer, t, !IsGod(t)));
    }

    [Command("setspeed", permission: "admins.commands.setspeed")]
    [CommandAlias("speed")]
    public void SetSpeedCommand(ICommandContext context)
    {
        if (!Begin(context, "setspeed", "<player> [multiplier]", 1, out var issuer, out var args)) return;
        var multiplier = 1f;
        if (args.Length > 1 && !TryFloat(issuer, args[1], "multiplier", 0.1f, 10f, out multiplier)) return;

        ForTargets(issuer, args[0], t => SetSpeed(issuer, t, multiplier));
    }

    [Command("setgravity", permission: "admins.commands.setgravity")]
    [CommandAlias("gravity")]
    public void SetGravityCommand(ICommandContext context)
    {
        if (!Begin(context, "setgravity", "<player> [multiplier]", 1, out var issuer, out var args)) return;
        var multiplier = 1f;
        if (args.Length > 1 && !TryFloat(issuer, args[1], "multiplier", 0.1f, 10f, out multiplier)) return;

        ForTargets(issuer, args[0], t => SetGravity(issuer, t, multiplier));
    }

    [Command("hp", permission: "admins.commands.hp")]
    [CommandAlias("health")]
    public void HealthCommand(ICommandContext context)
    {
        if (!Begin(context, "hp", "<player> <health> [armor] [helmet 0|1]", 2, out var issuer, out var args)) return;

        var armor = 0;
        var helmet = false;
        if (!TryInt(issuer, args[1], "health", 0, 100, out var health)) return;
        if (args.Length > 2 && !TryInt(issuer, args[2], "armor", 0, 100, out armor)) return;
        if (args.Length > 3) helmet = args[3] is "1" || args[3].Equals("true", StringComparison.OrdinalIgnoreCase);

        ForTargets(issuer, args[0], t => SetHealth(issuer, t, health, armor, helmet));
    }

    [Command("rename", permission: "admins.commands.rename")]
    public void RenameCommand(ICommandContext context)
    {
        if (!Begin(context, "rename", "<player> <new_name>", 2, out var issuer, out var args)) return;

        var players = _targets.Find(issuer.Player, args[0], out var error);
        if (_messenger.ReportTargetError(issuer, error, args[0])) return;

        // One name for several players would make them indistinguishable.
        if (players.Count > 1)
        {
            _messenger.Reply(issuer, "command.multiple_targets", args[0]);
            return;
        }

        Rename(issuer, players[0], string.Join(' ', args.Skip(1)).Trim());
    }

    [Command("givemoney", permission: "admins.commands.givemoney")]
    [CommandAlias("money")]
    public void GiveMoneyCommand(ICommandContext context)
    {
        if (!Begin(context, "givemoney", "<player> <amount>", 2, out var issuer, out var args)) return;
        if (!TryInt(issuer, args[1], "amount", 1, 16000, out var amount)) return;

        ForTargets(issuer, args[0], t => SetMoney(issuer, t, amount, add: true));
    }

    [Command("setmoney", permission: "admins.commands.setmoney")]
    public void SetMoneyCommand(ICommandContext context)
    {
        if (!Begin(context, "setmoney", "<player> <amount>", 2, out var issuer, out var args)) return;
        if (!TryInt(issuer, args[1], "amount", 0, 16000, out var amount)) return;

        ForTargets(issuer, args[0], t => SetMoney(issuer, t, amount, add: false));
    }

    [Command("respawn", permission: "admins.commands.respawn")]
    public void RespawnCommand(ICommandContext context)
    {
        if (!Begin(context, "respawn", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => Respawn(issuer, t));
    }

    [Command("hrespawn", permission: "admins.commands.hrespawn")]
    [CommandAlias("1up")]
    public void RespawnAtDeathCommand(ICommandContext context)
    {
        if (!Begin(context, "hrespawn", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => RespawnAtDeath(issuer, t));
    }

    [Command("swap", permission: "admins.commands.swap")]
    public void SwapCommand(ICommandContext context)
    {
        if (!Begin(context, "swap", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => Swap(issuer, t));
    }

    [Command("team", permission: "admins.commands.team")]
    public void TeamCommand(ICommandContext context)
    {
        if (!Begin(context, "team", "<player> <ct|t|spec|none>", 2, out var issuer, out var args)) return;

        Team? team = args[1].ToLowerInvariant() switch
        {
            "ct" or "counterterrorist" or "3" => Team.CT,
            "t" or "terrorist" or "2" => Team.T,
            "spec" or "spectator" or "1" => Team.Spectator,
            "none" or "0" => Team.None,
            _ => null,
        };

        if (team is not { } value)
        {
            _messenger.Reply(issuer, "command.team_invalid", args[1]);
            return;
        }

        ForTargets(issuer, args[0], t => MoveToTeam(issuer, t, value));
    }

    [Command("goto", permission: "admins.commands.goto")]
    public void GotoCommand(ICommandContext context)
    {
        if (!Begin(context, "goto", "<player>", 1, out var issuer, out var args)) return;
        if (Single(issuer, args[0]) is { } target) Goto(issuer, target);
    }

    [Command("bring", permission: "admins.commands.bring")]
    public void BringCommand(ICommandContext context)
    {
        if (!Begin(context, "bring", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => Bring(issuer, t), excludeCaller: true);
    }

    [Command("giveitem", permission: "admins.commands.giveitem")]
    [CommandAlias("give")]
    public void GiveItemCommand(ICommandContext context)
    {
        if (!Begin(context, "giveitem", "<player> <item_name>", 2, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => GiveItem(issuer, t, args[1]));
    }

    [Command("melee", permission: "admins.commands.melee")]
    public void MeleeCommand(ICommandContext context)
    {
        if (!Begin(context, "melee", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => Disarm(issuer, t, keepKnife: true));
    }

    [Command("disarm", permission: "admins.commands.disarm")]
    public void DisarmCommand(ICommandContext context)
    {
        if (!Begin(context, "disarm", "<player>", 1, out var issuer, out var args)) return;
        ForTargets(issuer, args[0], t => Disarm(issuer, t, keepKnife: false));
    }

    private bool Begin(ICommandContext context, string command, string usage, int minArgs, out Issuer issuer, out string[] args)
    {
        issuer = Issuer.From(context);
        args = context.Args;
        if (args.Length >= minArgs && !string.IsNullOrWhiteSpace(args[0])) return true;

        _messenger.Syntax(issuer, command, usage);
        return false;
    }

    // Bare from a player targets the sender, as the original noclip/beacon did.
    private bool TargetOrSelf(ICommandContext context, string command, out Issuer issuer, out string query)
    {
        issuer = Issuer.From(context);
        query = context.Args.Length > 0 ? context.Args[0] : string.Empty;
        if (!string.IsNullOrWhiteSpace(query)) return true;

        if (issuer.Player is not null)
        {
            query = "@me";
            return true;
        }

        _messenger.Syntax(issuer, command, "<player>");
        return false;
    }

    private void ForTargets(Issuer issuer, string query, Func<IPlayer, bool> action, bool excludeCaller = false)
    {
        var players = _targets.Find(issuer.Player, query, out var error, excludeCaller);
        if (_messenger.ReportTargetError(issuer, error, query)) return;

        foreach (var player in players)
        {
            action(player);
        }
    }

    private IPlayer? Single(Issuer issuer, string query)
    {
        var players = _targets.Find(issuer.Player, query, out var error, excludeCaller: true);
        if (_messenger.ReportTargetError(issuer, error, query)) return null;

        if (players.Count > 1)
        {
            _messenger.Reply(issuer, "command.multiple_targets", query);
            return null;
        }

        return players[0];
    }

    private bool TryInt(Issuer issuer, string value, string name, int min, int max, out int result)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result >= min && result <= max) return true;

        _messenger.Reply(issuer, "command.invalid_value_range", value, name, min, max);
        return false;
    }

    private bool TryFloat(Issuer issuer, string value, string name, float min, float max, out float result)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result >= min && result <= max) return true;

        _messenger.Reply(issuer, "command.invalid_value_range", value, name, min, max);
        return false;
    }
}
