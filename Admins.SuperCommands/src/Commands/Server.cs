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

        float delay = 1; // Default delay of 1 second before round restarts
        if (context.Args.Length > 0)
        {
            if (!TryParseFloat(context, context.Args[0], "delay", 0, 300, out delay))
            {
                SendSyntax(context, "restartround", ["<delay>"]);
                return;
            }
        }

        var gameRules = Core.EntitySystem.GetGameRules();
        if (gameRules != null && gameRules.IsValid)
        {
            gameRules.TerminateRound(RoundEndReason.RoundDraw, delay);
        }

        var adminName = context.Sender!.Controller.PlayerName;
        SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
        {
            return (localizer["command.restartround", ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, delay], MessageType.Chat);
        });
    }

    [Command("say", permission: "admins.commands.say")]
    public void Command_Say(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "say", ["<message>"]))
        {
            return;
        }

        var message = string.Join(" ", context.Args);
        var adminName = context.Sender!.Controller.PlayerName;
        SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
        {
            return (localizer["command.say", ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, message], MessageType.Chat);
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

    [Command("map", permission: "admins.commands.map")]
    [CommandAlias("changemap")]
    public void Command_Map(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "map", ["<map_name>"]))
        {
            return;
        }

        var mapName = context.Args[0];
        var adminName = context.Sender!.Controller.PlayerName;

        if (mapName.StartsWith("ws:"))
        {
            var issuedCommand = long.TryParse(mapName.Replace("ws:", ""), out var mapId)
                ? $"host_workshop_map {mapId}"
                : $"ds_workshop_changelevel {mapName.Replace("ws:", "")}";
                
            Core.Scheduler.DelayBySeconds(3.0f, () => Core.Engine.ExecuteCommand(issuedCommand));
        }
        else
        {
            if (!Core.Engine.IsMapValid(mapName))
            {
                var localizer = GetPlayerLocalizer(context);
                var syntax = localizer["command.map_not_found", ConfigurationManager.GetCurrentConfiguration()!.Prefix, mapName];
                context.Reply(syntax);
                return;
            }

            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                return (localizer["command.changing_map", ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, mapName], MessageType.Chat);
            });

            Core.Scheduler.DelayBySeconds(3.0f, () => Core.Engine.ExecuteCommand($"changelevel {mapName}"));
        }
    }
}