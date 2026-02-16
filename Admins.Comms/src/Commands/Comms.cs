using Admins.Comms.Contract;
using Admins.Comms.Database.Models;
using Admins.Comms.Manager;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace Admins.Comms.Commands;

public partial class ServerCommands
{
    [Command("gag", permission: "admins.commands.gag")]
    public void Command_Gag(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "gag", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: false);
    }

    [Command("globalgag", permission: "admins.commands.globalgag")]
    public void Command_GlobalGag(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalgag", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: true);
    }

    [Command("mute", permission: "admins.commands.mute")]
    public void Command_Mute(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "mute", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalmute", permission: "admins.commands.globalmute")]
    public void Command_GlobalMute(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalmute", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    [Command("silence", permission: "admins.commands.silence")]
    public void Command_Silence(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "silence", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: false);
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalsilence", permission: "admins.commands.globalsilence")]
    public void Command_GlobalSilence(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalsilence", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: true);
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    [Command("ungag", permission: "admins.commands.ungag")]
    public void Command_Ungag(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 1, "ungag", ["<player|steamid64>"]))
        {
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);

        if (players != null && players.Any())
        {
            RemoveSanctions([.. players], context, SanctionKind.Gag, SanctionType.SteamID);
        }
        else if (TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            RemoveSanctionBySteamID(context, (long)steamId64, SanctionKind.Gag);
        }
        else
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_target", ConfigurationManager.GetCurrentConfiguration()!.Prefix, context.Args[0]]);
        }
    }

    [Command("unmute", permission: "admins.commands.unmute")]
    public void Command_Unmute(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 1, "unmute", ["<player|steamid64>"]))
        {
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);

        if (players != null && players.Any())
        {
            RemoveSanctions([.. players], context, SanctionKind.Mute, SanctionType.SteamID);
            gamePlayer.ScheduleCheck();
        }
        else if (TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            RemoveSanctionBySteamID(context, (long)steamId64, SanctionKind.Mute);
        }
        else
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_target", ConfigurationManager.GetCurrentConfiguration()!.Prefix, context.Args[0]]);
        }
    }

    [Command("unsilence", permission: "admins.commands.unsilence")]
    public void Command_Unsilence(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 1, "unsilence", ["<player|steamid64>"]))
        {
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);

        if (players != null && players.Any())
        {
            var playerList = players.ToList();
            var gagCount = RemoveSanctionsInternal(playerList, SanctionType.SteamID, SanctionKind.Gag);
            var muteCount = RemoveSanctionsInternal(playerList, SanctionType.SteamID, SanctionKind.Mute);
            gamePlayer.ScheduleCheck();

            var localizer = GetPlayerLocalizer(context);
            var adminName = GetAdminName(context);
            var message = localizer[
                "command.unsilence_success",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                adminName,
                gagCount,
                muteCount,
                playerList.Count
            ];
            context.Reply(message);
        }
        else if (TryParseSteamID(context, context.Args[0], out var steamId64))
        {
            RemoveSanctionBySteamID(context, (long)steamId64, SanctionKind.Gag);
            RemoveSanctionBySteamID(context, (long)steamId64, SanctionKind.Mute);
        }
        else
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_target", ConfigurationManager.GetCurrentConfiguration()!.Prefix, context.Args[0]]);
        }
    }

    [Command("gago", permission: "admins.commands.gag")]
    public void Command_GagOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "gago", ["<steamid64>", "<time>", "<reason>"]))
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
        ApplyOfflineSanction(context, steamId64, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: false);
    }

    [Command("muteo", permission: "admins.commands.mute")]
    public void Command_MuteOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "muteo", ["<steamid64>", "<time>", "<reason>"]))
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
        ApplyOfflineSanction(context, steamId64, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalgago", permission: "admins.commands.globalgag")]
    public void Command_GlobalGagOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalgago", ["<steamid64>", "<time>", "<reason>"]))
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
        ApplyOfflineSanction(context, steamId64, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: true);
    }

    [Command("globalmuteo", permission: "admins.commands.globalmute")]
    public void Command_GlobalMuteOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalmuteo", ["<steamid64>", "<time>", "<reason>"]))
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
        ApplyOfflineSanction(context, steamId64, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    [Command("silenceo", permission: "admins.commands.silence")]
    public void Command_SilenceOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "silenceo", ["<steamid64>", "<time>", "<reason>"]))
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
        ApplyOfflineSanction(context, steamId64, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: false);
        ApplyOfflineSanction(context, steamId64, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalsilenceo", permission: "admins.commands.globalsilence")]
    public void Command_GlobalSilenceOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalsilenceo", ["<steamid64>", "<time>", "<reason>"]))
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
        ApplyOfflineSanction(context, steamId64, SanctionKind.Gag, SanctionType.SteamID, duration, reason, isGlobal: true);
        ApplyOfflineSanction(context, steamId64, SanctionKind.Mute, SanctionType.SteamID, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    [Command("gagip", permission: "admins.commands.gag")]
    public void Command_GagIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "gagip", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.IP, duration, reason, isGlobal: false);
    }

    [Command("globalgagip", permission: "admins.commands.globalgag")]
    public void Command_GlobalGagIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalgagip", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.IP, duration, reason, isGlobal: true);
    }

    [Command("muteip", permission: "admins.commands.mute")]
    public void Command_MuteIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "muteip", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.IP, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalmuteip", permission: "admins.commands.globalmute")]
    public void Command_GlobalMuteIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalmuteip", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.IP, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    [Command("silenceip", permission: "admins.commands.silence")]
    public void Command_SilenceIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "silenceip", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.IP, duration, reason, isGlobal: false);
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.IP, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalsilenceip", permission: "admins.commands.globalsilence")]
    public void Command_GlobalSilenceIp(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalsilenceip", ["<player>", "<time>", "<reason>"]))
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
        ApplySanction(players, context, SanctionKind.Gag, SanctionType.IP, duration, reason, isGlobal: true);
        ApplySanction(players, context, SanctionKind.Mute, SanctionType.IP, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    [Command("gagipo", permission: "admins.commands.gag")]
    public void Command_GagIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "gagipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Gag, duration, reason, isGlobal: false);
    }

    [Command("globalgagipo", permission: "admins.commands.globalgag")]
    public void Command_GlobalGagIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalgagipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Gag, duration, reason, isGlobal: true);
    }

    [Command("muteipo", permission: "admins.commands.mute")]
    public void Command_MuteIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "muteipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Mute, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalmuteipo", permission: "admins.commands.globalmute")]
    public void Command_GlobalMuteIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalmuteipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Mute, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    [Command("silenceipo", permission: "admins.commands.silence")]
    public void Command_SilenceIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "silenceipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Gag, duration, reason, isGlobal: false);
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Mute, duration, reason, isGlobal: false);
        gamePlayer.ScheduleCheck();
    }

    [Command("globalsilenceipo", permission: "admins.commands.globalsilence")]
    public void Command_GlobalSilenceIpOffline(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 3, "globalsilenceipo", ["<ip_address>", "<time>", "<reason>"]))
        {
            return;
        }

        var ipAddress = context.Args[0];

        if (!TryParseDuration(context, context.Args[1], out var duration))
        {
            return;
        }

        var reason = string.Join(" ", context.Args.Skip(2));
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Gag, duration, reason, isGlobal: true);
        ApplyOfflineIpSanction(context, ipAddress, SanctionKind.Mute, duration, reason, isGlobal: true);
        gamePlayer.ScheduleCheck();
    }

    public void ApplySanction(
        List<IPlayer> players,
        ICommandContext context,
        SanctionKind sanctionKind,
        SanctionType sanctionType,
        TimeSpan duration,
        string reason,
        bool isGlobal)
    {
        var expiresAt = CalculateExpiresAt(duration);
        var adminName = GetAdminName(context);

        foreach (var player in players)
        {
            var sanction = new Sanction
            {
                SteamId64 = (long)player.SteamID,
                SanctionKind = sanctionKind,
                SanctionType = sanctionType,
                Reason = reason,
                PlayerName = player.Controller.PlayerName,
                PlayerIp = player.IPAddress,
                ExpiresAt = expiresAt,
                Length = (long)duration.TotalMilliseconds,
                AdminSteamId64 = context.IsSentByPlayer ? (long)context.Sender!.SteamID : 0,
                AdminName = adminName,
                Server = ServerManager.GetServerGUID(),
                GlobalSanction = isGlobal
            };

            CommsManager.AddSanction(sanction);
        }

        NotifySanctionApplied(players, context.Sender, sanctionKind, expiresAt, adminName, reason);
    }

    public void NotifySanctionApplied(
        List<IPlayer> players,
        IPlayer? sender,
        SanctionKind sanctionKind,
        long expiresAt,
        string adminName,
        string reason)
    {
        var messageKey = sanctionKind == SanctionKind.Gag ? "gag.message" : "mute.message";

        SendMessageToPlayers(players, sender, (player, localizer) =>
        {
            var expiryText = expiresAt == 0
                ? localizer["never"]
                : FormatTimestampInTimeZone(expiresAt);

            var message = localizer[
                messageKey,
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                adminName,
                expiryText,
                reason
            ];

            return (message, MessageType.Chat);
        });
    }

    public void RemoveSanctions(
        List<IPlayer> players,
        ICommandContext context,
        SanctionKind sanctionKind,
        SanctionType sanctionType
    )
    {
        var removedCount = RemoveSanctionsInternal(players, sanctionType, sanctionKind);

        var localizer = GetPlayerLocalizer(context);
        var adminName = GetAdminName(context);
        var messageKey = sanctionKind == SanctionKind.Gag ? "command.ungag_success" : "command.unmute_success";
        var message = localizer[
            messageKey,
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            adminName,
            removedCount,
            players.Count
        ];
        context.Reply(message);
    }

    private int RemoveSanctionsInternal(
        List<IPlayer> players,
        SanctionType sanctionType,
        SanctionKind sanctionKind
    )
    {
        var removedCount = 0;

        foreach (var player in players)
        {
            var playerSanctions = ServerComms.OnlinePlayerSanctions.TryGetValue(player.SteamID, out var cached)
                ? cached : [];
            var sanctions = playerSanctions.Where(s =>
                s.SanctionType == sanctionType && s.SanctionKind == sanctionKind
            ).ToList();

            foreach (var sanction in sanctions)
            {
                CommsManager.RemoveSanction(sanction);
                removedCount++;
            }
        }

        return removedCount;
    }

    public void ApplyOfflineSanction(
        ICommandContext context,
        ulong steamId64,
        SanctionKind sanctionKind,
        SanctionType sanctionType,
        TimeSpan duration,
        string reason,
        bool isGlobal)
    {
        var expiresAt = CalculateExpiresAt(duration);
        var adminName = GetAdminName(context);

        var sanction = new Sanction
        {
            SteamId64 = (long)steamId64,
            SanctionType = sanctionType,
            SanctionKind = sanctionKind,
            Reason = reason,
            PlayerName = "Unknown",
            PlayerIp = "",
            ExpiresAt = expiresAt,
            Length = (long)duration.TotalMilliseconds,
            AdminSteamId64 = context.IsSentByPlayer ? (long)context.Sender!.SteamID : 0,
            AdminName = adminName,
            Server = ServerManager.GetServerGUID(),
            GlobalSanction = isGlobal
        };

        CommsManager.AddSanction(sanction);

        var localizer = GetPlayerLocalizer(context);
        var messageKey = sanctionKind == SanctionKind.Gag ? "command.gago_success" : "command.muteo_success";
        var expiryText = expiresAt == 0
            ? localizer["never"]
            : FormatTimestampInTimeZone(expiresAt);

        var sanctionTypeKey = sanctionKind == SanctionKind.Gag
            ? (isGlobal ? "global_gag" : "gag")
            : (isGlobal ? "global_mute" : "mute");

        var sanctionTypeText = localizer[sanctionTypeKey];

        var message = localizer[
            messageKey,
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            adminName,
            sanctionTypeText,
            steamId64,
            expiryText,
            reason
        ];
        context.Reply(message);
    }

    public void ApplyOfflineIpSanction(
        ICommandContext context,
        string ipAddress,
        SanctionKind sanctionKind,
        TimeSpan duration,
        string reason,
        bool isGlobal)
    {
        var expiresAt = CalculateExpiresAt(duration);
        var adminName = GetAdminName(context);

        var sanction = new Sanction
        {
            SteamId64 = 0,
            SanctionType = SanctionType.IP,
            SanctionKind = sanctionKind,
            Reason = reason,
            PlayerName = "Unknown",
            PlayerIp = ipAddress,
            ExpiresAt = expiresAt,
            Length = (long)duration.TotalMilliseconds,
            AdminSteamId64 = context.IsSentByPlayer ? (long)context.Sender!.SteamID : 0,
            AdminName = adminName,
            Server = ServerManager.GetServerGUID(),
            GlobalSanction = isGlobal
        };

        CommsManager.AddSanction(sanction);

        var localizer = GetPlayerLocalizer(context);
        var messageKey = sanctionKind == SanctionKind.Gag ? "command.gagipo_success" : "command.muteipo_success";
        var expiryText = expiresAt == 0
            ? localizer["never"]
            : FormatTimestampInTimeZone(expiresAt);

        var sanctionTypeKey = sanctionKind == SanctionKind.Gag
            ? (isGlobal ? "global_gag" : "gag")
            : (isGlobal ? "global_mute" : "mute");

        var sanctionTypeText = localizer[sanctionTypeKey];

        var message = localizer[
            messageKey,
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            adminName,
            sanctionTypeText,
            ipAddress,
            expiryText,
            reason
        ];
        context.Reply(message);
    }

    private void RemoveSanctionBySteamID(ICommandContext context, long steamId64, SanctionKind sanctionKind)
    {
        var adminName = GetAdminName(context);
        var sanctions = CommsManager.FindSanctions(steamId64, null, sanctionKind, SanctionType.SteamID);

        foreach (var sanction in sanctions)
        {
            CommsManager.RemoveSanction(sanction);
        }

        var localizer = GetPlayerLocalizer(context);
        var messageKey = sanctionKind == SanctionKind.Gag ? "command.ungag_success" : "command.unmute_success";
        var message = sanctions.Count > 0
            ? localizer[messageKey, ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, sanctions.Count, 1]
            : localizer["command.no_sanctions_found", ConfigurationManager.GetCurrentConfiguration()!.Prefix, steamId64];
        context.Reply(message);
    }
}