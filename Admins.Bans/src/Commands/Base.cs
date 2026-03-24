using Admins.Bans.Manager;
using Admins.Core.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using SwiftlyS2.Shared.Translation;
using TimeSpanParserUtil;

namespace Admins.Bans.Commands;

public partial class ServerCommands
{
    private ISwiftlyCore Core = null!;
    private IConfigurationManager ConfigurationManager = null!;
    private IServerManager ServerManager = null!;
    private BansManager BanManager = null!;
    private IAdminsManager? AdminsManager = null!;
    private IGroupsManager? GroupsManager = null!;

    public ServerCommands(ISwiftlyCore core, BansManager bansManager)
    {
        Core = core;
        BanManager = bansManager;

        core.Registrator.Register(this);
    }

    public void SetConfigurationManager(IConfigurationManager configurationManager)
    {
        ConfigurationManager = configurationManager;
    }

    public void SetServerManager(IServerManager serverManager)
    {
        ServerManager = serverManager;
    }

    public void SetAdminsManager(IAdminsManager adminsManager)
    {
        AdminsManager = adminsManager;
    }

    public void SetGroupsManager(IGroupsManager groupsManager)
    {
        GroupsManager = groupsManager;
    }

    /// <summary>
    /// Sends command syntax help message to the command sender.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="cmdname">The command name.</param>
    /// <param name="arguments">Array of required arguments.</param>
    private void SendSyntax(ICommandContext context, string cmdname, string[] arguments)
    {
        var localizer = GetPlayerLocalizer(context);
        var syntax = localizer[
            "command.syntax",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            context.Prefix,
            cmdname,
            string.Join(" ", arguments)
        ];
        context.Reply(syntax);
    }

    /// <summary>
    /// Gets the appropriate localizer for the command context.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <returns>Player-specific localizer if sent by player, otherwise server localizer.</returns>
    private ILocalizer GetPlayerLocalizer(ICommandContext context)
    {
        return context.IsSentByPlayer
            ? Core.Translation.GetPlayerLocalizer(context.Sender!)
            : Core.Localizer;
    }

    /// <summary>
    /// Sends a message to multiple players and optionally the command sender.
    /// </summary>
    /// <param name="players">Target players to receive the message.</param>
    /// <param name="sender">The command sender (excluded from player list).</param>
    /// <param name="messageBuilder">Function to build the message for each player.</param>
    private void SendMessageToPlayers(
        IEnumerable<IPlayer> players,
        IPlayer? sender,
        Func<IPlayer, ILocalizer, (string message, MessageType type)> messageBuilder)
    {
        foreach (var player in players)
        {
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            var (message, type) = messageBuilder(player, localizer);

            player.SendMessage(type, message);

            if (sender != null && sender != player)
            {
                sender.SendMessage(type, message);
            }
        }
    }

    /// <summary>
    /// Validates that the command has the required number of arguments.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="requiredArgs">Number of required arguments.</param>
    /// <param name="cmdname">The command name.</param>
    /// <param name="arguments">Array of argument names for syntax help.</param>
    /// <returns>True if validation passes, false otherwise.</returns>
    private bool ValidateArgsCount(ICommandContext context, int requiredArgs, string cmdname, string[] arguments)
    {
        if (context.Args.Length < requiredArgs)
        {
            SendSyntax(context, cmdname, arguments);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Finds target players based on the target string.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="target">The target player identifier.</param>
    /// <returns>List of target players, or null if none found.</returns>
    private List<IPlayer>? FindTargetPlayers(ICommandContext context, string target)
    {
        var players = Core.PlayerManager.FindTargettedPlayers(
            context.Sender!,
            target,
            TargetSearchMode.IncludeSelf
        );

        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer[
                "command.player_not_found",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                target
            ]);
            return null;
        }

        return players.ToList();
    }

    /// <summary>
    /// Tries to parse a duration string into a TimeSpan.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="timeString">The time string to parse.</param>
    /// <param name="duration">The parsed duration.</param>
    /// <returns>True if parsing succeeds, false otherwise.</returns>
    private bool TryParseDuration(ICommandContext context, string timeString, out TimeSpan duration)
    {
        if (!TimeSpanParser.TryParse(timeString, out duration))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer[
                "command.invalid_time_format",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                timeString
            ]);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Tries to parse a SteamID64 string.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="steamIdString">The SteamID64 string to parse.</param>
    /// <param name="steamId64">The parsed SteamID64.</param>
    /// <returns>True if parsing succeeds, false otherwise.</returns>
    private bool TryParseSteamID(ICommandContext context, string steamIdString, out ulong steamId64)
    {
        var steamid = new CSteamID(steamIdString);
        if (!steamid.IsValid())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer[
                "command.invalid_steamid",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                steamIdString
            ]);

