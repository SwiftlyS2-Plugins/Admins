using SwiftlyS2.Shared.Players;

namespace Admins.Contract;

// What a row of the !admin menu stands for, set as the component's Tag, and what a page is about, set as the menu's
// Tag, so a menu skin can draw them from data instead of parsing their text. A row's Tag is fixed when the page is
// built; what changes while a page is open (frozen, muted, the picks so far) is read through the API when drawn.

/// <summary>
/// A player on the server: a row of the player lists, and the Tag of every page about them. <see cref="PlayerId"/>
/// and <see cref="SessionId"/> identify the connection, so a reconnect is a new row. <see cref="SteamId64"/> is 0 for
/// a bot; as a punishment's target, <see cref="SessionId"/> is 0 for someone who was not on the server.
/// </summary>
public sealed record AdminPlayerRow(int PlayerId, ulong SessionId, ulong SteamId64, string Name);

/// <summary>Someone who left in the last few minutes, offered when picking who to punish.</summary>
public sealed record AdminRecentRow(ulong SteamId64, string Name, DateTimeOffset LeftAt);

/// <summary>A link on a player's page.</summary>
public sealed record AdminPlayerGroupRow(AdminPlayerGroup Group);

/// <summary>
/// Something to do to a player, on their group pages. Which way a two-way action goes now (mute or unmute, freeze or
/// unfreeze) is read when drawn, from <see cref="ICommsManager.GetActiveSanction"/> and
/// <see cref="IAdminMenuAPI.GetPlayerState"/>.
/// </summary>
public sealed record AdminPlayerActionRow(AdminPlayerAction Action);

/// <summary>A team to move the player to.</summary>
public sealed record AdminTeamRow(Team Team);

/// <summary>A health preset, applied as <c>!hp</c> applies it.</summary>
public sealed record AdminHealthRow(int Health, int Armor, bool Helmet);

/// <summary>A money preset: an amount to give, or with <see cref="Add"/> false, to set.</summary>
public sealed record AdminMoneyRow(int Amount, bool Add);

/// <summary>
/// A ban, or a gag or mute: a row of the Bans and Comms lists and of a player's history, and the Tag of its own page.
/// Two are equal when they are the same record, however far apart they were loaded.
/// </summary>
public sealed class AdminRecordRow : IEquatable<AdminRecordRow>
{
    public AdminRecordRow(IBan ban) => Ban = ban;

    public AdminRecordRow(ISanction sanction) => Sanction = sanction;

    /// <summary>The ban; null for a gag or mute.</summary>
    public IBan? Ban { get; }

    /// <summary>The gag or mute; null for a ban.</summary>
    public ISanction? Sanction { get; }

    public long Id => Ban?.Id ?? Sanction!.Id;

    public bool Equals(AdminRecordRow? other) => other is not null && (Ban is null) == (other.Ban is null) && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as AdminRecordRow);

    public override int GetHashCode() => HashCode.Combine(Ban is null, Id);
}

/// <summary>
/// A player's time on every server sharing the database, a row of their Sessions page. Today, the week (from Monday),
/// the month and the year are calendar periods in Admins' <c>TimeZone</c>; the total covers the sessions still kept.
/// </summary>
public sealed record AdminPlaytimeRow(AdminPlaytimePeriod Period, TimeSpan Played);

/// <summary>An address a player connected from, a row of their Sessions page. Picking it prints who else used it.</summary>
/// <param name="Sessions">How many sessions came from it.</param>
/// <param name="LastSeen">When the newest of them ended, or now while it is open.</param>
public sealed record AdminSessionIpRow(string Ip, int Sessions, DateTimeOffset LastSeen);

/// <summary>One connection, a row of a player's Sessions page, newest first.</summary>
/// <param name="Server">The hostname of the server it was on, empty when unknown; null for this server.</param>
/// <param name="Online">Still connected, so <see cref="Played"/> runs to now.</param>
public sealed record AdminSessionRow(long Id, string Ip, string? Server, DateTimeOffset ConnectedAt, TimeSpan Played, bool Online);

/// <summary>A kind to give, on the kind step of giving a gag or mute.</summary>
public sealed record AdminKindRow(AdminPunishment Kind);

/// <summary>A reason to give. A null <see cref="Reason"/> is a kick's "no reason".</summary>
public sealed record AdminReasonRow(string? Reason);

/// <summary>A length to give; <see cref="TimeSpan.Zero"/> is permanent.</summary>
public sealed record AdminLengthRow(TimeSpan Length);

/// <summary>Where it applies: every server on the database, or this one.</summary>
public sealed record AdminScopeRow(bool Global);

