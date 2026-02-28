using Admins.Bans.Commands;
using Admins.Bans.Configuration;
using Admins.Bans.Contract;
using Admins.Bans.Database;
using Admins.Bans.Manager;
using Admins.Bans.Menus;
using Admins.Bans.Players;
using Admins.Core.Contract;
using Admins.Menu.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace Admins.Bans;

[PluginMetadata(Id = "Admins.Bans", Version = "1.0.0-b1", Name = "Admins - Bans", Author = "Swiftly Development Team", Description = "The admin system for your server.")]
public partial class AdminsBans : BasePlugin
{
    private ServiceProvider? _serviceProvider;
    private Core.Contract.IConfigurationManager? _configurationManager;
    private IServerManager? _serverManager;
    private ServerBans? _serverBans;
    private BansManager? _bansManager;
    private ServerCommands? _serverCommands;
    private AdminMenu? _adminMenu;
    private IAdminMenuAPI? _adminMenuAPI;

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
            .AddSingleton<AdminMenu>()
            .AddOptionsWithValidateOnStart<BansConfiguration>()
            .BindConfiguration("Main");

        _serviceProvider = services.BuildServiceProvider();

        _ = _serviceProvider.GetRequiredService<GamePlayer>();

        _bansManager = _serviceProvider.GetRequiredService<BansManager>();
        _serverBans = _serviceProvider.GetRequiredService<ServerBans>();
        _serverCommands = _serviceProvider.GetRequiredService<ServerCommands>();
        _adminMenu = _serviceProvider.GetRequiredService<AdminMenu>();
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
        try
        {
            if (interfaceManager.HasSharedInterface("Admins.Configuration.V1"))
            {
                _configurationManager = interfaceManager.GetSharedInterface<Core.Contract.IConfigurationManager>("Admins.Configuration.V1");

                _serverBans!.SetConfigurationManager(_configurationManager);
                _bansManager!.SetConfigurationManager(_configurationManager);
                _serverCommands!.SetConfigurationManager(_configurationManager);
                _adminMenu!.SetConfigurationManager(_configurationManager);
            }
            else
            {
                Core.Logger.LogWarning("Admins.Core is not loaded yet. IConfigurationManager interface not available.");
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Failed to get IConfigurationManager from Admins.Core. Make sure Admins.Core is loaded before Admins.Bans.");
        }

        try
        {
            if (interfaceManager.HasSharedInterface("Admins.GamePlayer.V1"))
            {
                var coreGamePlayer = interfaceManager.GetSharedInterface<Core.Contract.IGamePlayer>("Admins.GamePlayer.V1");
                coreGamePlayer.SetBansManager(_bansManager);
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Failed to get GamePlayer from Admins.Core.");
        }

        try
        {
            if (interfaceManager.HasSharedInterface("Admins.Server.V1"))
            {
                _serverManager = interfaceManager.GetSharedInterface<IServerManager>("Admins.Server.V1");

                _serverBans!.SetServerManager(_serverManager);
                _serverCommands!.SetServerManager(_serverManager);
                _adminMenu!.SetServerManager(_serverManager);
            }
            else
            {
                Core.Logger.LogWarning("Admins.Core is not loaded yet. IServerManager interface not available.");
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Failed to get IServerManager from Admins.Core. Make sure Admins.Core is loaded before Admins.Bans.");
        }

        try
        {
            if (interfaceManager.HasSharedInterface("Admins.Menu.V1"))
            {
                _adminMenuAPI = interfaceManager.GetSharedInterface<IAdminMenuAPI>("Admins.Menu.V1");

                _adminMenu!.SetAdminMenuAPI(_adminMenuAPI);
            }
            else
            {
                Core.Logger.LogWarning("Admins.Menu is not loaded yet. IAdminMenuAPI interface not available.");
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Failed to get IAdminMenuAPI from Admins.Menu.");
        }

        if (_configurationManager != null)
        {
            _adminMenu!.LoadAdminMenu();
            StartOnlinePlayerBanCheckTimer();
        }
        else
        {
            Core.Logger.LogError("Cannot initialize Admins.Bans: Required dependencies not available. Make sure Admins.Core is loaded first.");
        }
    }

    private void StartOnlinePlayerBanCheckTimer()
    {
        if (_configurationManager?.GetConfigurationMonitor()?.CurrentValue == null)
            return;

        var intervalSeconds = _configurationManager.GetConfigurationMonitor()!.CurrentValue.BansDatabaseSyncIntervalSeconds;

        if (intervalSeconds > 0 && _configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase)
        {
            Core.Logger.LogInformation($"Starting online player ban check timer with interval of {intervalSeconds} seconds");

            var gamePlayer = _serviceProvider!.GetRequiredService<GamePlayer>();
            Core.Scheduler.RepeatBySeconds(intervalSeconds, () =>
            {
                gamePlayer.CheckAllOnlinePlayers();
            });
        }
        else
        {
            Core.Logger.LogInformation("Online player ban check is disabled (database not configured)");
        }
    }
}