using Admins.SuperCommands.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Plugins;

namespace Admins.SuperCommands;

[PluginMetadata(Id = "Admins.SuperCommands", Version = "1.0.0", Name = "Admins - SuperCommands", Author = "Swiftly Development Team", Description = "The admin super commands system for your server.")]
public partial class AdminsSuperCommands : BasePlugin
{
    private ServiceProvider? _serviceProvider;
    private Core.Contract.IConfigurationManager? _configurationManager;
    private ServerCommands? _serverCommands;

    public AdminsSuperCommands(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        ServiceCollection services = new();

        services
            .AddSwiftly(Core)
            .AddSingleton<ServerCommands>();

        _serviceProvider = services.BuildServiceProvider();

        _serverCommands = _serviceProvider.GetRequiredService<ServerCommands>();
    }

    public override void Unload()
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("Admins.Configuration.V1"))
        {
            _configurationManager = interfaceManager.GetSharedInterface<Core.Contract.IConfigurationManager>("Admins.Configuration.V1");

            _serverCommands!.SetConfigurationManager(_configurationManager);
        }
    }
}