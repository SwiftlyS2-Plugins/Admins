using Admins.Bans.Commands;
using Admins.Bans.Configuration;
using Admins.Bans.Contract;
using Admins.Bans.Database;
using Admins.Bans.Manager;
using Admins.Bans.Players;
using Admins.Core.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace Admins.Bans;

[PluginMetadata(Id = "Admins.Bans", Version = "1.0.0", Name = "Admins - Bans", Author = "Swiftly Development Team", Description = "The admin system for your server.")]
public partial class AdminsBans : BasePlugin
{
    private ServiceProvider? _serviceProvider;
    private Core.Contract.IConfigurationManager? _configurationManager;
    private IServerManager? _serverManager;
    private ServerBans? _serverBans;
    private BansManager? _bansManager;
    private ServerCommands? _serverCommands;

    public AdminsBans(ISwiftlyCore core) : base(core)
    {
        var connection = Core.Database.GetConnection("admins");
        MigrationRunner.RunMigrations(connection);
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<BansConfiguration>("config.jsonc", "Main")
            .Configure(builder => builder.AddJsonFile("config.jsonc", false, true));

        ServiceCollection services = new();

        services
            .AddSwiftly(Core)
            .AddSingleton<GamePlayer>()
            .AddSingleton<BansManager>()
            .AddSingleton<ServerBans>()
            .AddSingleton<ServerCommands>()
            .AddOptionsWithValidateOnStart<BansConfiguration>()
            .BindConfiguration("Main");

        _serviceProvider = services.BuildServiceProvider();

        _ = _serviceProvider.GetRequiredService<GamePlayer>();

        _bansManager = _serviceProvider.GetRequiredService<BansManager>();
        _serverBans = _serviceProvider.GetRequiredService<ServerBans>();
        _serverCommands = _serviceProvider.GetRequiredService<ServerCommands>();
    }

    public override void Unload()
    {
    }

    public override void OnAllPluginsLoaded()
    {

    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IBansManager, BansManager>("Admins.Bans.V1", _serviceProvider!.GetRequiredService<BansManager>());
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("Admins.Configuration.V1"))
        {
            _configurationManager = interfaceManager.GetSharedInterface<Core.Contract.IConfigurationManager>("Admins.Configuration.V1");

            _serverBans!.SetConfigurationManager(_configurationManager);
            _bansManager!.SetConfigurationManager(_configurationManager);
            _serverCommands!.SetConfigurationManager(_configurationManager);
        }

        if (interfaceManager.HasSharedInterface("Admins.Server.V1"))
        {
            _serverManager = interfaceManager.GetSharedInterface<IServerManager>("Admins.Server.V1");

            _serverBans!.SetServerManager(_serverManager);
            _serverCommands!.SetServerManager(_serverManager);
        }

        _serverBans!.Load();
    }
}