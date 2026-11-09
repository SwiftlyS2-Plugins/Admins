using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

/// <summary>The Players page as it stands, its Tag. Changes while the page is open.</summary>
public interface IAdminPlayerList
{
    /// <summary>The team shown, or null for everyone. Spectators include players still picking a team.</summary>
    Team? Team { get; }
}

/// <summary>The Bans or Comms page as it stands, its Tag. Changes while the page is open.</summary>
public interface IAdminRecordList
{
    /// <summary>True on the Comms page (gags and mutes), false on Bans.</summary>
    bool Comms { get; }

    RecordStatus Status { get; }

    /// <summary>The kind the Comms page shows, or null for both.</summary>
    SanctionKind? Kind { get; }

    /// <summary>Of the kind shown, by status; null until counted.</summary>
    AdminRecordCounts? Counts { get; }

    bool Loading { get; }

    bool Failed { get; }
}

/// <summary>
/// A punishment being put together, or a record's length being changed. Changes as the admin picks; each step's page
/// carries it in an <see cref="AdminFlowPage"/>.
/// </summary>
public interface IAdminDraft
{
    AdminPunishment Kind { get; }

    /// <summary>The player once picked, else null, as when they are typed in chat.</summary>
    AdminPlayerRow? Target { get; }

    /// <summary>Matched by IP rather than by SteamID.</summary>
    bool ByIp { get; }

    /// <summary>False until a reason is picked, and while it is typed in chat; <see cref="Reason"/> is null for a kick's "no reason".</summary>
    bool HasReason { get; }

    string? Reason { get; }

    /// <summary>Null until picked, and while it is typed in chat; <see cref="TimeSpan.Zero"/> is permanent.</summary>
    TimeSpan? Length { get; }

    /// <summary>Every server on the database rather than this one; settled once the scope step is past, or when it is not asked.</summary>
    bool Global { get; }

    /// <summary>The record whose length is being changed; null when giving a new one.</summary>
    AdminRecordRow? Record { get; }

    /// <summary>This punishment's steps in order. The scope step is only asked of an admin who may give both.</summary>
    IReadOnlyList<AdminFlowStep> Steps { get; }

    /// <summary>
    /// The steps no listed choice fitted (the target, the reason, the length): their values are left for the admin to
    /// type into the chat command the last step prints instead of issuing it. Empty when the menu issues it.
    /// </summary>
    IReadOnlySet<AdminFlowStep> TypedInChat { get; }

    /// <summary>
    /// What the player will be shown, in the admin's language, for <paramref name="length"/> or the length picked.
    /// A part typed in chat shows as its placeholder, <c>&lt;reason&gt;</c> or <c>&lt;time&gt;</c>. Empty for a kick.
    /// </summary>
    string Preview(TimeSpan? length = null);
}

/// <summary>The vote a ballot is for, the ballot's Tag. Changes while the vote runs.</summary>
public interface IAdminVote
{
    AdminVoteKind Kind { get; }

    /// <summary>Who started it: the admin's name, or the console's.</summary>
    string StartedBy { get; }

    /// <summary>The player a vote kick is about; null for other votes.</summary>
    AdminPlayerRow? Target { get; }

    /// <summary>What is asked, in a player's language; the ballot's title.</summary>
    string Question(IPlayer? viewer);

    /// <summary>The answers in a player's language, in the ballot's order. A kick's are yes, then no.</summary>
    IReadOnlyList<string> Options(IPlayer? viewer);

    /// <summary>Ballots cast for an answer so far.</summary>
    int Votes(int option);

    /// <summary>Who may vote: the humans on the server when it started, less those who have left.</summary>
    int Voters { get; }

    /// <summary>Ballots cast so far.</summary>
    int Voted { get; }

    int SecondsLeft { get; }
}

public enum AdminVoteKind
{
    /// <summary>A question with answers of the admin's own.</summary>
    Poll,

    /// <summary>Yes or no on kicking <see cref="IAdminVote.Target"/>; a tie keeps them.</summary>
    Kick,

    /// <summary>A map per answer; the winner is changed to.</summary>
    Map,
}

/// <summary>What the admin tools have set on a player, beyond what the game shows.</summary>
/// <param name="Hidden">Hidden from the scoreboard and other players.</param>
/// <param name="Esp">Sees players through walls.</param>
/// <param name="HasDeathPosition">Can be respawned where they died.</param>
public readonly record struct AdminPlayerState(
    bool Hidden,
    bool Frozen,
    bool Noclip,
    bool God,
    bool Beacon,
    bool Drugged,
    bool Esp,
    bool HasDeathPosition);

/// <summary>Records that apply to this server (its own and every global one), by status.</summary>
public readonly record struct AdminRecordCounts(int Active, int Expired)
{
    public int All => Active + Expired;
}
