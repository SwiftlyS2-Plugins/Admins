using Admins.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace Admins.Shared;

/// <summary>
/// The player or console that started an action. Created on the game thread; after an await, check
/// <see cref="Messenger.IsConnected"/> before touching <see cref="Player"/> again.
/// </summary>
public sealed class Issuer
{
    private Issuer(ICommandContext? context, IPlayer? player, string name)
    {
        Context = context;
        Player = player;
        Name = name;
        SessionId = player?.SessionId ?? 0;
        SteamId = player?.SteamID ?? 0;
    }

    public ICommandContext? Context { get; }

    public IPlayer? Player { get; }

    public ulong SessionId { get; }

    public ulong SteamId { get; }

    public string Name { get; }

    public bool IsConsole => Player is null;

    public static Issuer From(ICommandContext context) =>
        context.IsSentByPlayer && context.Sender is { } sender
            ? new Issuer(context, sender, Names.Of(sender))
            : new Issuer(context, null, "Console");

    public static Issuer From(IPlayer player) => new(null, player, Names.Of(player));
}

public sealed class Messenger : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _subscription;
    private volatile string _prefix;
    private volatile TimeZoneInfo _timeZone;

    public Messenger(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        _prefix = config.CurrentValue.Prefix;
        _timeZone = ResolveTimeZone(config.CurrentValue.TimeZone);
        _subscription = config.OnChange(c =>
        {
            _prefix = c.Prefix;
            _timeZone = ResolveTimeZone(c.TimeZone);
        });
    }

    public ILocalizer For(IPlayer? player) => player is null ? _core.Localizer : _core.Translation.GetPlayerLocalizer(player);

    public string Text(IPlayer? player, string key, params object[] args) => For(player)[key, args];

    public void Chat(IPlayer player, string key, params object[] args) => player.SendChat($"{_prefix} {Text(player, key, args)}");

    public void Reply(Issuer issuer, string key, params object[] args)
    {
        // Votes and delayed actions reply long after the command; the issuer may have left by then.
        if (!IsConnected(issuer)) return;

        var text = $"{_prefix} {Text(issuer.Player, key, args)}";

        if (issuer.Context is { } context)
        {
            context.Reply(text);
        }
        else
        {
            issuer.Player?.SendChat(text);
        }
    }

    /// <summary>
    /// Replies from a background thread: hops to the game thread and drops the message if the player left.
    /// </summary>
    public void ReplyLater(Issuer issuer, string key, params object[] args) =>
        _core.Scheduler.NextTick(() => Reply(issuer, key, args));

    public bool IsConnected(Issuer issuer) => issuer.IsConsole || _core.PlayerManager.IsSessionIdValid(issuer.SessionId);

    public void Syntax(Issuer issuer, string command, string usage) =>
        Reply(issuer, "command.syntax", issuer.Context?.Prefix ?? string.Empty, command, usage);

    /// <summary>
    /// Reports an empty or ambiguous target lookup. Returns true when <paramref name="error"/> was reported.
    /// </summary>
    public bool ReportTargetError(Issuer issuer, TargetError error, string query)
    {
        switch (error)
        {
            case TargetError.Ambiguous:
                Reply(issuer, "command.multiple_targets", query);
                return true;
            case TargetError.NotFound:
                Reply(issuer, "command.player_not_found", query);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Sends a chat line to every connected human, localized per recipient.
    /// </summary>
    public void Broadcast(string key, params object[] args)
    {
        foreach (var player in Humans())
        {
            Chat(player, key, args);
        }
    }

    /// <summary>
    /// Sends a chat line to every connected human holding <paramref name="permission"/>.
    /// </summary>
    public void ToPermission(string permission, string key, params object[] args)
    {
        foreach (var player in Humans())
        {
            if (_core.Permission.PlayerHasPermission(player.SteamID, permission))
            {
                Chat(player, key, args);
            }
        }
    }

    public IEnumerable<IPlayer> Humans() => _core.PlayerManager.GetAllPlayers().Where(p => !p.IsFakeClient);

    public string Timestamp(IPlayer? viewer, long unixMs) =>
        unixMs == 0
            ? Text(viewer, "time.never")
            : TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(unixMs), _timeZone).ToString("yyyy-MM-dd HH:mm:ss");

    public string Duration(IPlayer? viewer, long lengthMs) =>
        lengthMs <= 0 ? Text(viewer, "time.permanent") : Durations.Format(TimeSpan.FromMilliseconds(lengthMs));

    public string Duration(IPlayer? viewer, TimeSpan length) => Duration(viewer, (long)length.TotalMilliseconds);

    public void Dispose() => _subscription?.Dispose();

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
