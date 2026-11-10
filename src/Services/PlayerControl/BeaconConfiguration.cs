using System.ComponentModel;

namespace Admins.Services.PlayerControl;

public sealed class BeaconConfiguration
{
    [Description("Sound event a beacon plays from the player on each pulse, for everyone. Players only hear it once your server's addon ships it: see 'Beacon sound' on the wiki's Commands page. Empty plays none.")]
    public string Sound { get; set; } = "admins.beacon_beep";

    [Description("Soundevents file the plugin precaches so players load Sound, e.g. 'soundevents/admins.vsndevts'. Applies from the next map. Empty precaches nothing.")]
    public string SoundEventsFile { get; set; } = string.Empty;
}
