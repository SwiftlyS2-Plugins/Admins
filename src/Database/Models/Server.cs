using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Admins.Contract;

namespace Admins.Database.Models;

[Table("servers")]
public sealed class Server : IServer
{
    [Key]
    public long Id { get; set; }

    [Column("IP")]
    public string IP { get; set; } = string.Empty;

    [Column("Port")]
    public int Port { get; set; }

    [Column("Hostname")]
    public string Hostname { get; set; } = string.Empty;

    [Column("GUID")]
    public string GUID { get; set; } = string.Empty;
}
