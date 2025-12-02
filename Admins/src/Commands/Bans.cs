using Admins.Bans;
using Admins.Contract;
using Admins.Database.Models;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using TimeSpanParserUtil;

namespace Admins.Commands;

public partial class AdminCommands
{
    [Command("ban", permission: "admins.commands.ban")]
    public void Command_Ban(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "ban", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.None);
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
            var sanction = new Ban
            {
                SteamId64 = player.SteamID,
                BanType = BanType.SteamID,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                GlobalBan = false
            };

            ServerBans.AddBan(sanction);
        }

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string banMessage = localizer[
                "ban.kick_message",
                reason,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                adminName,
                context.IsSentByPlayer ? context.Sender!.SteamID.ToString() : "0"
            ];
            return (banMessage, MessageType.Console);
        });

        Core.Scheduler.NextTick(() =>
        {
            foreach (var player in players)
            {
                player.Kick("Banned.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            }
        });
    }

    [Command("globalban", permission: "admins.commands.globalban")]
    public void Command_GlobalBan(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "globalban", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.None);
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
            var sanction = new Ban
            {
                SteamId64 = player.SteamID,
                BanType = BanType.SteamID,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                GlobalBan = true
            };

            ServerBans.AddBan(sanction);
        }

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string banMessage = localizer[
                "ban.kick_message",
                reason,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                adminName,
                context.IsSentByPlayer ? context.Sender!.SteamID.ToString() : "0"
            ];
            return (banMessage, MessageType.Console);
        });

        Core.Scheduler.NextTick(() =>
        {
            foreach (var player in players)
            {
                player.Kick("Banned.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            }
        });
    }

    [Command("banip", permission: "admins.commands.ban")]
    public void Command_BanIp(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "banip", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.None);
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
            var sanction = new Ban
            {
                SteamId64 = player.SteamID,
                BanType = BanType.IP,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                GlobalBan = false
            };

            ServerBans.AddBan(sanction);
        }

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string banMessage = localizer[
                "ban.kick_message",
                reason,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                adminName,
                context.IsSentByPlayer ? context.Sender!.SteamID.ToString() : "0"
            ];
            return (banMessage, MessageType.Console);
        });

        Core.Scheduler.NextTick(() =>
        {
            foreach (var player in players)
            {
                player.Kick("Banned.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            }
        });
    }

    [Command("globalbanip", permission: "admins.commands.globalban")]
    public void Command_GlobalBanIp(ICommandContext context)
    {
        if (context.Args.Length < 3)
        {
            SendSyntax(context, "globalbanip", ["<player>", "<time>", "<reason>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.None);
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
            var sanction = new Ban
            {
                SteamId64 = player.SteamID,
                BanType = BanType.IP,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = Admins.ServerGUID,
                GlobalBan = true
            };

            ServerBans.AddBan(sanction);
        }

        SendMessageToPlayers(players, context.IsSentByPlayer ? context.Sender! : null, (player, localizer) =>
        {
            string banMessage = localizer[
                "ban.kick_message",
                reason,
                expiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                adminName,
                context.IsSentByPlayer ? context.Sender!.SteamID.ToString() : "0"
            ];
            return (banMessage, MessageType.Console);
        });

        Core.Scheduler.NextTick(() =>
        {
            foreach (var player in players)
            {
                player.Kick("Banned.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            }
        });
    }
}