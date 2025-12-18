using Microsoft.Extensions.Options;

namespace Admins.Core.Contract;

public interface IConfigurationManager
{
    /// <summary>
    /// Gets the configuration monitor for the core configuration.
    /// </summary>
    /// <returns>The configuration monitor for the core configuration.</returns>
    public IOptionsMonitor<ICoreConfiguration>? GetConfigurationMonitor();

    /// <summary>
    /// Gets the current core configuration.
    /// </summary>
    /// <returns>The current core configuration.</returns>
    public ICoreConfiguration? GetCurrentConfiguration();
}