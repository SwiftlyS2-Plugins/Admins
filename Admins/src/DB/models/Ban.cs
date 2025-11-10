using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Admins.Contract;

namespace Admins.Database.Models;

public enum BanType
{
    SteamID = 1,
    IP = 2
}

[Table("Bans")]
public class Ban : IBan
{
    [Key]
    public ulong Id { get; set; }

    [Column("SteamId64")]
    public ulong SteamId64 { get; set; }

    [Column("PlayerName")]
    public string PlayerName { get; set; } = string.Empty;

    [Column("PlayerIp")]
    public string PlayerIp { get; set; } = string.Empty;

    [Column("BanType")]
    public int BanType { get; set; }

    [Column("ExpiresAt")]
    public ulong ExpiresAt { get; set; }

    [Column("Length")]
    public ulong Length { get; set; }

    [Column("Reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("AdminSteamId64")]
    public ulong AdminSteamId64 { get; set; }

    [Column("AdminName")]
    public string AdminName { get; set; } = string.Empty;

    [Column("Server")]
    public string Server { get; set; } = string.Empty;

    [Column("GlobalBan")]
    public bool GlobalBan { get; set; }
}
