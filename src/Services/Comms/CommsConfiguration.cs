using System.ComponentModel;

namespace Admins.Services.Comms;

public sealed class CommsConfiguration
{
    [Description("Seconds between re-loads of the sanctions of every online player, so sanctions issued on other servers take effect. 0 disables the periodic reload.")]
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

    [Description("Durations offered by the gag/mute menu, in seconds. 0 is permanent.")]
    public List<int> DurationsInSeconds { get; set; } = [0, 300, 600, 900, 1800, 3600, 7200, 21600, 43200, 86400, 172800, 604800, 1209600];

    [Description("Team-chat messages starting with AdminChatStartCharacter are sent to every player with 'admins.chat' instead of the team.")]
    public bool EnableAdminChat { get; set; } = true;

    [Description("Prefix that routes a team-chat message to admin chat.")]
    public string AdminChatStartCharacter { get; set; } = "@";
}
