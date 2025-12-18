using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Admins.Menu.Menu;

public class AdminMenu
{
    private ISwiftlyCore Core = null!;

    private IOptionsMonitor<CoreMenuConfiguration>? _config;

    private Dictionary<string, (string[], Func<IPlayer, string, string>, Func<IPlayer, IMenuAPI>)> _registeredSubmenus = [];

    public AdminMenu(ISwiftlyCore core, IOptionsMonitor<CoreMenuConfiguration> config)
    {
        core.Registrator.Register(this);
        _config = config;
        Core = core;
    }

    public IMenuAPI CreateAdminMenu(IPlayer player)
    {
        var translation = Core.Translation.GetPlayerLocalizer(player);
        var builder = Core.MenusAPI.CreateBuilder();

        builder.Design.SetMenuTitle(translation["adminmenu.title"])
            .Design.SetMenuFooterColor(_config!.CurrentValue.MenuColor)
            .Design.SetVisualGuideLineColor(_config!.CurrentValue.MenuColor)
            .Design.SetNavigationMarkerColor(_config!.CurrentValue.MenuColor);

        foreach (var entry in _registeredSubmenus)
        {
            if (!Core.Permission.PlayerHasPermissions(player.SteamID, entry.Value.Item1))
                continue;

            builder.AddOption(new SubmenuMenuOption(entry.Value.Item2(player, entry.Key), entry.Value.Item3(player)));
        }

        return builder.Build();
    }

    public void RegisterSubmenu(string translationKey, string[] permission, Func<IPlayer, string, string> getPlayerTranslationFromConsumer, Func<IPlayer, IMenuAPI> submenu)
    {
        _registeredSubmenus[translationKey] = (permission, getPlayerTranslationFromConsumer, submenu);
    }

    public void UnregisterSubmenu(string translationKey)
    {
        _registeredSubmenus.Remove(translationKey);
    }
}