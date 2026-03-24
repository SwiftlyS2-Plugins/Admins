using Admins.Core.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace Admins.SuperCommands.Commands;

public partial class ServerCommands
{
    private readonly ISwiftlyCore Core = null!;
    private IConfigurationManager ConfigurationManager = null!;
    private IAdminsManager? AdminsManager;
    private IGroupsManager? GroupsManager;

    public ServerCommands(ISwiftlyCore core)
    {
        Core = core;

        core.Registrator.Register(this);
    }

    public void SetConfigurationManager(IConfigurationManager configurationManager)
    {
        ConfigurationManager = configurationManager;
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
    /// Sends a \"player only\" error message when command requires a player sender.
    /// </summary>
    /// <param name="context">The command context.</param>
    private void SendByPlayerOnly(ICommandContext context)
    {
        var localizer = GetPlayerLocalizer(context);
        context.Reply(localizer["command.player_only", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
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

            if (sender != null && sender.PlayerID != player.PlayerID)
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

        return [.. players];
    }

    /// <summary>
    /// Tries to parse an integer value with validation.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="value">The string value to parse.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <param name="result">The parsed integer.</param>
    /// <returns>True if parsing and validation succeed.</returns>
    private bool TryParseInt(
        ICommandContext context,
        string value,
        string paramName,
        int min,
        int max,
        out int result)
    {
        var localizer = GetPlayerLocalizer(context);

        if (!int.TryParse(value, out result))
        {
            context.Reply(localizer[
                "command.invalid_value_range",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                value,
                paramName,
                min.ToString(),
                max.ToString()
            ]);
            return false;
        }

        if (result < min || result > max)
        {
            context.Reply(localizer[
                "command.invalid_value_range",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                value,
                paramName,
                min.ToString(),
                max.ToString()
            ]);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to parse a float value with validation.
    /// </summary>
    private bool TryParseFloat(
        ICommandContext context,
        string value,
        string paramName,
        float min,
        float max,
        out float result)
    {
        var localizer = GetPlayerLocalizer(context);

        if (!float.TryParse(value, out result))
        {
            context.Reply(localizer[
                "command.invalid_value_range",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                value,
                paramName,
                min.ToString("F1"),
                max.ToString("F1")
            ]);
            return false;
        }

        if (result < min || result > max)
        {
            context.Reply(localizer[
                "command.invalid_value_range",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                value,
                paramName,
                min.ToString("F1"),
                max.ToString("F1")
            ]);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to parse a boolean value.
    /// </summary>
    private bool TryParseBool(ICommandContext context, string value, string paramName, out bool result)
    {
        var localizer = GetPlayerLocalizer(context);

        if (!bool.TryParse(value, out result))
        {
            context.Reply(localizer[
                "command.invalid_value_range",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                value,
                paramName,
                "false",
                "true"
            ]);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a valid player name, defaulting to "Unknown" if controller is invalid.
    /// </summary>
    private string GetPlayerName(IPlayer player)
    {
        return player.Controller.IsValid ? player.Controller.PlayerName : "Unknown";
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
    /// Gets the immunity level of a player (highest of admin direct immunity or group immunity).
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>The player's immunity level (0-100), or 0 if managers unavailable.</returns>
    public int GetPlayerImmunityLevel(IPlayer player)
    {
        if (AdminsManager == null)
        {
            return 0;
        }

        var admin = AdminsManager.GetAdmin(player.PlayerID);
        if (admin == null)
        {
            return 0;
        }

        var immunity = admin.Immunity;

        if (GroupsManager != null)
        {
            foreach (var groupId in admin.Groups)
            {
                var group = GroupsManager.GetGroup(groupId);
                if (group != null && group.Immunity > immunity)
                {
                    immunity = group.Immunity;
                }
            }
        }

        return immunity;
    }

    /// <summary>
    /// Checks if an admin can apply an action to a target player based on immunity levels.
    /// Admin must have strictly higher immunity than target.
    /// </summary>
    /// <param name="adminPlayer">The admin performing the action.</param>
    /// <param name="targetPlayer">The target player.</param>
    /// <returns>True if action can be applied, false if target is protected.</returns>
    private bool CanApplyActionToPlayer(IPlayer adminPlayer, IPlayer targetPlayer)
    {
        var adminImmunity = GetPlayerImmunityLevel(adminPlayer);
        var targetImmunity = GetPlayerImmunityLevel(targetPlayer);

        var config = ConfigurationManager.GetCurrentConfiguration();
        var immunityMode = config!.ImmunityMode;

        return immunityMode switch
        {
            ImmunityMode.IgnoreImmunity => true,
            ImmunityMode.ProtectFromLowerAccess => adminImmunity >= targetImmunity,
            ImmunityMode.ProtectFromEqualOrLowerAccess => adminImmunity > targetImmunity,
            ImmunityMode.ProtectWithNoImmunityBypass =>
                (adminImmunity == 0 || targetImmunity == 0) || adminImmunity > targetImmunity,

            _ => false,
        };
    }

    /// <summary>
    /// Notifies the admin when target player has immunity protection.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="targetPlayerName">Name of the protected target.</param>
    /// <param name="targetImmunityLevel">The target's immunity level.</param>
    public void NotifyAdminOfImmunityProtection(ICommandContext context, string targetPlayerName, int targetImmunityLevel)
    {
        var localizer = GetPlayerLocalizer(context);
        var message = localizer["command.target_has_immunity", ConfigurationManager.GetCurrentConfiguration()!.Prefix, targetPlayerName, targetImmunityLevel];
        context.Reply(message);
    }
}