using Admins.Contract;
using Admins.Database;
using Admins.Models;
using Admins.Services.Access;
using Admins.Services.Bans;
using Admins.Services.Comms;
using Admins.Services.Menus;
using Admins.Services.Notifications;
using Admins.Services.PlayerControl;
using Admins.Services.Server;
using Admins.Services.ServerControl;
using Admins.Services.Votes;
using Admins.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace Admins;

[PluginMetadata(Id = "Admins", Version = "2.0.0", Name = "Admins", Author = "Swiftly Development Team", Description = "Admins, groups, bans, gags and mutes, player commands and the admin menu.")]
public sealed class AdminsPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? _services;

    public override void Load(bool hotReload)
    {
        LegacyConfigImport.Run(Core);

        Core.Configuration
            .InitializeJsonWithModel<Configuration>("config.jsonc", "Main", addMissingKeys: true)
            .Configure(builder => builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true));

        var services = new ServiceCollection();
        services
            .AddSwiftly(Core)
            .AddSingleton<AdminsDatabase>()
            .AddSingleton<Messenger>()
            .AddSingleton<TargetResolver>()
            .AddSingleton<MenuShortcuts>()
            .AddSingleton<ServerService>()
            .AddSingleton<AccessService>()
            .AddSingleton<BansService>()
            .AddSingleton<CommsService>()
            .AddSingleton<PlayerControlService>()
            .AddSingleton<ServerControlService>()
            .AddSingleton<VotesService>()
            .AddSingleton<RecordNotifier>()
            .AddSingleton<AdminMenuService>()
            .AddOptionsWithValidateOnStart<Configuration>()
            // The binder appends file entries to list initializers, so empty them first or every default shows twice.
            .Configure(c =>
            {
                c.Bans.Reasons.Clear();
                c.Bans.DurationsInSeconds.Clear();
                c.Comms.Reasons.Clear();
                c.Comms.DurationsInSeconds.Clear();
            })
            .BindConfiguration("Main");

        _services = services.BuildServiceProvider();

        // Before any service opens the database.
        if (_services.GetRequiredService<IOptionsMonitor<Configuration>>().CurrentValue.UseDatabase)
        {
            MigrationRunner.Run(Core);
        }

        // Resolving constructs each service, which registers its commands and listeners.
        _services.GetRequiredService<ServerService>().Start(hotReload);
        _services.GetRequiredService<AccessService>();
        _services.GetRequiredService<BansService>();
        _services.GetRequiredService<CommsService>();
        _services.GetRequiredService<PlayerControlService>();
        _services.GetRequiredService<ServerControlService>();
        _services.GetRequiredService<VotesService>();
        _services.GetRequiredService<RecordNotifier>();
        _services.GetRequiredService<AdminMenuService>();
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        if (_services is null) return;

        var access = _services.GetRequiredService<AccessService>();
        interfaceManager.AddSharedInterface<IAdminsManager, AccessService>(AdminsInterfaceKeys.Admins, access);
        interfaceManager.AddSharedInterface<IGroupsManager, AccessService>(AdminsInterfaceKeys.Groups, access);
        interfaceManager.AddSharedInterface<IServerManager, ServerService>(AdminsInterfaceKeys.Server, _services.GetRequiredService<ServerService>());
        interfaceManager.AddSharedInterface<IBansManager, BansService>(AdminsInterfaceKeys.Bans, _services.GetRequiredService<BansService>());
        interfaceManager.AddSharedInterface<ICommsManager, CommsService>(AdminsInterfaceKeys.Comms, _services.GetRequiredService<CommsService>());
        interfaceManager.AddSharedInterface<IAdminMenuAPI, AdminMenuService>(AdminsInterfaceKeys.Menu, _services.GetRequiredService<AdminMenuService>());
    }

    public override void Unload()
    {
        _services?.Dispose();
        _services = null;
    }
}
