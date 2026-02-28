using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Admins.Core.Contract;

namespace Admins.Core.Database.Models;

[Table("servers")]
public class Server : IServer
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