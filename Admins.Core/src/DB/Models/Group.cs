using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Admins.Core.Contract;
using Dommel;

namespace Admins.Core.Database.Models;

[Table("groups")]
public class Group : IGroup
{
    [Key]
    public long Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Permissions")]
    public string PermissionsJson
    {
        get => JsonSerializer.Serialize(Permissions);
        set => Permissions = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
    }

    [Column("Servers")]
    public string ServersJson
    {
        get => JsonSerializer.Serialize(Servers);
        set => Servers = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
    }

    [Column("Immunity")]
    public uint Immunity { get; set; }

    [Ignore]
    public List<string> Permissions { get; set; } = new();

    [Ignore]
    public List<string> Servers { get; set; } = new();
}