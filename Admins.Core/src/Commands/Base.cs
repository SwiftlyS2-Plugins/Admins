using Admins.Core.Admins;
using Admins.Core.Config;
using Admins.Core.Groups;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using SwiftlyS2.Shared.Translation;

namespace Admins.Core.Commands;

public partial class ServerCommands
{
    private ISwiftlyCore Core = null!;
    private ConfigurationManager ConfigurationManager = null!;
    private AdminsManager? _adminsManager;
    private GroupsManager? _groupsManager;

    public ServerCommands(ISwiftlyCore core, ConfigurationManager configurationManager, AdminsManager adminsManager, GroupsManager groupsManager)
    {
        Core = core;
        ConfigurationManager = configurationManager;
        _adminsManager = adminsManager;
        _groupsManager = groupsManager;

        core.Registrator.Register(this);
    }

    /// <summary>
    /// Sends command syntax help message to the command sender.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="cmdname">The command name.</param>
    /// <param name="arguments">Array of required arguments.</param>
    private async Task SendSyntaxAsync(ICommandContext context, string cmdname, string[] arguments)
    {
        var localizer = GetPlayerLocalizer(context);
        var syntax = localizer[
            "command.syntax",
            ConfigurationManager.GetCurrentConfiguration()!.Prefix,
            context.Prefix,
            cmdname,
            string.Join(" ", arguments)
        ];
        await context.ReplyAsync(syntax);
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
    private async Task<bool> ValidateArgsCountAsync(ICommandContext context, int requiredArgs, string cmdname, string[] arguments)
    {
        if (context.Args.Length < requiredArgs)
        {
            await SendSyntaxAsync(context, cmdname, arguments);
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
}