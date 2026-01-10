using Admins.Comms.Contract;

namespace Admins.Comms.Configuration;

public class CommsConfiguration : ICommsConfiguration
{
    // <inheritdoc/>
    public List<string> CommsReasons { get; set; } = [
        "Obscene language",
        "Insult players",
        "Admin disrespect",
        "Inappropriate language",
        "Spam",
        "Trading",
        "Other",
        "Advertisement",
        "Music in voice"
    ];

    /// <inheritdoc/>
    public List<int> CommsDurationsInSeconds { get; set; } = [
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

    /// <inheritdoc/>
    public bool EnableAdminChat { get; set; } = true;

    /// <inheritdoc/>
    public string AdminChatStartCharacter { get; set; } = "@";
}