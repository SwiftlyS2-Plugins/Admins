using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Admins.Core.Contract;
using Dommel;

namespace Admins.Core.Database.Models;

[Table("admins-admins")]
public class Admin : IAdmin
{
    [Key]
    public long Id { get; set; }

    [Column("SteamId64")]
    public long SteamId64 { get; set; }

    [Column("Username")]
    public string Username { get; set; } = string.Empty;

    [Column("Permissions")]
    public string PermissionsJson
    {
        get => JsonSerializer.Serialize(Permissions);
        set => Permissions = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
    }

    [Column("Groups")]
    public string GroupsJson
    {
        get => JsonSerializer.Serialize(Groups);
        set => Groups = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
    }

    [Column("Immunity")]
    public uint Immunity { get; set; }

    [Column("Servers")]
    public string ServersJson
    {
        get => JsonSerializer.Serialize(Servers);
        set => Servers = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
    }

    [Ignore]
    public List<string> Permissions { get; set; } = [];

    [Ignore]
    public List<string> Groups { get; set; } = [];

    [Ignore]
    public List<string> Servers { get; set; } = [];
}