using System.ComponentModel;
using Admins.Contract;
using Admins.Services.Access;
using Admins.Services.Bans;
using Admins.Services.Comms;
using Admins.Services.Esp;
using Admins.Services.PlayerControl;
using Admins.Services.Presence;
using Admins.Services.ServerControl;

namespace Admins.Models;

public sealed class Configuration
{
    [Description("Text shown in front of every chat message of the plugin. Supports color tags.")]
    public string Prefix { get; set; } = "[[blue]SwiftlyS2[default]]";

    [Description("Store admins, groups, bans and sanctions in the database connection named 'admins'. When false nothing is persisted and admins can only be supplied through the API.")]
    public bool UseDatabase { get; set; } = true;

    [Description("Time zone used to display dates, as an IANA or Windows id (e.g. 'Europe/Vilnius'). Unknown ids fall back to UTC.")]
    public string TimeZone { get; set; } = "UTC";

    [Description("How immunity is compared when an admin targets another admin. 0 = ignore immunity, 1 = an admin can target admins with lower or equal immunity, 2 = an admin can only target admins with strictly lower immunity, 3 = like 2, but two admins without immunity can target each other. Players without an admin record can always be targeted.")]
    public ImmunityMode ImmunityMode { get; set; } = ImmunityMode.ProtectFromLowerAccess;

    public AccessConfiguration Admins { get; set; } = new();

    public BansConfiguration Bans { get; set; } = new();

    public CommsConfiguration Comms { get; set; } = new();

    public KickConfiguration Kick { get; set; } = new();

    public ServerControlConfiguration Server { get; set; } = new();

    public PresenceConfiguration Presence { get; set; } = new();

    public StatusConfiguration Status { get; set; } = new();

    public EspConfiguration Esp { get; set; } = new();
}
