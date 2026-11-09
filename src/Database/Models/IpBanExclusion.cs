using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Admins.Database.Models;

/// <summary>A SteamID that bans reaching it only through its IP address leave alone, on every server.</summary>
[Table("ip_ban_exclusions")]
public sealed class IpBanExclusion
{
    [Key]
    public long Id { get; set; }

    [Column("SteamId64")]
    public long SteamId64 { get; set; }

    [Column("PlayerName")]
    public string PlayerName { get; set; } = string.Empty;

    [Column("AdminSteamId64")]
    public long AdminSteamId64 { get; set; }

    [Column("AdminName")]
    public string AdminName { get; set; } = string.Empty;

    [Column("CreatedAt")]
    public long CreatedAt { get; set; }
}
