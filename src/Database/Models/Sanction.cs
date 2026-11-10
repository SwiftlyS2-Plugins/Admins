using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Admins.Contract;
using Dommel;

namespace Admins.Database.Models;

[Table("sanctions")]
public sealed class Sanction : ISanction
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

    /// <summary>As last written: a sanction that ran out reads Active here until the next sync marks it Expired.</summary>
    [Column("Status")]
    public PunishmentStatus StoredStatus { get; set; }

    [Ignore]
    public PunishmentStatus Status => StatusAt(AdminsDatabase.NowMs);

    public bool IsActive(long nowMs) => ExpiresAt == 0 || ExpiresAt > nowMs;

    public PunishmentStatus StatusAt(long nowMs) =>
        IsActive(nowMs) ? PunishmentStatus.Active : StoredStatus == PunishmentStatus.Lifted ? PunishmentStatus.Lifted : PunishmentStatus.Expired;

    public static Sanction From(ISanction source) => new Sanction
    {
        Id = source.Id,
        SteamId64 = source.SteamId64,
        PlayerName = source.PlayerName,
        PlayerIp = source.PlayerIp,
        SanctionType = source.SanctionType,
        SanctionKind = source.SanctionKind,
        ExpiresAt = source.ExpiresAt,
        Length = source.Length,
        Reason = source.Reason,
        AdminSteamId64 = source.AdminSteamId64,
        AdminName = source.AdminName,
        Server = source.Server,
        GlobalSanction = source.GlobalSanction,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        StoredStatus = source.Status,
    };
}
