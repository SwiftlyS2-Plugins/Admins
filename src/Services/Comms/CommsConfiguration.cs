using System.ComponentModel;

namespace Admins.Services.Comms;

public sealed class CommsConfiguration
{
    [Description("Seconds between re-loads of the sanctions of every online player, so sanctions issued on other servers or written by a web panel take effect. Each reload is one indexed query, so a few seconds is fine. 0 disables the periodic reload.")]
    public float DatabaseSyncIntervalSeconds { get; set; } = 30;

    [Description("Reasons offered by the gag/mute menu.")]
    public List<string> Reasons { get; set; } =
    [
        "Obscene language",
        "Insult players",
        "Admin disrespect",
        "Inappropriate language",
        "Spam",
        "Trading",
        "Other",
        "Advertisement",
        "Music in voice",
    ];

    [Description("Durations offered by the gag/mute menu, in seconds. 0 is permanent. The menu lists them shortest first with permanent last, and any length can also be typed.")]
    public List<int> DurationsInSeconds { get; set; } = [1800, 3600, 21600, 86400, 604800, 2592000, 0];

    [Description("Team-chat messages starting with AdminChatStartCharacter are sent to every player with 'admins.chat' instead of the team.")]
    public bool EnableAdminChat { get; set; } = true;

    [Description("Prefix that routes a team-chat message to admin chat.")]
    public string AdminChatStartCharacter { get; set; } = "@";
}
