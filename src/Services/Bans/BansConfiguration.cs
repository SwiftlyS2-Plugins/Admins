using System.ComponentModel;

namespace Admins.Services.Bans;

public sealed class BansConfiguration
{
    [Description("Seconds between re-checks of every online player against the bans table, so bans issued on other servers or written by a web panel take effect. Each check is one indexed query, so a few seconds is fine. 0 disables the periodic check.")]
    public float DatabaseSyncIntervalSeconds { get; set; } = 30;

    [Description("Reasons offered by the ban menu.")]
    public List<string> Reasons { get; set; } =
    [
        "Hacking",
        "Aimbot",
        "Wallhack",
        "SpeedHack",
        "Exploit",
        "Team Killing",
        "Team Flashing",
        "Spamming Mic/Chat",
        "Inappropriate Spray",
        "Inappropriate Language",
        "Inappropriate Name",
        "Ignoring Staff",
        "Team Stacking",
        "Other",
    ];

    [Description("Durations offered by the ban menu, in seconds. 0 is permanent. The menu lists them shortest first with permanent last, and any length can also be typed.")]
    public List<int> DurationsInSeconds { get; set; } = [1800, 3600, 21600, 86400, 604800, 2592000, 0];

    [Description("Kick players whose Steam authorization fails. Turning this off lets players in during Steam outages, but their bans and admin rights cannot be checked until they authorize.")]
    public bool KickOnSteamAuthorizationFailure { get; set; } = true;

    [Description("A global ban of a SteamID also bans the IP address the player had when banned, on every server, and so anyone else connecting from it, admins included. Bans issued by SteamID alone to a player who is not on the server have no address. Applies to existing global bans too.")]
    public bool GlobalBansMatchIp { get; set; } = true;
}
