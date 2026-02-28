using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Admins.Comms.Contract;

namespace Admins.Comms.Database.Models;

[Table("admins-sanctions")]
public class Sanction : ISanction
{
    [Key]
    public long Id { get; set; }

    [Column("SteamId64")]
    public long SteamId64 { get; set; }

    [Column("PlayerName")]
    public string PlayerName { get; set; } = string.Empty;

    [Column("PlayerIp")]
    public string PlayerIp { get; set; } = string.Empty;

    [Column("SanctionType")]
    public SanctionType SanctionType { get; set; }

    [Column("SanctionKind")]
    public SanctionKind SanctionKind { get; set; }

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

    [Column("GlobalSanction")]
    public bool GlobalSanction { get; set; }

    [Column("CreatedAt")]
    public long CreatedAt { get; set; }

    [Column("UpdatedAt")]
    public long UpdatedAt { get; set; }
}