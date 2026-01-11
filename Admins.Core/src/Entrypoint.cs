using Admins.Core.Admins;
using Admins.Core.Commands;
using Admins.Core.Config;
using Admins.Core.Contract;
using Admins.Core.Database;
using Admins.Core.Groups;
using Admins.Core.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace Admins.Core;

[PluginMetadata(Id = "Admins.Core", Version = "1.0.0", Name = "Admins - Core", Author = "Swiftly Development Team", Description = "The admin system for your server.")]
public partial class AdminsCore : BasePlugin
{
    private ServiceProvider? _serviceProvider;

    public AdminsCore(ISwiftlyCore core) : base(core)
    {
        var connection = Core.Database.GetConnection("admins");
        MigrationRunner.RunMigrations(connection);
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<CoreConfiguration>("config.jsonc", "Main")
            .Configure(builder => builder.AddJsonFile("config.jsonc", false, true));

        ServiceCollection services = new();

        services
            .AddSwiftly(Core)
            .AddSingleton<ServerLoader>()
            .AddSingleton<ServerAdmins>()
            .AddSingleton<ServerGroups>()
            .AddSingleton<ServerManager>()
            .AddSingleton<GroupsManager>()
            .AddSingleton<AdminsManager>()
            .AddSingleton<ServerCommands>()
            .AddSingleton<Config.ConfigurationManager>()
            .AddOptionsWithValidateOnStart<CoreConfiguration>()
            .BindConfiguration("Main");

        _serviceProvider = services.BuildServiceProvider();
        var serverLoader = _serviceProvider.GetRequiredService<ServerLoader>();
        var serverAdmins = _serviceProvider.GetRequiredService<ServerAdmins>();
        var serverGroups = _serviceProvider.GetRequiredService<ServerGroups>();
        var serverManager = _serviceProvider.GetRequiredService<ServerManager>();
        var groupsManager = _serviceProvider.GetRequiredService<GroupsManager>();
        var adminsManager = _serviceProvider.GetRequiredService<AdminsManager>();
        var configurationManager = _serviceProvider.GetRequiredService<Config.ConfigurationManager>();
        _ = _serviceProvider.GetRequiredService<ServerCommands>();

        serverAdmins.SetAdminsManager(adminsManager);
        adminsManager.SetServerAdmins(serverAdmins);

        serverGroups.Load();
    }

    public override void Unload()
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IAdminsManager, AdminsManager>("Admins.Admins.V1", _serviceProvider!.GetRequiredService<AdminsManager>());
        interfaceManager.AddSharedInterface<IGroupsManager, GroupsManager>("Admins.Groups.V1", _serviceProvider!.GetRequiredService<GroupsManager>());
        interfaceManager.AddSharedInterface<IServerManager, ServerManager>("Admins.Server.V1", _serviceProvider!.GetRequiredService<ServerManager>());
        interfaceManager.AddSharedInterface<Contract.IConfigurationManager, Config.ConfigurationManager>("Admins.Configuration.V1", _serviceProvider!.GetRequiredService<Config.ConfigurationManager>());
    }
}