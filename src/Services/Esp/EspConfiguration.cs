using System.ComponentModel;

namespace Admins.Services.Esp;

public sealed class EspConfiguration
{
    [Description("Also show the outlines while the admin is alive. When false they only show while the admin is dead or spectating, so ESP cannot be used as a wallhack in play.")]
    public bool AllowWhileAlive { get; set; } = false;
}
