using System.ComponentModel;

namespace Admins.Services.Bans;

public sealed class BansConfiguration
{
    [Description("Seconds between re-checks of every online player against the bans table, so bans issued on other servers take effect. 0 disables the periodic check.")]
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

    [Description("Durations offered by the ban menu, in seconds. 0 is permanent.")]
    public List<int> DurationsInSeconds { get; set; } = [0, 300, 600, 900, 1800, 3600, 7200, 21600, 43200, 86400, 172800, 604800, 1209600];

    [Description("Kick players whose Steam authorization fails. Turning this off lets players in during Steam outages, but their bans and admin rights cannot be checked until they authorize.")]
    public bool KickOnSteamAuthorizationFailure { get; set; } = true;
}
