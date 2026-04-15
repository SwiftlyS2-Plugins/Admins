using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins.SuperCommands.Commands;

public partial class ServerCommands
{
    [Command("giveitem", permission: "admins.commands.giveitem")]
    [CommandAlias("give")]
    public void Command_GiveItem(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 2, "giveitem", ["<player>", "<item_name>"]))
        {
            return;
        }

        var targetPlayers = FindTargetPlayers(context, context.Args[0]);
        if (targetPlayers == null)
        {
            return;
        }

        var players = new List<IPlayer>();
        foreach (var player in targetPlayers)
        {
            if (!CanApplyActionToPlayer(context.Sender!, player))
            {
                NotifyAdminOfImmunityProtection(context, GetPlayerName(player), GetPlayerImmunityLevel(player));
                continue;
            }
            players.Add(player);
        }

        var itemName = context.Args[1];
        var adminName = context.Sender!.Controller.PlayerName;
        foreach (var player in players)
        {
            GiveItemToPlayer(player, itemName);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.giveitem_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    itemName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("melee", permission: "admins.commands.melee")]
    public void Command_Melee(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "melee", ["<player>"]))
        {
            return;
        }

        var targetPlayers = FindTargetPlayers(context, context.Args[0]);
        if (targetPlayers == null)
        {
            return;
        }

        var players = new List<IPlayer>();
        foreach (var player in targetPlayers)
        {
            if (!CanApplyActionToPlayer(context.Sender!, player))
            {
                NotifyAdminOfImmunityProtection(context, GetPlayerName(player), GetPlayerImmunityLevel(player));
                continue;
            }
            players.Add(player);
        }

        var adminName = context.Sender!.Controller.PlayerName; 
        foreach (var player in players)
        {
            StripAndGiveKnife(player);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.melee_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("disarm", permission: "admins.commands.disarm")]
    public void Command_Disarm(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "disarm", ["<player>"]))
        {
            return;
        }

        var targetPlayers = FindTargetPlayers(context, context.Args[0]);
        if (targetPlayers == null)
        {
            return;
        }

        var players = new List<IPlayer>();
        foreach (var player in targetPlayers)
        {
            if (!CanApplyActionToPlayer(context.Sender!, player))
            {
                NotifyAdminOfImmunityProtection(context, GetPlayerName(player), GetPlayerImmunityLevel(player));
                continue;
            }
            players.Add(player);
        }

        var adminName = context.Sender!.Controller.PlayerName;
        foreach (var player in players)
        {
            RemoveAllItems(player);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.disarm_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("clean", permission: "admins.commands.clean")]
    public void Command_Clean(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        var entities = Core.EntitySystem.GetAllEntitiesByClass<CBaseEntity>().ToList();
        int count = 0;

        foreach (var entity in entities)
        {
            if (entity.DesignerName.StartsWith("weapon_") && entity.DesignerName != "weapon_c4" && !entity.OwnerEntity.IsValid)
            {
                entity.Despawn();
                count++;
            }
        }

        var adminName = context.Sender!.Controller.PlayerName;
        SendMessageToPlayers(Core.PlayerManager.GetAllPlayers().ToList(), (p, localizer) =>
        {
            return (localizer[
                "command.clean_success",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                adminName,
                count.ToString()
            ], MessageType.Chat);
        });
    }

    private void GiveItemToPlayer(IPlayer player, string itemName)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        var itemServices = pawn.ItemServices;
        if (itemServices != null && itemServices.IsValid)
        {
            itemServices.GiveItem(itemName);
        }
    }

    private void StripAndGiveKnife(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        var itemServices = pawn.ItemServices;
        if (itemServices != null && itemServices.IsValid)
        {
            itemServices.RemoveItems();
            itemServices.GiveItem("weapon_knife");
        }
    }

    private void RemoveAllItems(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        var itemServices = pawn.ItemServices;
        if (itemServices != null && itemServices.IsValid)
        {
            itemServices.RemoveItems();
        }
    }
}