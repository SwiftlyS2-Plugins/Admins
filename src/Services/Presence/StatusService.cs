using System.Text.RegularExpressions;
using Admins.Models;
using Admins.Shared;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Services.Presence;

/// <summary>
/// Replaces a player's <c>status</c> output with <see cref="StatusConfiguration"/>, so hidden admins stay hidden there too.
/// </summary>
public sealed partial class StatusService
{
    // A client can send status as fast as it likes, and each answer is a console line per player.
    private const long CooldownMs = 1000;

    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<Configuration> _config;
    private readonly PresenceService _presence;
    private readonly Messenger _messenger;

    // Game thread only, keyed by slot.
    private readonly Dictionary<int, (ulong SessionId, long At)> _answered = [];

    public StatusService(ISwiftlyCore core, IOptionsMonitor<Configuration> config, PresenceService presence, Messenger messenger)
    {
        _core = core;
        _config = config;
        _presence = presence;
        _messenger = messenger;

        core.Registrator.Register(this);
    }

    [GeneratedRegex(@"\{(\w+)(?:,(-?\d+))?\}")]
    private static partial Regex Placeholder();

    // A client's 'status' arrives as this message; the server console's own 'status' never passes here.
    [ClientNetMessageHandler]
    public HookResult OnServerStatus(CCLCMsg_ServerStatus message, int playerId)
    {
        if (!_config.CurrentValue.Status.Enabled || _core.PlayerManager.GetPlayer(playerId) is not { } player) return HookResult.Continue;

        var sessionId = player.SessionId;
        _core.Scheduler.NextTick(() => Send(playerId, sessionId));
        return HookResult.Stop;
    }

    private void Send(int slot, ulong sessionId)
    {
        if (_core.PlayerManager.GetPlayer(slot) is not { IsFakeClient: false } viewer || viewer.SessionId != sessionId) return;

        var tick = Environment.TickCount64;
        if (_answered.TryGetValue(slot, out var last) && last.SessionId == sessionId && tick - last.At < CooldownMs) return;
        _answered[slot] = (sessionId, tick);

        var config = _config.CurrentValue.Status;

        var players = _core.PlayerManager.GetAllPlayers()
            .Where(p => p.Controller is { IsValid: true, IsHLTV: false } && (!_presence.IsHidden(p) || _presence.CanSeeHidden(viewer, p)))
            .OrderBy(p => p.UserID)
            .ToList();

        var now = _messenger.Now();
        var server = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hostname"] = _core.ConVar.FindAsString("hostname")?.ValueAsString ?? string.Empty,
            ["map"] = _core.Engine.GlobalVars.MapName.Value,
            ["humans"] = players.Count(p => !p.IsFakeClient).ToString(),
            ["bots"] = players.Count(p => p.IsFakeClient).ToString(),
            ["maxplayers"] = _core.Engine.GlobalVars.MaxClients.ToString(),
            ["date"] = now.ToString("yyyy-MM-dd"),
            ["time"] = now.ToString("HH:mm:ss"),
        };

        foreach (var line in config.Header) viewer.SendConsole(Fill(line, server));

        foreach (var player in players)
        {
            var bot = player.IsFakeClient;
            var name = Names.Of(player);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userid"] = player.UserID.ToString(),
                ["name"] = _presence.IsHidden(player) ? _messenger.Text(viewer, "status.hidden", name) : name,
                ["steamid"] = bot ? "BOT" : player.SteamID.ToString(),
                ["duration"] = bot ? "BOT" : Clock(player.ConnectedTime),
                ["ping"] = bot ? "0" : player.Controller.Ping.ToString(),
            };

            viewer.SendConsole(Fill(config.Row, row));
        }

        foreach (var line in config.Footer) viewer.SendConsole(Fill(line, server));
    }

    private static string Fill(string template, Dictionary<string, string> values) => Placeholder().Replace(template, match =>
    {
        if (!values.TryGetValue(match.Groups[1].Value, out var value)) return match.Value;
        if (!match.Groups[2].Success || !int.TryParse(match.Groups[2].Value, out var width)) return value;

        width = Math.Clamp(width, -256, 256);
        var size = Math.Abs(width);
        if (value.Length > size) value = value[..size];
        return width < 0 ? value.PadRight(size) : value.PadLeft(size);
    });

    private static string Clock(uint seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}" : $"{span.Minutes:D2}:{span.Seconds:D2}";
    }
}
