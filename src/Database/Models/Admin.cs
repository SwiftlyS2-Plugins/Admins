using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Admins.Contract;
using Dommel;

namespace Admins.Database.Models;

[Table("admins")]
public sealed class Admin : IAdmin
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
        set => Permissions = JsonList.Parse(value);
    }

    [Column("Groups")]
    public string GroupsJson
    {
        get => JsonSerializer.Serialize(Groups);
        set => Groups = JsonList.Parse(value);
    }

    [Column("Immunity")]
    public int Immunity { get; set; }

    [Column("Servers")]
    public string ServersJson
    {
        get => JsonSerializer.Serialize(Servers);
        set => Servers = JsonList.Parse(value);
    }

    [Ignore]
    public List<string> Permissions { get; set; } = [];

    [Ignore]
    public List<string> Groups { get; set; } = [];

    [Ignore]
    public List<string> Servers { get; set; } = [];

    public static Admin From(IAdmin source) => new Admin
    {
        Id = source.Id,
        SteamId64 = source.SteamId64,
        Username = source.Username,
        Immunity = source.Immunity,
        Permissions = [.. source.Permissions],
        Groups = [.. source.Groups],
        Servers = [.. source.Servers],
    };
}
