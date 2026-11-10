using System.ComponentModel;

namespace Admins.Services.ServerControl;

public sealed class ServerControlConfiguration
{
    [Description("Maps listed by the admin menu's 'Change map' page, e.g. 'de_dust2' or 'ws:3070284539'. Any installed map can also be typed.")]
    public List<string> Maps { get; set; } = [];
}
