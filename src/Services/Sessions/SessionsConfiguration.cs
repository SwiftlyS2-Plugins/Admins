using System.ComponentModel;

namespace Admins.Services.Sessions;

public sealed class SessionsConfiguration
{
    [Description("Record every connection in the 'player_sessions' table: SteamID, name, IP address, server, and when it started and ended. !sessions shows them, and the menu's 'Recently left' list keeps its players across restarts.")]
    public bool Enabled { get; set; } = true;

    [Description("Days a finished session of this server is kept before it is deleted. 0 keeps them forever.")]
    public int RetentionDays { get; set; } = 90;
}
