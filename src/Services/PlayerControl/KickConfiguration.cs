using System.ComponentModel;

namespace Admins.Services.PlayerControl;

public sealed class KickConfiguration
{
    [Description("Reasons offered by the kick menu, after 'No reason'. A reason can also be typed.")]
    public List<string> Reasons { get; set; } = ["AFK", "Mic spam", "Toxic", "Bad name", "Other"];
}
