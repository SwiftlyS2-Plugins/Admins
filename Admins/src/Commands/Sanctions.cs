using Admins.Contract;
using Admins.Database.Models;
using Admins.Sanctions;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using TimeSpanParserUtil;

namespace Admins.Commands;

public partial class AdminCommands
{
    [Command("gag", permission: "admins.commands.gag")]
    public void Command_Gag(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "gag", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        var time = context.Args[1];
        if (!TimeSpanParser.TryParse(time, out var duration))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_time_format", Admins.Config.CurrentValue.Prefix, time]);
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        var expiresAt = duration.TotalMilliseconds == 0 ? 0 : DateTimeOffset.UtcNow.Add(duration).ToUnixTimeMilliseconds();
        var adminName = context.IsSentByPlayer ? context.Sender!.Controller.PlayerName : "Console";

        foreach (var player in players)
        {
            var sanction = new Sanction
            {
                SteamId64 = player.SteamID,
                SanctionType = SanctionKind.Gag,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                Global = false
            };

            ServerSanctions.AddSanction(sanction);
        }

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string gagMessage = localizer[
                "gag.message",
                Admins.Config.CurrentValue.Prefix,
                adminName,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                reason
            ];
            return (gagMessage, MessageType.Chat);
        });
    }

    [Command("globalgag", permission: "admins.commands.globalgag")]
    public void Command_GlobalGag(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "globalgag", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        var time = context.Args[1];
        if (!TimeSpanParser.TryParse(time, out var duration))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_time_format", Admins.Config.CurrentValue.Prefix, time]);
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        var expiresAt = duration.TotalMilliseconds == 0 ? 0 : DateTimeOffset.UtcNow.Add(duration).ToUnixTimeMilliseconds();
        var adminName = context.IsSentByPlayer ? context.Sender!.Controller.PlayerName : "Console";

        foreach (var player in players)
        {
            var sanction = new Sanction
            {
                SteamId64 = player.SteamID,
                SanctionType = SanctionKind.Gag,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                Global = true
            };

            ServerSanctions.AddSanction(sanction);
        }

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string gagMessage = localizer[
                "gag.message",
                Admins.Config.CurrentValue.Prefix,
                adminName,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                reason
            ];
            return (gagMessage, MessageType.Chat);
        });
    }

    [Command("mute", permission: "admins.commands.mute")]
    public void Command_Mute(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "mute", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        var time = context.Args[1];
        if (!TimeSpanParser.TryParse(time, out var duration))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_time_format", Admins.Config.CurrentValue.Prefix, time]);
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        var expiresAt = duration.TotalMilliseconds == 0 ? 0 : DateTimeOffset.UtcNow.Add(duration).ToUnixTimeMilliseconds();
        var adminName = context.IsSentByPlayer ? context.Sender!.Controller.PlayerName : "Console";

        foreach (var player in players)
        {
            var sanction = new Sanction
            {
                SteamId64 = player.SteamID,
                SanctionType = SanctionKind.Mute,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                Global = false
            };

            ServerSanctions.AddSanction(sanction);
        }

        ServerSanctions.ScheduleCheck();

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string muteMessage = localizer[
                "mute.message",
                Admins.Config.CurrentValue.Prefix,
                adminName,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                reason
            ];
            return (muteMessage, MessageType.Chat);
        });
    }

    [Command("globalmute", permission: "admins.commands.globalmute")]
    public void Command_GlobalMute(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "globalmute", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        var time = context.Args[1];
        if (!TimeSpanParser.TryParse(time, out var duration))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_time_format", Admins.Config.CurrentValue.Prefix, time]);
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        var expiresAt = duration.TotalMilliseconds == 0 ? 0 : DateTimeOffset.UtcNow.Add(duration).ToUnixTimeMilliseconds();
        var adminName = context.IsSentByPlayer ? context.Sender!.Controller.PlayerName : "Console";

        foreach (var player in players)
        {
            var sanction = new Sanction
            {
                SteamId64 = player.SteamID,
                SanctionType = SanctionKind.Mute,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                Global = true
            };

            ServerSanctions.AddSanction(sanction);
        }

        ServerSanctions.ScheduleCheck();

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string muteMessage = localizer[
                "mute.message",
                Admins.Config.CurrentValue.Prefix,
                adminName,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                reason
            ];
            return (muteMessage, MessageType.Chat);
        });
    }
}