using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace Admins.SuperCommands.Commands;

public partial class ServerCommands
{
    [Command("restartround", permission: "admins.commands.restartround")]
    [CommandAlias("rr")]
    public void Command_RestartRound(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "restartround", ["<delay>"]))
        {
            return;
        }

        if (!TryParseFloat(context, context.Args[0], "delay", 0, 300, out var delay))
        {
            return;
        }

        var gameRules = Core.EntitySystem.GetGameRules();
        if (gameRules != null && gameRules.IsValid)
        {
            gameRules.TerminateRound(RoundEndReason.RoundDraw, delay);
        }

        var adminName = context.Sender!.Controller.PlayerName;
        SendMessageToPlayers(Core.PlayerManager.GetAllPlayers(), context.Sender!, (p, localizer) =>
        {
            return (localizer["command.restartround", ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, delay], MessageType.Chat);
        });
    }

    [Command("csay", permission: "admins.commands.csay")]
    public void Command_CSay(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "csay", ["<message>"]))
        {
            return;
        }

        var message = string.Join(" ", context.Args);
        var adminName = context.Sender!.Controller.PlayerName;
        Core.PlayerManager.SendCenter($"{adminName}: {message}");
    }

    [Command("rcon", permission: "admins.commands.rcon")]
    public void Command_Rcon(ICommandContext context)
    {
        if (!ValidateArgsCount(context, 1, "rcon", ["<command>"]))
        {
            return;
        }

        var rconCommand = string.Join(" ", context.Args);
        Core.Engine.ExecuteCommand(rconCommand);
    }
}