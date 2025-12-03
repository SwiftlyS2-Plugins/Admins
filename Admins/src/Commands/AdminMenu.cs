using System.Security;
using Admins.Menu;
using SwiftlyS2.Shared.Commands;

namespace Admins.Commands;

public partial class AdminCommands
{
    [Command("admin", permission: "admins.commands.admin")]
    [CommandAlias("adminmenu")]
    public void Command_AdminMenu(ICommandContext ctx)
    {
        if (!ctx.IsSentByPlayer)
        {
            SendByPlayerOnly(ctx);
            return;
        }

        var player = ctx.Sender!;
        var adminMenu = Admins.AdminsMenuAPI.GenerateMenu(player);

        Core.MenusAPI.OpenMenuForPlayer(player, adminMenu);
    }
}