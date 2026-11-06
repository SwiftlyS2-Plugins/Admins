using System.Net;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using TimeSpanParserUtil;

namespace Admins.Shared;

public static class Names
{
    // Controller name first: it reflects !rename and is what the scoreboard shows.
    public static string Of(IPlayer player)
    {
        try
        {
            var controller = player.Controller;
            if (controller is { IsValid: true }) return controller.PlayerName;
            return player.Name;
        }
        catch (Exception)
        {
            return "Unknown";
        }
    }
}

public static class Net
{
    /// <summary>
    /// Strips a port suffix (<c>1.2.3.4:27005</c>, <c>[::1]:27005</c>) so IP bans match across reconnects.
    /// </summary>
    public static string NormalizeIp(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;

        var value = address.Trim();

        if (value.StartsWith('['))
        {
            var end = value.IndexOf(']');
            return end > 1 ? value[1..end] : value;
        }

        var colon = value.IndexOf(':');
        return colon > 0 && colon == value.LastIndexOf(':') ? value[..colon] : value;
    }

    public static bool IsIp(string? value)
    {
        var normalized = NormalizeIp(value);
        if (!IPAddress.TryParse(normalized, out var ip)) return false;

        // IPAddress.TryParse accepts "123" as 0.0.0.123, which would turn SteamIDs and user ids into IPs.
        return ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || normalized.Count(c => c == '.') == 3;
    }
}

public static class SteamIds
{
    public static bool TryParse(string? value, out ulong steamId64)
    {
        steamId64 = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            var id = new CSteamID(value.Trim());
            if (!id.IsValid()) return false;

            steamId64 = id.GetSteamID64();
            return steamId64 != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public static class Durations
{
    /// <summary>
    /// Parses relative time such as <c>30m</c>, <c>12h</c>, <c>7d</c>. <c>0</c> is permanent (zero).
    /// </summary>
    public static bool TryParse(string? value, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (text == "0") return true;

        if (!TimeSpanParser.TryParse(text, out var parsed) || parsed <= TimeSpan.Zero) return false;

        duration = parsed;
        return true;
    }

    public static long ExpiresAt(TimeSpan duration, long nowMs) => duration <= TimeSpan.Zero ? 0 : nowMs + (long)duration.TotalMilliseconds;

    /// <summary>
    /// Compact form with the two largest units: <c>14d</c>, <c>1d 6h</c>, <c>1h 30m</c>, <c>45s</c>.
    /// </summary>
    public static string Format(TimeSpan span)
    {
        var parts = new List<string>(2);

        if (span.Days > 0) parts.Add($"{span.Days}d");
        if (span.Hours > 0) parts.Add($"{span.Hours}h");
        if (span.Minutes > 0) parts.Add($"{span.Minutes}m");
        if (span.Seconds > 0) parts.Add($"{span.Seconds}s");

        return parts.Count == 0 ? "0s" : string.Join(' ', parts.Take(2));
    }
}
