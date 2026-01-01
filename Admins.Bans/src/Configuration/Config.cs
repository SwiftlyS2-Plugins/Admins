using Admins.Bans.Contract;

namespace Admins.Bans.Configuration;

public class BansConfiguration : IBansConfiguration
{
    // <inheritdoc/>
    public List<string> BansReasons { get; set; } = [
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
        "Other"
    ];

    /// <inheritdoc/>
    public List<int> BansDurationsInSeconds { get; set; } = [
        0,
        300,
        600,
        900,
        1800,
        3600,
        7200,
        21600,
        43200,
        86400,
        172800,
        604800,
        1209600
    ];
}