using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins.SuperCommands.Commands;

public partial class ServerCommands
{
    // globals
    public List<IPlayer> _beaconPlayers = new();
    public Dictionary<IPlayer, CancellationTokenSource> _beaconEffectTimerToken = new();

    [Command("hp", permission: "admins.commands.hp")]
    [CommandAlias("health")]
    public void Command_HP(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 2, "hp", ["<player>", "<health>", "[armour]", "[helmet]"]))
        {
            return;
        }

        var targetPlayers = FindTargetPlayers(context, context.Args[0]);
        if (targetPlayers == null)
        {
            return;
        }

        if (!TryParseInt(context, context.Args[1], "health", 0, 100, out var health))
        {
            return;
        }

        var armour = 0;
        if (context.Args.Length >= 3 && !TryParseInt(context, context.Args[2], "armour", 0, 100, out armour))
        {
            return;
        }

        var helmet = false;
        if (context.Args.Length >= 4 && !TryParseBool(context, context.Args[3], "helmet", out helmet))
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
            ApplyHealthAndArmor(player, health, armour, helmet);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.hp_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName,
                    health,
                    armour,
                    helmet
                ], MessageType.Chat);
            });
        }
    }

    [Command("freeze", permission: "admins.commands.freeze")]
    public void Command_Freeze(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "freeze", ["<player>"]))
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
            SetPlayerMoveType(player, MoveType_t.MOVETYPE_INVALID);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.freeze_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("unfreeze", permission: "admins.commands.unfreeze")]
    public void Command_Unfreeze(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "unfreeze", ["<player>"]))
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
            SetPlayerMoveType(player, MoveType_t.MOVETYPE_WALK);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.unfreeze_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("kick", permission: "admins.commands.kick")]
    public void Command_Kick(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "kick", ["<player>"]))
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
            player.Kick($"Kicked by admin {adminName}", SwiftlyS2.Shared.ProtobufDefinitions.ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.kick_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("beacon", permission: "admins.commands.beacon")]
    public void Command_Beacon(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        var players = new List<IPlayer>();

        if (context.Args.Length < 1)
        {
            players.Add(context.Sender!); // If no player argument is provided, apply to the command sender
        }
        else 
        {
            var targetPlayers = FindTargetPlayers(context, context.Args[0]);
            if (targetPlayers == null)
            {
                return;
            }

            foreach (var player in targetPlayers)
            {
                if (!CanApplyActionToPlayer(context.Sender!, player))
                {
                    NotifyAdminOfImmunityProtection(context, GetPlayerName(player), GetPlayerImmunityLevel(player));
                    continue;
                }
                players.Add(player);
            }
        }

        var adminName = context.Sender!.Controller.PlayerName; 
        var Localizer = GetPlayerLocalizer(context);
        foreach (var player in players)
        {
            var playerName = GetPlayerName(player);

            if (!IsValidAlivePawn(player.PlayerPawn))
            {
                context.Reply(Localizer["command.target_not_alive", ConfigurationManager.GetCurrentConfiguration()!.Prefix, playerName]);
                return;
            }

            var IsAlreadyHaveBeacon = false;
            if(_beaconPlayers.Contains(player))
            {
                StopBeaconOnPlayer(player);
                IsAlreadyHaveBeacon = true;
            }
            else
            {
                StartBeaconOnPlayer(player);
            }

            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                return (localizer[
                    "command.beacon_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    IsAlreadyHaveBeacon ? "[red]disabled" : "[green]enabled",
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("noclip", permission: "admins.commands.noclip")]
    public void Command_Noclip(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        var players = new List<IPlayer>();

        if (context.Args.Length < 1)
        {
            players.Add(context.Sender!); // If no player argument is provided, apply to the command sender
        }
        else 
        {
            var targetPlayers = FindTargetPlayers(context, context.Args[0]);
            if (targetPlayers == null)
            {
                return;
            }

            foreach (var player in targetPlayers)
            {
                if (!CanApplyActionToPlayer(context.Sender!, player))
                {
                    NotifyAdminOfImmunityProtection(context, GetPlayerName(player), GetPlayerImmunityLevel(player));
                    continue;
                }
                players.Add(player);
            }
        }

        var adminName = context.Sender!.Controller.PlayerName; 
        var Localizer = GetPlayerLocalizer(context);
        foreach (var player in players)
        {
            var playerName = GetPlayerName(player);

            if (!IsValidAlivePawn(player.PlayerPawn))
            {
                context.Reply(Localizer["command.target_not_alive", ConfigurationManager.GetCurrentConfiguration()!.Prefix, playerName]);
                return;
            }

            var IsAlreadyNoclip = player.PlayerPawn!.ActualMoveType == MoveType_t.MOVETYPE_NOCLIP;
            if(IsAlreadyNoclip) SetPlayerMoveType(player, MoveType_t.MOVETYPE_WALK);
            else SetPlayerMoveType(player, MoveType_t.MOVETYPE_NOCLIP);

            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                return (localizer[
                    "command.noclip_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    IsAlreadyNoclip ? "[red]disabled" : "[green]enabled",
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("setspeed", permission: "admins.commands.setspeed")]
    [CommandAlias("speed")]
    public void Command_SetSpeed(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "setspeed", ["<player>", "<speed_multiplier>"]))
        {
            return;
        }

        var speedMultiplier = 1.0f; // Default Speed
        if (context.Args.Length >= 2 && !TryParseFloat(context, context.Args[1], "speed_multiplier", 0.1f, 10.0f, out speedMultiplier))
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
        var Localizer = GetPlayerLocalizer(context);
        foreach (var player in players)
        {
            var playerName = GetPlayerName(player);
            var pawn = player.PlayerPawn;

            if (!IsValidAlivePawn(pawn))
            {
                context.Reply(Localizer["command.target_not_alive", ConfigurationManager.GetCurrentConfiguration()!.Prefix, playerName]);
                return;
            }

            pawn!.VelocityModifier = speedMultiplier;
            pawn.VelocityModifierUpdated();

            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                return (localizer[
                    "command.setspeed_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    speedMultiplier,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("setgravity", permission: "admins.commands.setgravity")]
    [CommandAlias("gravity")]
    public void Command_SetGravity(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "setgravity", ["<player>", "<gravity_multiplier>"]))
        {
            return;
        }

        var gravityMultiplier = 1.0f; // Default gravity multiplier
        if (context.Args.Length >= 2 && !TryParseFloat(context, context.Args[1], "gravity_multiplier", 0.1f, 10.0f, out gravityMultiplier))
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
        var Localizer = GetPlayerLocalizer(context);
        foreach (var player in players)
        {
            var playerName = GetPlayerName(player);
            var pawn = player.PlayerPawn;

            if (!IsValidAlivePawn(pawn))
            {
                context.Reply(Localizer["command.target_not_alive", ConfigurationManager.GetCurrentConfiguration()!.Prefix, playerName]);
                return;
            }

            pawn!.ActualGravityScale = gravityMultiplier;
            pawn!.GravityScale = gravityMultiplier;
            pawn.GravityScaleUpdated();

            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                return (localizer[
                    "command.setgravity_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    gravityMultiplier,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("slay", permission: "admins.commands.slay")]
    public void Command_Slay(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "slay", ["<player>"]))
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
            SlayPlayer(player);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.slay_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("slap", permission: "admins.commands.slap")]
    public void Command_Slap(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "slap", ["<player>", "[damage]"]))
        {
            return;
        }

        var targetPlayers = FindTargetPlayers(context, context.Args[0]);
        if (targetPlayers == null)
        {
            return;
        }

        var damage = 0;
        if (context.Args.Length >= 2 && !TryParseInt(context, context.Args[1], "damage", 0, 100, out damage))
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
            ApplySlap(player, damage);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.slap_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("rename", permission: "admins.commands.rename")]
    public void Command_Rename(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 2, "rename", ["<player>", "<new_name>"]))
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

        var newName = context.Args[1];

        var adminName = context.Sender!.Controller.PlayerName; 
        foreach (var player in players)
        {
            if (player.Controller != null && player.Controller.IsValid)
            {
                var oldName = $"{player.Controller.PlayerName}";
                player.Controller.PlayerName = newName;
                player.Controller.PlayerNameUpdated();
                SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                {
                    return (localizer[
                        "command.rename_success",
                        ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                        adminName,
                        oldName,
                        newName
                    ], MessageType.Chat);
                });
            }
        }
    }

    [Command("givemoney", permission: "admins.commands.givemoney")]
    [CommandAlias("money")]
    public void Command_GiveMoney(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 2, "givemoney", ["<player>", "<amount>"]))
        {
            return;
        }

        var targetPlayers = FindTargetPlayers(context, context.Args[0]);
        if (targetPlayers == null)
        {
            return;
        }

        if (!TryParseInt(context, context.Args[1], "amount", 1, 16000, out var amount))
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
            ModifyPlayerMoney(player, amount, isAdditive: true);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.givemoney_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    amount,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("setmoney", permission: "admins.commands.setmoney")]
    public void Command_SetMoney(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 2, "setmoney", ["<player>", "<amount>"]))
        {
            return;
        }

        var targetPlayers = FindTargetPlayers(context, context.Args[0]);
        if (targetPlayers == null)
        {
            return;
        }

        if (!TryParseInt(context, context.Args[1], "amount", 0, 16000, out var amount))
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
            ModifyPlayerMoney(player, amount, isAdditive: false);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.setmoney_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    amount,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    private void ApplyHealthAndArmor(IPlayer player, int health, int armour, bool helmet)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        if (health <= 0)
        {
            pawn.CommitSuicide(false, false);
        }
        else
        {
            pawn.Health = health;
            pawn.HealthUpdated();
        }

        var itemServices = pawn.ItemServices;
        var weaponServices = pawn.WeaponServices;
        if (itemServices != null && itemServices.IsValid && weaponServices != null && weaponServices.IsValid)
        {
            if (helmet)
            {
                itemServices.GiveItem("item_assaultsuit");
            }
            else
            {
                var weapons = weaponServices.MyValidWeapons;
                foreach (var weapon in weapons)
                {
                    if (weapon.AttributeManager.Item.ItemDefinitionIndex == 51)
                    {
                        weaponServices.RemoveWeapon(weapon);
                        break;
                    }
                }
            }
        }

        pawn.ArmorValue = armour;
        pawn.ArmorValueUpdated();
    }

    private void ModifyPlayerMoney(IPlayer player, int amount, bool isAdditive)
    {
        var moneyServices = player.Controller.InGameMoneyServices;
        if (moneyServices != null && moneyServices.IsValid)
        {
            if (isAdditive)
            {
                moneyServices.Account += amount;
            }
            else
            {
                moneyServices.Account = amount;
            }
            moneyServices.AccountUpdated();
        }
    }

    private void SetPlayerMoveType(IPlayer player, MoveType_t moveType)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        pawn.ActualMoveType = moveType;
        pawn.MoveType = moveType;
        pawn.MoveTypeUpdated();
    }

    private bool IsValidAlivePawn(CCSPlayerPawn? pawn)
    {
        return pawn != null && pawn!.IsValid && pawn!.LifeState == (byte)LifeState_t.LIFE_ALIVE;
    }

    private void SlayPlayer(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        pawn.CommitSuicide(false, false);
    }

    private void ApplySlap(IPlayer player, int damage)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        pawn.Health = Math.Max(pawn.Health - damage, 0);
        pawn.HealthUpdated();

        if (pawn.Health == 0)
        {
            pawn.CommitSuicide(false, false);
        }
        else
        {
            var velocity = new Vector(
                (float)Random.Shared.NextInt64(50, 230) * (Random.Shared.NextDouble() < 0.5 ? -1 : 1),
                (float)Random.Shared.NextInt64(50, 230) * (Random.Shared.NextDouble() < 0.5 ? -1 : 1),
                Random.Shared.NextInt64(100, 300)
            );

            pawn.Teleport(null, null, velocity);
        }
    }

    [Command("god", permission: "admins.commands.god")]
    public void Command_God(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "god", ["<player>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        var adminName = context.Sender!.Controller.PlayerName; 
        foreach (var player in players)
        {
            ToggleGodMode(player);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.god_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    player.PlayerPawn!.TakesDamage ? "[red]disabled" : "[green]enabled",
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("respawn", permission: "admins.commands.respawn")]
    public void Command_Respawn(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "respawn", ["<player>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        var adminName = context.Sender!.Controller.PlayerName; 
        foreach (var player in players)
        {
            RespawnPlayer(player);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.respawn_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }

        
        
    }

    [Command("swap", permission: "admins.commands.swap")]
    public void Command_Swap(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "swap", ["<player>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        var adminName = context.Sender!.Controller.PlayerName; 
        foreach (var player in players)
        {
            SwapPlayerTeam(player);
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.swap_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("team", permission: "admins.commands.team")]
    public void Command_Team(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 2, "team", ["<player>", "<team>"]))
        {
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        var teamName = context.Args[1].ToLower();
        var targetTeam = GetTeamFromName(teamName);

        if (targetTeam == null)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.team_invalid", ConfigurationManager.GetCurrentConfiguration()!.Prefix, teamName]);
            return;
        }

        var adminName = context.Sender!.Controller.PlayerName;
        foreach (var player in players)
        {
            if(!player.IsAlive) player.ChangeTeam(targetTeam.Value);
            else player.SwitchTeam(targetTeam.Value);
            
            var TargetteamName = targetTeam.Value switch
            {
                Team.CT => "CT",
                Team.T => "T",
                Team.Spectator => "Spectator",
                Team.None => "None",
                _ => "Unknown"
            };

            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(player);
                return (localizer[
                    "command.team_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName,
                    teamName
                ], MessageType.Chat);
            });
        }
    }

    [Command("goto", permission: "admins.commands.goto")]
    public void Command_Goto(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "goto", ["<player>"]) || string.IsNullOrWhiteSpace(context.Args[0]))
        {
            if (context.Args.Length > 0 && string.IsNullOrWhiteSpace(context.Args[0]))
            {
                var localizer = GetPlayerLocalizer(context);
                context.Reply(localizer[
                    "command.syntax",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    context.Prefix,
                    "goto",
                    "<player>"
                ]);
            }
            return;
        }

        var adminPawn = context.Sender!.PlayerPawn;
        if (!IsValidAlivePawn(adminPawn))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.self_dead", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null || players.Count == 0)
        {
            return;
        }

        var targetPlayer = players.FirstOrDefault(p => p.Slot != context.Sender!.Slot);
        if (targetPlayer == null)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.no_valid_targets", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var targetPawn = targetPlayer.PlayerPawn;
        if (!IsValidAlivePawn(targetPawn))
        {
            var localizer = GetPlayerLocalizer(context);
            var playerName = GetPlayerName(targetPlayer);
            context.Reply(localizer["command.target_not_alive", ConfigurationManager.GetCurrentConfiguration()!.Prefix, playerName]);
            return;
        }

        var origin = targetPawn!.AbsOrigin;
        var rotation = targetPawn.AbsRotation;

        if (origin != null && rotation != null)
        {
            rotation.Value.ToDirectionVectors(out var forward, out var right, out var up);

            // Teleport 100 units behind the target player
            var safeOrigin = new Vector(
                origin.Value.X - (forward.X * 100f),
                origin.Value.Y - (forward.Y * 100f),
                origin.Value.Z
            );
            
            adminPawn!.Teleport(safeOrigin, rotation, new Vector(0, 0, 0));

            var adminName = context.Sender!.Controller.PlayerName; 
            SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
            {
                var playerName = GetPlayerName(targetPlayer);
                return (localizer[
                    "command.goto_success",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    adminName,
                    playerName
                ], MessageType.Chat);
            });
        }
    }

    [Command("bring", permission: "admins.commands.bring")]
    public void Command_Bring(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "bring", ["<player>"]) || string.IsNullOrWhiteSpace(context.Args[0]))
        {
            if (context.Args.Length > 0 && string.IsNullOrWhiteSpace(context.Args[0]))
            {
                var localizer = GetPlayerLocalizer(context);
                context.Reply(localizer[
                    "command.syntax",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    context.Prefix,
                    "bring",
                    "<player>"
                ]);
            }
            return;
        }

        var adminPawn = context.Sender!.PlayerPawn;
        if (!IsValidAlivePawn(adminPawn))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.self_dead", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null || players.Count == 0)
        {
            return;
        }

        var validPlayers = players.Where(p =>  p.Slot != context.Sender.Slot).ToList();

        if (validPlayers.Count == 0)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.no_valid_targets", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var origin = adminPawn!.AbsOrigin;
        var rotation = adminPawn.AbsRotation;

        if (origin != null && rotation != null)
        {
            rotation.Value.ToDirectionVectors(out var forward, out var right, out var up);

            // Teleport 100 units in front of the admin
            var safeOrigin = new Vector(
                origin.Value.X + (forward.X * 100f),
                origin.Value.Y + (forward.Y * 100f),
                origin.Value.Z
            );

            var adminName = context.Sender!.Controller.PlayerName; 
            foreach (var player in validPlayers)
            {
                var playerName = GetPlayerName(player);
                var targetPawn = player.PlayerPawn;
                if (!IsValidAlivePawn(targetPawn))
                {
                    var localizer = GetPlayerLocalizer(context);
                    context.Reply(localizer["command.target_not_alive", ConfigurationManager.GetCurrentConfiguration()!.Prefix, playerName]);
                    return;
                }

                targetPawn!.Teleport(safeOrigin, rotation, new Vector(0, 0, 0));
                SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                {
                    return (localizer[
                        "command.bring_success",
                        ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                        adminName,
                        playerName
                    ], MessageType.Chat);
                });
            }
        }
    }

    private void ToggleGodMode(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        pawn.TakesDamage = !pawn.TakesDamage;
        pawn.TakesDamageUpdated();
    }

    private void RespawnPlayer(IPlayer player)
    {
        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
        {
            return;
        }

        controller.Respawn();
    }

    private void SwapPlayerTeam(IPlayer player)
    {
        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
        {
            return;
        }

        var currentTeam = controller.TeamNum;
        var newTeam = currentTeam == (byte)Team.T
            ? Team.CT
            : Team.T;

        player.SwitchTeam(newTeam);
    }

    private Team? GetTeamFromName(string teamName)
    {
        return teamName switch
        {
            "ct" or "counterterrorist" or "3" => Team.CT,
            "t" or "terrorist" or "2" => Team.T,
            "spec" or "spectator" or "1" => Team.Spectator,
            "none" or "0" => Team.None,
            _ => null
        };
    }

    private void StartBeaconOnPlayer(IPlayer player)
    {
        if(player == null || !player.IsValid)return;

        _beaconPlayers.Add(player);
        _beaconEffectTimerToken[player] = Core.Scheduler.RepeatBySeconds(1f, ()=>
        {
            DrawBeaconOnPlayer(player);
        });
    }

    private void StopBeaconOnPlayer(IPlayer player)
    {
        if(player == null || !player.IsValid)return;

        if(_beaconEffectTimerToken.TryGetValue(player, out var token))
        {
            token.Cancel();
            _beaconEffectTimerToken.Remove(player);
        }
        _beaconPlayers.Remove(player);
    }

    public void DrawBeaconOnPlayer(IPlayer? player)
    {
        if(player == null || !player.IsValid || player.Pawn == null || player.PlayerPawn == null || player.Pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.PlayerPawn.AbsOrigin == null)return;
        
        Vector mid =  new Vector(player.PlayerPawn.AbsOrigin.Value.X ,player.PlayerPawn.AbsOrigin.Value.Y, player.PlayerPawn.AbsOrigin.Value.Z);

        int lines = 20;
        int[] ent = new int[lines];
        CBeam?[] beam_ent = new CBeam?[lines];

        // draw piecewise approx by stepping angle
        // and joining points with a dot to dot
        float step = (float)(2.0f * Math.PI) / (float)lines;
        float radius = 20.0f;

        float angle_old = 0.0f;
        float angle_cur = step;

        float BeaconTimerSecond = 0.0f;

        
        for(int i = 0; i < lines; i++) // Drawing Beacon Circle
        {
            Vector start = angle_on_circle(angle_old, radius, mid);
            Vector end = angle_on_circle(angle_cur, radius, mid);

            if(player.Controller.TeamNum == 2)
            {
                var result = DrawLaserBetween(start, end, Color.FromBuiltin(System.Drawing.Color.Red), 1.0f, 2.0f);
                ent[i] = result.Item1;
                beam_ent[i] = result.Item2;
            } 
            if(player.Controller.TeamNum == 3)
            {
                var result = DrawLaserBetween(start, end, Color.FromBuiltin(System.Drawing.Color.Blue), 1.0f, 2.0f);
                ent[i] = result.Item1;
                beam_ent[i] = result.Item2;
            }

            angle_old = angle_cur;
            angle_cur += step;
        }
        
        Core.Scheduler.DelayAndRepeatBySeconds(0.1f, 0.1f, ()=>
        {
            if (BeaconTimerSecond >= 0.9f)
            {
                return;
            }
            for(int i = 0; i < lines; i++) // Moving Beacon Circle
            {
                Vector start = angle_on_circle(angle_old, radius, mid);
                Vector end = angle_on_circle(angle_cur, radius, mid);

                TeleportLaser(beam_ent[i], start, end);

                angle_old = angle_cur;
                angle_cur += step;
            }
            radius += 10;
            BeaconTimerSecond += 0.1f;
        });
        PlaySoundOnPlayer(player, "sounds/tools/sfm/beep.vsnd_c");
        return;
    }
    private void PlaySoundOnPlayer(IPlayer? player, string soundPath)
    {
        if(player == null || !player.IsValid)return;
        player.ExecuteCommand($"play {soundPath}");
    }

    public void TeleportLaser(CBeam? laser,Vector start, Vector end)
    {
        if(laser == null || !laser.IsValid)return;
        // set pos
        laser.Teleport(start, RotationZero, VectorZero);
        // end pos
        // NOTE: we cant just move the whole vec
        laser.EndPos.X = end.X;
        laser.EndPos.Y = end.Y;
        laser.EndPos.Z = end.Z;
        laser.EndPosUpdated();
    }
    private Vector angle_on_circle(float angle, float radius, Vector mid)
    {
        // {r * cos(x),r * sin(x)} + mid
        // NOTE: we offset Z so it doesn't clip into the ground
        return new Vector((float)(mid.X + (radius * Math.Cos(angle))),(float)(mid.Y + (radius * Math.Sin(angle))), mid.Z + 6.0f);
    }
    private static readonly Vector VectorZero = new Vector(0, 0, 0);
    private static readonly QAngle RotationZero = new QAngle(0, 0, 0);
    public (int, CBeam?) DrawLaserBetween(Vector startPos, Vector endPos, Color color, float life, float width)
    {
        var beam = Core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");

        if (beam == null)
        {
            return (-1, null);
        }

        beam.Render = color;
        beam.Width = width;

        beam.Teleport(startPos, RotationZero, VectorZero);
        beam.EndPos.X = endPos.X;
        beam.EndPos.Y = endPos.Y;
        beam.EndPos.Z = endPos.Z;
        beam.DispatchSpawn();

        Core.Scheduler.DelayBySeconds(life, () => {if(beam != null && beam.IsValid) beam.Despawn(); }); // destroy beam after specific time

        return ((int)beam.Index, beam);
    }
}