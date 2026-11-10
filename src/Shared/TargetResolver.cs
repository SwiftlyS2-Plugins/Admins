using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins.Shared;

public enum TargetError
{
    None,
    NotFound,
    Ambiguous,
}

/// <summary>
/// Resolves command targets. Unlike <c>FindTargettedPlayers</c> alone, it reaches players who have no pawn yet
/// (connecting, team select) and refuses a partial name that matches several players.
/// </summary>
public sealed class TargetResolver(ISwiftlyCore core)
{
    /// <param name="excludeCaller">Drops the caller from group selectors such as <c>@all</c> or <c>@t</c>; <c>@me</c> still works.</param>
    /// <param name="groups">Off, a query starting with <c>@</c> is read as a name, so it finds one player at most.</param>
    public IReadOnlyList<IPlayer> Find(IPlayer? caller, string? query, out TargetError error, bool excludeCaller = false, bool groups = true)
    {
        error = TargetError.NotFound;
        if (string.IsNullOrWhiteSpace(query)) return [];

        var text = query.Trim();
        var players = core.PlayerManager.GetAllPlayers().Where(p => p.Controller is not { IsValid: true, IsHLTV: true }).ToList();

        if (groups && text.StartsWith('@'))
        {
            // SwiftlyS2's selectors match tags case-sensitively, skip players with no pawn, and find no team at all
            // for a caller without one, so these are read from the controller here.
            var tag = text.ToLowerInvariant();
            var isMe = tag == "@me";
            List<IPlayer> group;

            if (Selector(tag) is { } member)
            {
                group = [.. players.Where(member)];
            }
            else
            {
                // FindTargettedPlayers dereferences the caller for these selectors.
                if (caller is null && (isMe || tag.StartsWith("@aim", StringComparison.Ordinal)))
                {
                    return [];
                }

                try
                {
                    group = [.. core.PlayerManager.FindTargettedPlayers(caller!, tag, TargetSearchMode.IncludeSelf)];
                }
                catch (Exception)
                {
                    return [];
                }
            }

            if (excludeCaller && caller is not null && !isMe) group.RemoveAll(p => p.Slot == caller.Slot);

            error = group.Count == 0 ? TargetError.NotFound : TargetError.None;
            return group;
        }

        if (text.StartsWith('#') && int.TryParse(text[1..], out var id))
        {
            return Single(players.FirstOrDefault(p => p.UserID == id) ?? players.FirstOrDefault(p => p.Slot == id), out error);
        }

        if (SteamIds.TryParse(text, out var steamId))
        {
            return Single(players.FirstOrDefault(p => p.SteamID == steamId), out error);
        }

        var lowered = text.ToLowerInvariant();
        int? statusId = text.All(char.IsAsciiDigit) && int.TryParse(text, out var number) ? number : null;
        var named = players.Select(p => (Player: p, Name: Names.Of(p).ToLowerInvariant())).ToList();

        // A name typed in full, or the user id from status without its '#', beats a name that only contains the text.
        var exact = named.Where(n => n.Name == lowered || n.Player.UserID == statusId).ToList();
        var matches = exact.Count > 0 ? exact : named.Where(n => n.Name.Contains(lowered, StringComparison.Ordinal)).ToList();

        if (matches.Count > 1)
        {
            error = TargetError.Ambiguous;
            return [];
        }

        return Single(matches.Count == 1 ? matches[0].Player : null, out error);
    }

    /// <summary>
    /// A punishment's target: one player found by name, SteamID or user id, never a group. With nobody on the server
    /// behind a SteamID, returns null with <paramref name="steamId"/> set and no error, for an offline punishment.
    /// </summary>
    public IPlayer? FindOne(IPlayer? caller, string query, out ulong steamId, out TargetError error)
    {
        steamId = 0;
        var players = Find(caller, query, out error, groups: false);
        if (players.Count > 0) return players[0];

        if (error == TargetError.NotFound && SteamIds.TryParse(query.Trim(), out steamId)) error = TargetError.None;
        return null;
    }

    /// <summary>The humans connected from <paramref name="ip"/>, a normalized address.</summary>
    public IReadOnlyList<IPlayer> OnIp(string ip) =>
        [.. core.PlayerManager.GetAllPlayers().Where(p => !p.IsFakeClient && Net.NormalizeIp(p.IPAddress) == ip)];

    /// <summary>
    /// The records named <paramref name="query"/>, ignoring case, or else those whose name contains it, as players on
    /// the server are matched.
    /// </summary>
    public static List<T> Named<T>(IEnumerable<T> records, string query, Func<T, string> name)
    {
        var lowered = query.Trim().ToLowerInvariant();
        var named = records.Where(r => name(r).ToLowerInvariant().Contains(lowered, StringComparison.Ordinal)).ToList();
        var exact = named.Where(r => name(r).ToLowerInvariant() == lowered).ToList();
        return exact.Count > 0 ? exact : named;
    }

    private static Func<IPlayer, bool>? Selector(string tag) => tag switch
    {
        "@all" => _ => true,
        "@t" => p => TeamOf(p) == Team.T,
        "@ct" => p => TeamOf(p) == Team.CT,
        "@spec" => p => TeamOf(p) is Team.Spectator or Team.None,
        "@alive" => IsAlive,
        "@dead" => p => TeamOf(p) is Team.T or Team.CT && !IsAlive(p),
        _ => null,
    };

    private static Team TeamOf(IPlayer player) => player.Controller is { IsValid: true } controller ? (Team)controller.TeamNum : Team.None;

    private static bool IsAlive(IPlayer player) =>
        player.PlayerPawn is { IsValid: true } pawn && pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE;

    private static IReadOnlyList<IPlayer> Single(IPlayer? player, out TargetError error)
    {
        error = player is null ? TargetError.NotFound : TargetError.None;
        return player is null ? [] : [player];
    }
}
