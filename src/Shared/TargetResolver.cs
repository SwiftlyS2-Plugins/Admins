using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
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
/// Resolves command targets through SwiftlyS2's <c>FindTargettedPlayers</c>, so only players it offers are found, with
/// Admins' rule for each lookup applied in its handler. Refuses a partial name that matches several players.
/// </summary>
public sealed class TargetResolver
{
    // Set only around Admins' own search: the handler runs for every plugin's searches.
    [ThreadStatic] private static Func<IPlayer, bool>? _rule;
    [ThreadStatic] private static List<IPlayer>? _accepted;

    private readonly ISwiftlyCore _core;

    public TargetResolver(ISwiftlyCore core)
    {
        _core = core;

        core.Registrator.Register(this);
    }

    /// <param name="excludeCaller">Drops the caller from group selectors such as <c>@all</c> or <c>@t</c>; <c>@me</c> still works.</param>
    /// <param name="groups">Off, a query starting with <c>@</c> is read as a name, so it finds one player at most.</param>
    public IReadOnlyList<IPlayer> Find(IPlayer? caller, string? query, out TargetError error, bool excludeCaller = false, bool groups = true)
    {
        error = TargetError.NotFound;
        if (string.IsNullOrWhiteSpace(query)) return [];

        var text = query.Trim();
        HashSet<IPlayer> turnedDown;

        if (groups && text.StartsWith('@'))
        {
            // Lowercased, as SwiftlyS2 compares tags case-sensitively. Admins' selectors read teams from the controller,
            // where SwiftlyS2's read the pawn.
            var tag = text.ToLowerInvariant();
            var isMe = tag == "@me";
            List<IPlayer> group;

            if (Selector(tag) is { } member)
            {
                group = Candidates(caller, tag, member, out turnedDown);
                group.RemoveAll(turnedDown.Contains);
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
                    group = [.. _core.PlayerManager.FindTargettedPlayers(caller!, tag, TargetSearchMode.IncludeSelf)];
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
            var players = Candidates(caller, text, p => p.UserID == id || p.Slot == id, out turnedDown);
            return Single(players.FirstOrDefault(p => p.UserID == id) ?? players.FirstOrDefault(p => p.Slot == id), turnedDown, out error);
        }

        if (SteamIds.TryParse(text, out var steamId))
        {
            var players = Candidates(caller, text, p => p.SteamID == steamId, out turnedDown);
            return Single(players.FirstOrDefault(p => p.SteamID == steamId), turnedDown, out error);
        }

        var lowered = text.ToLowerInvariant();
        int? statusId = text.All(char.IsAsciiDigit) && int.TryParse(text, out var number) ? number : null;
        var named = Candidates(caller, text, p => p.UserID == statusId || Names.Of(p).ToLowerInvariant().Contains(lowered, StringComparison.Ordinal), out turnedDown)
            .Select(p => (Player: p, Name: Names.Of(p).ToLowerInvariant())).ToList();

        // A name typed in full, or the user id from status without its '#', beats a name that only contains the text.
        var exact = named.Where(n => n.Name == lowered || n.Player.UserID == statusId).ToList();
        var matches = exact.Count > 0 ? exact : named.Where(n => n.Name.Contains(lowered, StringComparison.Ordinal)).ToList();

        if (matches.Count > 1)
        {
            error = TargetError.Ambiguous;
            return [];
        }

        return Single(matches.Count == 1 ? matches[0].Player : null, turnedDown, out error);
    }

    /// <summary>
    /// A punishment's target: one player found by name, SteamID or user id, never a group. With nobody on the server
    /// behind a SteamID, returns null with <paramref name="steamId"/> set and no error, for an offline punishment;
    /// with a player there whom the search does not return, it finds nobody.
    /// </summary>
    public IPlayer? FindOne(IPlayer? caller, string query, out ulong steamId, out TargetError error)
    {
        steamId = 0;
        var players = Find(caller, query, out error, groups: false);
        if (players.Count > 0) return players[0];

        // Unauthorized players too: under SwiftlyS2's default flexible auth the search matches them by SteamID.
        if (error == TargetError.NotFound && SteamIds.TryParse(query.Trim(), out var id)
            && _core.PlayerManager.GetPlayerFromSteamId(id, allowUnauthorized: true) is null)
        {
            steamId = id;
            error = TargetError.None;
        }

        return null;
    }

    /// <summary>The humans connected from <paramref name="ip"/>, a normalized address.</summary>
    public IReadOnlyList<IPlayer> OnIp(string ip) =>
        [.. _core.PlayerManager.GetAllPlayers().Where(p => !p.IsFakeClient && Net.NormalizeIp(p.IPAddress) == ip)];

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

    [EventListener<EventDelegates.OnFindTargettedPlayers>]
    public void OnFindTargettedPlayers(IOnFindTargettedPlayersEvent e)
    {
        if (_rule is not { } rule) return;

        e.IsMatch = rule(e.Candidate);
        if (e.IsMatch) _accepted!.Add(e.Candidate);
    }

    // Returns the candidates the rule accepted, which OnFindTargettedPlayers collects while SwiftlyS2 searches.
    private List<IPlayer> Candidates(IPlayer? caller, string target, Func<IPlayer, bool> rule, out HashSet<IPlayer> turnedDown)
    {
        turnedDown = [];

        // SwiftlyS2 answers "@me" and "@aim" from the searcher alone, never as names.
        if (target is "@me" or "@aim") return [];

        List<IPlayer> accepted = [];
        _rule = rule;
        _accepted = accepted;
        try
        {
            // Without IncludeSelf, SwiftlyS2 reads the caller's slot, which the console (a null caller) does not have.
            var found = _core.PlayerManager.FindTargettedPlayers(caller!, target, TargetSearchMode.IncludeSelf).ToHashSet();
            turnedDown = [.. accepted.Where(p => !found.Contains(p))];
        }
        finally
        {
            _rule = null;
            _accepted = null;
        }

        return accepted;
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

    private static IReadOnlyList<IPlayer> Single(IPlayer? player, HashSet<IPlayer> turnedDown, out TargetError error)
    {
        // A pick another plugin's handler turned down finds nobody, never the next match.
        if (player is not null && turnedDown.Contains(player)) player = null;

        error = player is null ? TargetError.NotFound : TargetError.None;
        return player is null ? [] : [player];
    }
}
