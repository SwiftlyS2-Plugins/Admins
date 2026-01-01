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
        if (!ValidateArgsCount(context, 3, "ban", ["<player>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyBan(players, context, BanType.SteamID, duration, reason, isGlobal: false);
        KickBannedPlayers(players);
    }

    [Command("globalban", permission: "admins.commands.globalban")]
    public void Command_GlobalBan(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalban", ["<player>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyBan(players, context, BanType.SteamID, duration, reason, isGlobal: true);
        KickBannedPlayers(players);
    }

    [Command("banip", permission: "admins.commands.ban")]
    public void Command_BanIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "banip", ["<player>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyBan(players, context, BanType.IP, duration, reason, isGlobal: false);
        KickBannedPlayers(players);
    }

    [Command("globalbanip", permission: "admins.commands.globalban")]
    public void Command_GlobalBanIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalbanip", ["<player>", "<time>", "<reason>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyBan(players, context, BanType.IP, duration, reason, isGlobal: true);
        KickBannedPlayers(players);
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

        RemoveBanBySteamID(context, steamId64);
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

    [Command("bano", permission: "admins.commands.ban")]
    public void Command_BanOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "bano", ["<steamid64>", "<time>", "<reason>"]))
        {
            return;
        }

        if (!TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            return;
        }

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineBan(context, steamId64, null, BanType.SteamID, duration, reason, isGlobal: false);
    }

    [Command("globalbano", permission: "admins.commands.globalban")]
    public void Command_GlobalBanOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalbano", ["<steamid64>", "<time>", "<reason>"]))
        {
            return;
        }

        if (!TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            return;
        }

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineBan(context, steamId64, null, BanType.SteamID, duration, reason, isGlobal: true);
    }

    [Command("banipo", permission: "admins.commands.ban")]
    public void Command_BanIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "banipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineBan(context, 0, ipAddress, BanType.IP, duration, reason, isGlobal: false);
    }

    [Command("globalbanipo", permission: "admins.commands.globalban")]
    public void Command_GlobalBanIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalbanipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineBan(context, 0, ipAddress, BanType.IP, duration, reason, isGlobal: true);
    }

    private void ApplyBan(
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
                SteamId64 = player.SteamID,
                BanType = banType,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = (ulong)expiresAt,
                Length = (ulong)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = ServerManager.GetServerGUID(),
                GlobalBan = isGlobal
            };

            BanManager.AddBan(ban);
        }

        NotifyBanApplied(players, context.Sender, expiresAt, adminName, reason);
    }

    private void NotifyBanApplied(
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
                : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt)
                    .ToString("yyyy-MM-dd HH:mm:ss");

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

    private void KickBannedPlayers(List<IPlayer> players)
    {
        Core.Scheduler.NextTick(() =>
        {
            foreach (var player in players)
            {
                player.Kick("Banned.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            }
        });
    }

    private void RemoveBanBySteamID(ICommandContext context, ulong steamId64)
    {
        var adminName = GetAdminName(context);
        var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var bans = BanManager.GetBans()
            .Where(b => b.SteamId64 == steamId64 &&
                       b.BanType == BanType.SteamID &&
                       (b.ExpiresAt == 0 || b.ExpiresAt > currentTime))
            .ToList();

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
        var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var bans = BanManager.GetBans()
            .Where(b => b.PlayerIp == ipAddress &&
                       b.BanType == BanType.IP &&
                       (b.ExpiresAt == 0 || b.ExpiresAt > currentTime))
            .ToList();

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
            SteamId64 = steamId64,
            BanType = banType,
            Reason = reason,
            PlayerName = "Unknown",
            PlayerIp = ipAddress ?? "",
            ExpiresAt = (ulong)expiresAt,
            Length = (ulong)duration.TotalMilliseconds,
            AdminSteamId64 = context.IsSentByPlayer ? context.Sender!.SteamID : 0,
            AdminName = adminName,
            Server = ServerManager.GetServerGUID(),
            GlobalBan = isGlobal
        };

        BanManager.AddBan(ban);

        var localizer = GetPlayerLocalizer(context);
        var expiryText = expiresAt == 0
            ? localizer["never"]
            : DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).ToString("yyyy-MM-dd HH:mm:ss");

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