using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Admins.Database.Models;

/// <summary>One connection of a player to a server.</summary>
[Table("player_sessions")]
public sealed class PlayerSession
{
    [Key]
    public long Id { get; set; }

    [Column("SteamId64")]
    public long SteamId64 { get; set; }

    [Column("PlayerName")]
    public string PlayerName { get; set; } = string.Empty;

    [Column("PlayerIp")]
    public string PlayerIp { get; set; } = string.Empty;

    [Column("Server")]
    public string Server { get; set; } = string.Empty;

    [Column("ConnectedAt")]
    public long ConnectedAt { get; set; }

    /// <summary>The last heartbeat; where a session left open by a crash is taken to have ended.</summary>
    [Column("LastSeenAt")]
    public long LastSeenAt { get; set; }

    /// <summary>0 while the session is open.</summary>
    [Column("DisconnectedAt")]
    public long DisconnectedAt { get; set; }
}
