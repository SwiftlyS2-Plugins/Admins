using Admins.Core.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using SwiftlyS2.Shared.Translation;

namespace Admins.SuperCommands.Commands;

public partial class ServerCommands
{
    private readonly ISwiftlyCore Core = null!;
    private IConfigurationManager ConfigurationManager = null!;

    public ServerCommands(ISwiftlyCore core)
    {
        Core = core;

        core.Registrator.Register(this);
    }

    public void SetConfigurationManager(IConfigurationManager configurationManager)
    {
        ConfigurationManager = configurationManager;
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
}