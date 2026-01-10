using Admins.Comms.Commands;
using Admins.Comms.Configuration;
using Admins.Comms.Contract;
using Admins.Comms.Database;
using Admins.Comms.Manager;
using Admins.Comms.Menus;
using Admins.Comms.Players;
using Admins.Core.Contract;
using Admins.Menu.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace Admins.Comms;

[PluginMetadata(Id = "Admins.Comms", Version = "1.0.0", Name = "Admins - Comms", Author = "Swiftly Development Team", Description = "The admin system for your server.")]
public partial class AdminsComms : BasePlugin
{
    private ServiceProvider? _serviceProvider;
    private Core.Contract.IConfigurationManager? _configurationManager;
    private IServerManager? _serverManager;
    private IAdminMenuAPI? _adminMenuAPI;
    private ServerComms? _serverComms;
    private CommsManager? _commsManager;
    private ServerCommands? _serverCommands;
    private GamePlayer? _gamePlayer;
    private AdminMenu? _adminMenu;

    public AdminsComms(ISwiftlyCore core) : base(core)
    {
        var connection = Core.Database.GetConnection("admins");
        MigrationRunner.RunMigrations(connection);
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<CommsConfiguration>("config.jsonc", "Main")
            .Configure(builder => builder.AddJsonFile("config.jsonc", false, true));

        ServiceCollection services = new();

        services
            .AddSwiftly(Core)
            .AddSingleton<GamePlayer>()
            .AddSingleton<CommsManager>()
            .AddSingleton<ServerComms>()
            .AddSingleton<ServerCommands>()
            .AddSingleton<AdminMenu>()
            .AddOptionsWithValidateOnStart<CommsConfiguration>()
            .BindConfiguration("Main");

        _serviceProvider = services.BuildServiceProvider();

        _gamePlayer = _serviceProvider.GetRequiredService<GamePlayer>();
        _commsManager = _serviceProvider.GetRequiredService<CommsManager>();
        _serverComms = _serviceProvider.GetRequiredService<ServerComms>();
        _serverCommands = _serviceProvider.GetRequiredService<ServerCommands>();
        _adminMenu = _serviceProvider.GetRequiredService<AdminMenu>();
    }

    public override void Unload()
    {
        _adminMenu!.UnloadAdminMenu();
    }

    public override void OnAllPluginsLoaded()
    {

    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<ICommsManager, CommsManager>("Admins.Comms.V1", _serviceProvider!.GetRequiredService<CommsManager>());
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("Admins.Configuration.V1"))
        {
            _configurationManager = interfaceManager.GetSharedInterface<Core.Contract.IConfigurationManager>("Admins.Configuration.V1");

            _serverComms!.SetConfigurationManager(_configurationManager);
            _commsManager!.SetConfigurationManager(_configurationManager);
            _serverCommands!.SetConfigurationManager(_configurationManager);
            _gamePlayer!.SetConfigurationManager(_configurationManager);
            _adminMenu!.SetConfigurationManager(_configurationManager);
        }

        if (interfaceManager.HasSharedInterface("Admins.Server.V1"))
        {
            _serverManager = interfaceManager.GetSharedInterface<IServerManager>("Admins.Server.V1");

            _serverComms!.SetServerManager(_serverManager);
            _serverCommands!.SetServerManager(_serverManager);
            _adminMenu!.SetServerManager(_serverManager);
        }

        if (interfaceManager.HasSharedInterface("Admins.Menu.V1"))
        {
            _adminMenuAPI = interfaceManager.GetSharedInterface<IAdminMenuAPI>("Admins.Menu.V1");

            _adminMenu!.SetAdminMenuAPI(_adminMenuAPI);
        }

        _serverComms!.Load();
        _adminMenu!.LoadAdminMenu();
    }
}