using Admins.Core.Contract;

namespace Admins.Core.Config;

public class CoreConfiguration : ICoreConfiguration
{
    public string Prefix { get; set; } = "[[blue]SwiftlyS2[default]]";
    public bool UseDatabase { get; set; } = true;
    public string TimeZone { get; set; } = "UTC";
    public float BansDatabaseSyncIntervalSeconds { get; set; } = 30f;
}