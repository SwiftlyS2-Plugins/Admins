using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

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
    public IReadOnlyList<IPlayer> Find(IPlayer? caller, string? query, out TargetError error, bool excludeCaller = false)
    {
        error = TargetError.NotFound;
        if (string.IsNullOrWhiteSpace(query)) return [];

        var text = query.Trim();

        if (text.StartsWith('@'))
        {
            var isMe = text.Equals("@me", StringComparison.OrdinalIgnoreCase);

            // FindTargettedPlayers dereferences the caller for these selectors.
            if (caller is null && (isMe || text.StartsWith("@aim", StringComparison.OrdinalIgnoreCase)))
            {
                return [];
            }

            List<IPlayer> group;
            try
            {
                group = [.. core.PlayerManager.FindTargettedPlayers(caller!, text, TargetSearchMode.IncludeSelf)];
            }
            catch (Exception)
            {
                return [];
            }

            if (excludeCaller && caller is not null && !isMe) group.RemoveAll(p => p.Slot == caller.Slot);

            error = group.Count == 0 ? TargetError.NotFound : TargetError.None;
            return group;
        }

        var players = core.PlayerManager.GetAllPlayers().Where(p => p.Controller is not { IsValid: true, IsHLTV: true }).ToList();

        if (text.StartsWith('#') && int.TryParse(text[1..], out var id))
        {
            return Single(players.FirstOrDefault(p => p.UserID == id) ?? players.FirstOrDefault(p => p.Slot == id), out error);
        }

        if (text.Length == 17 && text.All(char.IsAsciiDigit) && ulong.TryParse(text, out var steamId))
        {
            return Single(players.FirstOrDefault(p => p.SteamID == steamId), out error);
        }

        var named = players.Select(p => (Player: p, Name: Names.Of(p))).ToList();

        var exact = named.Where(n => string.Equals(n.Name, text, StringComparison.OrdinalIgnoreCase)).ToList();
        var matches = exact.Count > 0 ? exact : named.Where(n => n.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count > 1)
        {
            error = TargetError.Ambiguous;
            return [];
        }

        return Single(matches.Count == 1 ? matches[0].Player : null, out error);
    }

    private static IReadOnlyList<IPlayer> Single(IPlayer? player, out TargetError error)
    {
        error = player is null ? TargetError.NotFound : TargetError.None;
        return player is null ? [] : [player];
    }
}