/// <summary>How long until the round restarts.</summary>
public sealed record AdminRestartRow(float DelaySeconds);

/// <summary>A map to change to; also the Tag of the page that confirms it.</summary>
public sealed record AdminMapRow(string Map);

/// <summary>A control on a page that is not one of its items: a filter, a button, a chat usage line.</summary>
public sealed record AdminActionRow(AdminAction Action);

/// <summary>A page of a punishment being put together: the step it shows, and the punishment as it stands. Its Tag.</summary>
public sealed record AdminFlowPage(AdminFlowStep Step, IAdminDraft Draft);

/// <summary>An answer on a vote's ballot, indexing <see cref="IAdminVote.Options"/>.</summary>
public sealed record AdminVoteOptionRow(int Index);

public enum AdminPlayerGroup
{
    Punish,
    Control,

    /// <summary>No longer shown: its actions are under <see cref="Control"/>.</summary>
    Move,
    History,
    Sessions,
}

public enum AdminPlaytimePeriod
{
    Today,
    Week,
    Month,
    Year,
    Total,
}

public enum AdminPlayerAction
{
    Kick,
    Ban,
    Mute,
    Gag,
    Silence,
    Slay,
    Freeze,
    Slap,
    Respawn,
    RespawnWhereDied,

    /// <summary>Opens the team page.</summary>
    Team,

    Swap,
    Beacon,
    Drug,
    God,
    Noclip,
    Goto,
    Bring,

    /// <summary>Prints the chat command; the name is typed.</summary>
    Rename,

    /// <summary>Opens the health page.</summary>
    Health,

    /// <summary>Opens the money page.</summary>
    Money,
}

public enum AdminPunishment
{
    Gag,
    Mute,

    /// <summary>A gag and a mute together.</summary>
    Silence,

    Kick,
    Ban,
}

public enum AdminFlowStep
{
    Target,
    Kind,
    Reason,
    Length,
    Scope,
    Confirm,
}

public enum AdminAction
{
    /// <summary>Players list: shows the next team (the page's <see cref="IAdminPlayerList"/>).</summary>
    TeamFilter,

    /// <summary>Bans and Comms lists: starts giving one.</summary>
    Give,

    /// <summary>Bans and Comms lists: shows the next status (the page's <see cref="IAdminRecordList"/>).</summary>
    StatusFilter,

    /// <summary>Comms list: shows the next kind.</summary>
    KindFilter,

    /// <summary>Loads a list or a history again after it failed.</summary>
    Retry,

    /// <summary>A record's page, and a punishment's last step: prints it in chat.</summary>
    Summary,

    /// <summary>A record's page: lifts it.</summary>
    Remove,

    /// <summary>A record's page: opens the page that sets a new length.</summary>
    ChangeLength,

    /// <summary>A record's page: opens the player's page while they are on the server.</summary>
    OpenPlayer,

    /// <summary>Picking a target: opens the players who left in the last few minutes.</summary>
    Recent,

    /// <summary>Picking a target: someone the menu does not list, whose SteamID or IP is typed into the printed command.</summary>
    ById,

    /// <summary>A reason the menu does not list, typed into the command the last step prints; a kick prints it at once.</summary>
    CustomReason,

    /// <summary>A length the menu does not list, typed into the command the last step prints.</summary>
    CustomLength,

    /// <summary>A punishment's last step: issues it.</summary>
    Confirm,

    /// <summary>A punishment's last step: switches between matching by SteamID and by IP.</summary>
    Match,

    /// <summary>Health and money pages: prints the command for an amount they do not list.</summary>
    OtherAmount,

    /// <summary>My tools: the admin's own ESP.</summary>
    Esp,

    /// <summary>My tools: hides the admin.</summary>
    Hide,

    /// <summary>My tools: the admin's own noclip.</summary>
    Noclip,

    /// <summary>My tools: the admin's own god mode.</summary>
    God,

    /// <summary>My tools: respawns the admin.</summary>
    Respawn,

    /// <summary>Server: opens the restart delays.</summary>
    RestartRound,

    /// <summary>Server: removes dropped weapons.</summary>
    CleanWeapons,

    /// <summary>Server: opens the maps.</summary>
    ChangeMap,

    /// <summary>Maps: prints the command for a map the list does not have.</summary>
    OtherMap,

    /// <summary>The map confirm page: changes the map.</summary>
    ConfirmMap,

    /// <summary>
    /// A punishment's last step when part of it is typed in chat (<see cref="IAdminDraft.TypedInChat"/>): prints the
    /// command filled in with every pick, in place of <see cref="Confirm"/>.
    /// </summary>
    PrintCommand,
}
