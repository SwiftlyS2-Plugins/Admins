using System.ComponentModel;

namespace Admins.Services.Access;

public sealed class AccessConfiguration
{
    [Description("Seconds between reloads of admins and groups from the database. 0 disables periodic reloads (use !reloadadmins).")]
    public float DatabaseSyncIntervalSeconds { get; set; } = 60;
}
