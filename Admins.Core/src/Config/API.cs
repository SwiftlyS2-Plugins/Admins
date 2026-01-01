using Admins.Core.Contract;
using Microsoft.Extensions.Options;

namespace Admins.Core.Config;

public class ConfigurationManager : IConfigurationManager
{
    private IOptionsMonitor<CoreConfiguration>? _configMonitor;

    public ConfigurationManager(IOptionsMonitor<CoreConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    public IOptionsMonitor<ICoreConfiguration>? GetConfigurationMonitor()
    {
        return _configMonitor;
    }

    public ICoreConfiguration? GetCurrentConfiguration()
    {
        return _configMonitor?.CurrentValue;
    }
}