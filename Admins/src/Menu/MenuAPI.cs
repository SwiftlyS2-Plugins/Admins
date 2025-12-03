using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Admins.Menu;

public class AdminMenuAPI
{
    private static Dictionary<string, (string[], Func<IPlayer, string, string>, IMenuAPI)> _registeredSubmenus = [];

    public static IMenuAPI GenerateMenu(IPlayer player)
    {
        var translation = Admins.SwiftlyCore.Translation.GetPlayerLocalizer(player);
        var builder = Admins.SwiftlyCore.MenusAPI.CreateBuilder();

        builder.Design.SetMenuTitle(translation["adminmenu.title"])
            .Design.SetMenuFooterColor(Admins.Config.CurrentValue.AdminMenuColor)
            .Design.SetVisualGuideLineColor(Admins.Config.CurrentValue.AdminMenuColor)
            .Design.SetNavigationMarkerColor(Admins.Config.CurrentValue.AdminMenuColor);

        foreach (var entry in _registeredSubmenus)
        {
            if (!Admins.SwiftlyCore.Permission.PlayerHasPermissions(player.SteamID, entry.Value.Item1))
                continue;

            builder.AddOption(new SubmenuMenuOption(entry.Value.Item2(player, entry.Key), entry.Value.Item3));
        }

        return builder.Build();
    }

    public static void RegisterSubmenu(string translationKey, string[] permission, Func<IPlayer, string, string> getPlayerTranslationFromConsumer, IMenuAPI submenu)
    {
        _registeredSubmenus[translationKey] = (permission, getPlayerTranslationFromConsumer, submenu);
    }

    public static void UnregisterSubmenu(string translationKey)
    {
        _registeredSubmenus.Remove(translationKey);
    }
}