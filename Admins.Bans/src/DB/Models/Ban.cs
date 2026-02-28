using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Admins.Bans.Contract;

namespace Admins.Bans.Database.Models;

[Table("bans")]
public class Ban : IBan
{
    [Key]
    public long Id { get; set; }

    [Column("SteamId64")]
    public long SteamId64 { get; set; }

    [Column("PlayerName")]
    public string PlayerName { get; set; } = string.Empty;

    [Column("PlayerIp")]
    public string PlayerIp { get; set; } = string.Empty;

    [Column("BanType")]
    public BanType BanType { get; set; }

    [Column("ExpiresAt")]
    public long ExpiresAt { get; set; }

    [Column("Length")]
    public long Length { get; set; }

    [Column("Reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("AdminSteamId64")]
    public long AdminSteamId64 { get; set; }

    [Column("AdminName")]
    public string AdminName { get; set; } = string.Empty;

    [Column("Server")]
    public string Server { get; set; } = string.Empty;

    [Column("GlobalBan")]
    public bool GlobalBan { get; set; }

    [Column("CreatedAt")]
    public long CreatedAt { get; set; }

    [Column("UpdatedAt")]
    public long UpdatedAt { get; set; }
}