            steamId64 = 0;
            return false;
        }

        steamId64 = steamid.GetSteamID64();
        return true;
    }

    /// <summary>
    /// Gets the admin name from the command context.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <returns>Admin name or "Console" if sent by server.</returns>
    private string GetAdminName(ICommandContext context)
    {
        return context.IsSentByPlayer
            ? context.Sender!.Controller.PlayerName
            : "Console";
    }

    /// <summary>
    /// Calculates expiration timestamp from duration.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>Unix timestamp in milliseconds, or 0 for permanent.</returns>
    public long CalculateExpiresAt(TimeSpan duration)
    {
        return duration.TotalMilliseconds == 0
            ? 0
            : DateTimeOffset.UtcNow.Add(duration).ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Gets the configured time zone.
    /// </summary>
    /// <returns>The configured time zone.</returns>
    public TimeZoneInfo GetConfiguredTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ConfigurationManager.GetCurrentConfiguration()!.TimeZone);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Formats a Unix timestamp into a string in the configured time zone.
    /// </summary>
    /// <param name="unixTimeMilliseconds">The Unix timestamp in milliseconds.</param>
    /// <returns>The formatted timestamp string.</returns>
    public string FormatTimestampInTimeZone(long unixTimeMilliseconds)
    {
        var utcTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
        var timeZone = GetConfiguredTimeZone();
        var localTime = TimeZoneInfo.ConvertTime(utcTime, timeZone);
        return localTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Gets the immunity level of a player, checking both admin and group immunity.
    /// Returns the highest immunity level between the admin's direct immunity and their groups' immunity.
    /// </summary>
    /// <param name="player">The player to check immunity for.</param>
    /// <returns>The player's immunity level, or 0 if not an admin.</returns>
    public int GetPlayerImmunityLevel(IPlayer player)
    {
        // Return 0 if managers are not available
        if (AdminsManager == null || GroupsManager == null)
        {
            return 0;
        }

        // Check if player is an admin
        var admin = AdminsManager.GetAdmin(player);
        if (admin == null)
        {
            return 0;
        }

        int maxImmunity = admin.Immunity;

        // Check group immunities
        var groups = GroupsManager.GetAdminGroups(admin);
        foreach (var group in groups)
        {
            if (group.Immunity > maxImmunity)
            {
                maxImmunity = group.Immunity;
            }
        }

        return maxImmunity;
    }

    /// <summary>
    /// Checks if the command sender has sufficient immunity to apply action to target player.
    /// </summary>
    /// <param name="context">The command context (sender).</param>
    /// <param name="targetPlayer">The target player.</param>
    /// <returns>True if the action can be applied, false if target has immunity.</returns>
    private bool CanApplyActionToPlayer(ICommandContext context, IPlayer targetPlayer)
    {
        // Console always can apply actions
        if (!context.IsSentByPlayer)
        {
            return true;
        }

        int senderImmunity = GetPlayerImmunityLevel(context.Sender!);
        int targetImmunity = GetPlayerImmunityLevel(targetPlayer);

        var config = ConfigurationManager.GetCurrentConfiguration();
        var immunityMode = config!.ImmunityMode;

        return immunityMode switch
        {
            ImmunityMode.IgnoreImmunity => true,
            ImmunityMode.ProtectFromLowerAccess => senderImmunity >= targetImmunity,
            ImmunityMode.ProtectFromEqualOrLowerAccess => senderImmunity > targetImmunity,
            ImmunityMode.ProtectWithNoImmunityBypass =>
                (senderImmunity == 0 || targetImmunity == 0) || senderImmunity > targetImmunity,

            _ => false,
        };
    }

    /// <summary>
    /// Sends an immunity protection message to the command sender.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="targetPlayer">The target player who has immunity.</param>
    private void NotifyImmunityProtection(ICommandContext context, IPlayer targetPlayer)
    {
        var localizer = GetPlayerLocalizer(context);
        var message = localizer[
            "command.target_has_immunity",
            targetPlayer.Controller.PlayerName,
            GetPlayerImmunityLevel(targetPlayer)
        ];
        context.Reply(message);
    }

    /// <summary>
    /// Checks if an admin player has sufficient immunity to apply action to a target player
    /// (for use in menu system and other non-command contexts).
    /// </summary>
    /// <param name="adminPlayer">The admin executing the action.</param>
    /// <param name="targetPlayer">The target player.</param>
    /// <returns>True if the action can be applied, false if target has immunity.</returns>
    public bool CanAdminApplyActionToPlayer(IPlayer adminPlayer, IPlayer targetPlayer)
    {
        int adminImmunity = GetPlayerImmunityLevel(adminPlayer);
        int targetImmunity = GetPlayerImmunityLevel(targetPlayer);

        // If target has higher or equal immunity, admin cannot apply action
        return adminImmunity > targetImmunity;
    }

    /// <summary>
    /// Checks if an admin has sufficient immunity to apply action to a target SteamID
    /// (for use in menu system with offline players).
    /// </summary>
    /// <param name="adminPlayer">The admin executing the action.</param>
    /// <param name="targetSteamId64">The target SteamID64.</param>
    /// <returns>True if the action can be applied, false if target has immunity.</returns>
    public bool CanAdminApplyActionToSteamId(IPlayer adminPlayer, ulong targetSteamId64)
    {
        // Return 0 if managers are not available
        if (AdminsManager == null)
        {
            return true; // Allow if we can't check
        }

        // Try to get the target as an admin
        var targetAdmin = AdminsManager.GetAdmin(targetSteamId64);
        if (targetAdmin == null)
        {
            return true; // Not an admin, allow action
        }

        int adminImmunity = GetPlayerImmunityLevel(adminPlayer);
        int targetImmunity = targetAdmin.Immunity;

        // Check group immunities for target
        if (GroupsManager != null)
        {
            var targetGroups = GroupsManager.GetAdminGroups(targetAdmin);
            foreach (var group in targetGroups)
            {
                if (group.Immunity > targetImmunity)
                {
                    targetImmunity = group.Immunity;
                }
            }
        }

        return adminImmunity > targetImmunity;
    }

    /// <summary>
    /// Notifies the admin player that the target has immunity protection.
    /// </summary>
    /// <param name="adminPlayer">The admin player to notify.</param>
    /// <param name="targetPlayerName">The target player's name.</param>
    /// <param name="targetImmunityLevel">The target's immunity level.</param>
    public void NotifyAdminOfImmunityProtection(IPlayer adminPlayer, string targetPlayerName, int targetImmunityLevel)
    {
        var localizer = Core.Translation.GetPlayerLocalizer(adminPlayer);
        var message = localizer[
            "command.target_has_immunity",
            targetPlayerName,
            targetImmunityLevel
        ];
        adminPlayer.SendChat(message);
    }
}