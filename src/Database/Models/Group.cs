using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Admins.Contract;
using Dommel;

namespace Admins.Database.Models;

[Table("groups")]
public sealed class Group : IGroup
{
    [Key]
    public long Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Permissions")]
    public string PermissionsJson
    {
        get => JsonSerializer.Serialize(Permissions);
        set => Permissions = JsonList.Parse(value);
    }

    [Column("Servers")]
    public string ServersJson
    {
        get => JsonSerializer.Serialize(Servers);
        set => Servers = JsonList.Parse(value);
    }

    [Column("Immunity")]
    public int Immunity { get; set; }

    [Ignore]
    public List<string> Permissions { get; set; } = [];

    [Ignore]
    public List<string> Servers { get; set; } = [];

    public static Group From(IGroup source) => new Group
    {
        Id = source.Id,
        Name = source.Name,
        Immunity = source.Immunity,
        Permissions = [.. source.Permissions],
        Servers = [.. source.Servers],
    };
}
