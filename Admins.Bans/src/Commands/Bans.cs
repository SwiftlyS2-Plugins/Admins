using Admins.Bans.Contract;
using Admins.Bans.Database.Models;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Bans.Commands;

public partial class ServerCommands
{
    [Command("ban", permission: "admins.commands.ban")]
    public void Command_Ban(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "ban", ["<player|steamid64>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));

        if (players != null && players.Any())
        {
            ApplyBan(players.ToList(), context, BanType.SteamID, duration, reason, isGlobal: false);
            KickBannedPlayers(players.ToList());
        }
        else if (TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            // Check if this SteamID is currently online
            var onlinePlayer = GetOnlinePlayerBySteamID(steamId64);
            if (onlinePlayer != null)
            {
                ApplyBan([onlinePlayer], context, BanType.SteamID, duration, reason, isGlobal: false);
                KickBannedPlayers([onlinePlayer]);
            }
            else
            {
                ApplyOfflineBan(context, steamId64, null, BanType.SteamID, duration, reason, isGlobal: false);
            }
        }
        else
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_target", ConfigurationManager.GetCurrentConfiguration()!.Prefix, context.Args[0]]);
        }
    }

    [Command("globalban", permission: "admins.commands.globalban")]
    public void Command_GlobalBan(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalban", ["<player|steamid64>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));

        if (players != null && players.Any())
        {
            ApplyBan(players.ToList(), context, BanType.SteamID, duration, reason, isGlobal: true);
            KickBannedPlayers(players.ToList());
        }
        else if (TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            // Check if this SteamID is currently online
            var onlinePlayer = GetOnlinePlayerBySteamID(steamId64);
            if (onlinePlayer != null)
            {
                ApplyBan([onlinePlayer], context, BanType.SteamID, duration, reason, isGlobal: true);
                KickBannedPlayers([onlinePlayer]);
            }
            else
            {
                ApplyOfflineBan(context, steamId64, null, BanType.SteamID, duration, reason, isGlobal: true);
            }
        }
        else
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_target", ConfigurationManager.GetCurrentConfiguration()!.Prefix, context.Args[0]]);
        }
    }

    [Command("banip", permission: "admins.commands.ban")]
    public void Command_BanIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "banip", ["<player|ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));

        if (players != null && players.Any())
        {
            ApplyBan(players.ToList(), context, BanType.IP, duration, reason, isGlobal: false);
            KickBannedPlayers(players.ToList());
        }
        else
        {
            // Assume it's an IP address
            var ipAddress = context.Args[0];
            ApplyOfflineBan(context, 0, ipAddress, BanType.IP, duration, reason, isGlobal: false);
        }
    }

    [Command("globalbanip", permission: "admins.commands.globalban")]
    public void Command_GlobalBanIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalbanip", ["<player|ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));

        if (players != null && players.Any())
        {
            ApplyBan(players.ToList(), context, BanType.IP, duration, reason, isGlobal: true);
            KickBannedPlayers(players.ToList());
        }
        else
        {
            // Assume it's an IP address
            var ipAddress = context.Args[0];
            ApplyOfflineBan(context, 0, ipAddress, BanType.IP, duration, reason, isGlobal: true);
        }
    }

    [Command("unban", permission: "admins.commands.unban")]
    public void Command_Unban(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 1, "unban", ["<steamid64>"]))
        {
            return;
        }

        if (!TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            return;
        }

        RemoveBanBySteamID(context, (long)steamId64);
    }

    [Command("unbanip", permission: "admins.commands.unban")]
    public void Command_UnbanIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 1, "unbanip", ["<ip_address>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];
        RemoveBanByIP(context, ipAddress);
    }

    public void ApplyBan(
        List<IPlayer> players,
        ICommandContext context,
        BanType banType,
        TimeSpan duration,
        string reason,
        bool isGlobal)
    {
        var expiresAt = CalculateExpiresAt(duration);
        var adminName = GetAdminName(context);

        foreach (var player in players)
        {
            var ban = new Ban
            {
                SteamId64 = (long)player.SteamID,
                BanType = banType,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = expiresAt,
                Length = (long)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? (long)context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = ServerManager.GetServerGUID(),
                GlobalBan = isGlobal
            };

            BanManager.AddBan(ban);
        }

        NotifyBanApplied(players, context.Sender, expiresAt, adminName, reason);
    }

    public void NotifyBanApplied(
        List<IPlayer> players,
        IPlayer? sender,
        long expiresAt,
        string adminName,
        string reason)
    {
        SendMessageToPlayers(players, sender, (player, localizer) =>
        {
            var expiryText = expiresAt == 0
                ? localizer["never"]
                : FormatTimestampInTimeZone(expiresAt);

            var message = localizer[
                "ban.kick_message",
                reason,
                expiryText,
                adminName,
                sender?.SteamID.ToString() ?? "0"
            ];

            return (message, MessageType.Console);
        });
    }

    public void KickBannedPlayers(List<IPlayer> players)
    {
        Core.Scheduler.NextTick(() =>
        {
            foreach (var player in players)
            {
                player.Kick("Banned.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            }
        });
    }

    private void RemoveBanBySteamID(ICommandContext context, long steamId64)
    {
        var adminName = GetAdminName(context);
        var bans = BanManager.FindBans(steamId64);

        foreach (var ban in bans)
        {
            BanManager.RemoveBan(ban);
        }

        var localizer = GetPlayerLocalizer(context);
        var messageKey = bans.Count > 0 ? "command.unban_success" : "command.unban_none";
        var message = bans.Count > 0
            ? localizer[messageKey, ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, bans.Count, steamId64]
            : localizer[messageKey, ConfigurationManager.GetCurrentConfiguration()!.Prefix, steamId64];
        context.Reply(message);
    }

    private void RemoveBanByIP(ICommandContext context, string ipAddress)
    {
        var adminName = GetAdminName(context);
        var bans = BanManager.FindBans(null, ipAddress);

        foreach (var ban in bans)
        {
            BanManager.RemoveBan(ban);
        }

        var localizer = GetPlayerLocalizer(context);
        var messageKey = bans.Count > 0 ? "command.unbanip_success" : "command.unbanip_none";
        var message = bans.Count > 0
            ? localizer[messageKey, ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, bans.Count, ipAddress]
            : localizer[messageKey, ConfigurationManager.GetCurrentConfiguration()!.Prefix, ipAddress];
        context.Reply(message);
    }

    private IPlayer? GetOnlinePlayerBySteamID(ulong steamId64)
    {
        var allPlayers = Core.PlayerManager.GetAllPlayers();
        return allPlayers.FirstOrDefault(p => !p.IsFakeClient && p.IsValid && p.SteamID == steamId64);
    }

    private void ApplyOfflineBan(
        ICommandContext context,
        ulong steamId64,
        string? ipAddress,
        BanType banType,
        TimeSpan duration,
        string reason,
        bool isGlobal)
    {
        var expiresAt = CalculateExpiresAt(duration);
        var adminName = GetAdminName(context);

        var ban = new Ban
        {
            SteamId64 = (long)steamId64,
            BanType = banType,
            Reason = reason,
            PlayerName = "Unknown",
            PlayerIp = ipAddress ?? "",
            ExpiresAt = expiresAt,
            Length = (long)duration.TotalMilliseconds,
            AdminSteamId64 = context.IsSentByPlayer ? (long)context.Sender!.SteamID : 0,
            AdminName = adminName,
            Server = ServerManager.GetServerGUID(),
            GlobalBan = isGlobal
        };

        BanManager.AddBan(ban);

        var localizer = GetPlayerLocalizer(context);
        var expiryText = expiresAt == 0
            ? localizer["never"]
            : FormatTimestampInTimeZone(expiresAt);

        var messageKey = banType == BanType.SteamID ? "command.bano_success" : "command.banipo_success";
        var target = banType == BanType.SteamID ? $"SteamID64 [green]{steamId64}[default]" : $"IP [green]{ipAddress}[default]";
        var globalSuffix = isGlobal ? $"([green]{localizer["global"]}[default])" : "";
        var message = localizer[
            messageKey,
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            adminName,
            target,
            expiryText,
            globalSuffix,
            reason
        ];
        context.Reply(message);
    }
}