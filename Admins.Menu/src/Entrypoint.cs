using Admins.Menu.API;
using Admins.Menu.Contract;
using Admins.Menu.Menu;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Plugins;

namespace Admins.Menu;

[PluginMetadata(Id = "Admins.Menu", Version = "1.0.0-b3", Name = "Admins - Menu", Author = "Swiftly Development Team", Description = "The admin menu system for your server.")]
public partial class AdminsMenu : BasePlugin
{
    private ServiceProvider? _serviceProvider;

    public AdminsMenu(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<CoreMenuConfiguration>("config.jsonc", "Main")
            .Configure(builder => builder.AddJsonFile("config.jsonc", false, true));

        ServiceCollection services = new();

        services
            .AddSwiftly(Core)
            .AddSingleton<AdminMenu>()
            .AddSingleton<AdminMenuAPI>()
            .AddOptionsWithValidateOnStart<CoreMenuConfiguration>()
            .BindConfiguration("Main");

        _serviceProvider = services.BuildServiceProvider();

        var adminMenu = _serviceProvider.GetRequiredService<AdminMenu>();
        var adminMenuAPI = _serviceProvider.GetRequiredService<AdminMenuAPI>();
    }

    public override void Unload()
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IAdminMenuAPI, AdminMenuAPI>("Admins.Menu.V1", _serviceProvider!.GetRequiredService<AdminMenuAPI>());
    }

    [Command("admin", permission: "admins.commands.admin")]
    public void OpenAdminMenuCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players.");
            return;
        }

        var adminMenu = _serviceProvider!.GetRequiredService<AdminMenu>();
        var menu = adminMenu.CreateAdminMenu(context.Sender!);

        Core.MenusAPI.OpenMenuForPlayer(context.Sender!, menu);
    }
}