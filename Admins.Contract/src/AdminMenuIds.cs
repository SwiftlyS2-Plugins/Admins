namespace Admins.Contract;

/// <summary>
/// Ids of the <c>!admin</c> menu's pages, for a menu skin to key a layout on. A punishment's steps add the step to a
/// prefix: <see cref="GiveBan"/> + <see cref="AdminFlowSteps.Reason"/> is <c>admins.bans.give.reason</c>.
/// </summary>
public static class AdminMenuIds
{
    public const string Root = "admins.main";

    /// <summary>Shown in place of a registered entry whose menu failed to build.</summary>
    public const string Notice = "admins.notice";

    public const string Players = "admins.players";
    public const string Player = "admins.players.player";
    public const string PlayerPunish = "admins.players.player.punish";
    public const string PlayerControl = "admins.players.player.control";
    public const string PlayerMove = "admins.players.player.move";
    public const string PlayerHistory = "admins.players.player.history";
    public const string PlayerTeam = "admins.players.team";
    public const string PlayerHealth = "admins.players.health";
    public const string PlayerMoney = "admins.players.money";

    public const string Bans = "admins.bans";
    public const string Comms = "admins.comms";
    public const string BanRecord = "admins.bans.record";
    public const string CommsRecord = "admins.comms.record";

    /// <summary>Prefix of a ban's steps; also of the page that changes a ban's length.</summary>
    public const string GiveBan = "admins.bans.give.";

    /// <summary>Prefix of a gag's, a mute's and a silence's steps; also of the page that changes a gag's or mute's length.</summary>
    public const string GiveComms = "admins.comms.give.";

    /// <summary>Prefix of a kick's steps.</summary>
    public const string Kick = "admins.kick.";

    public const string Tools = "admins.tools";
    public const string Server = "admins.server";
    public const string ServerRestart = "admins.server.restart";
    public const string ServerMap = "admins.server.map";
    public const string ServerMapConfirm = "admins.server.map_confirm";

    /// <summary>The ballot every player gets during a vote; its Tag is the <see cref="IAdminVote"/>.</summary>
    public const string Vote = "admins.vote";
}

/// <summary>The step names a punishment's page ids end with.</summary>
public static class AdminFlowSteps
{
    public const string Target = "target";

    /// <summary>The target step's page of players who left in the last few minutes.</summary>
    public const string Recent = "target.recent";

    public const string Kind = "kind";
    public const string Reason = "reason";
    public const string Length = "length";
    public const string Scope = "scope";
    public const string Confirm = "confirm";
}
