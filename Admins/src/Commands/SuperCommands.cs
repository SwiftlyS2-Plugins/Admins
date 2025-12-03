using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins.Commands;

public partial class AdminCommands
{
    [Command("hp", permission: "admins.commands.hp")]
    public void Command_HP(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 2)
        {
            SendSyntax(context, "hp", ["<player>", "<health>", "[armour]", "[helmet]"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        if (!int.TryParse(context.Args[1], out var health))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "health", "0", "100"]);
            return;
        }

        if (health < 0 || health > 100)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "health", "0", "100"]);
            return;
        }

        int armour = 0;
        if (context.Args.Length >= 3)
        {
            if (!int.TryParse(context.Args[2], out armour))
            {
                var localizer = GetPlayerLocalizer(context);
                context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[2], "armour", "0", "100"]);
                return;
            }

            if (armour < 0 || armour > 100)
            {
                var localizer = GetPlayerLocalizer(context);
                context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[2], "armour", "0", "100"]);
                return;
            }
        }

        bool helmet = false;
        if (context.Args.Length >= 4)
        {
            if (!bool.TryParse(context.Args[3], out helmet))
            {
                var localizer = GetPlayerLocalizer(context);
                context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[3], "helmet", "false", "true"]);
                return;
            }
        }

        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

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

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;

            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;

            return (localizer["command.hp_success", Admins.Config.CurrentValue.Prefix, adminName, playerName, health, armour, helmet], MessageType.Chat);
        });
    }

    [Command("giveitem", permission: "admins.commands.giveitem")]
    public void Command_GiveItem(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 2)
        {
            SendSyntax(context, "giveitem", ["<player>", "<item_name>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        string itemName = context.Args[1];
        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

            var itemServices = pawn.ItemServices;
            if (itemServices != null && itemServices.IsValid)
            {
                itemServices.GiveItem(itemName);
            }
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;

            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;

            return (localizer["command.giveitem_success", Admins.Config.CurrentValue.Prefix, adminName, itemName, playerName], MessageType.Chat);
        });
    }

    [Command("givemoney", permission: "admins.commands.givemoney")]
    public void Command_GiveMoney(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 2)
        {
            SendSyntax(context, "givemoney", ["<player>", "<amount>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        if (!int.TryParse(context.Args[1], out var amount))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "amount", "1", "16000"]);
            return;
        }

        if (amount < 1 || amount > 16000)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "amount", "1", "16000"]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var moneyServices = targetPlayer.Controller.InGameMoneyServices;
            if (moneyServices != null && moneyServices.IsValid)
            {
                moneyServices.Account += amount;
                moneyServices.AccountUpdated();
            }
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;

            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;

            return (localizer["command.givemoney_success", Admins.Config.CurrentValue.Prefix, adminName, amount, playerName], MessageType.Chat);
        });
    }

    [Command("setmoney", permission: "admins.commands.setmoney")]
    public void Command_SetMoney(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 2)
        {
            SendSyntax(context, "setmoney", ["<player>", "<amount>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        if (!int.TryParse(context.Args[1], out var amount))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "amount", "0", "16000"]);
            return;
        }

        if (amount < 0 || amount > 16000)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "amount", "0", "16000"]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var moneyServices = targetPlayer.Controller.InGameMoneyServices;
            if (moneyServices != null && moneyServices.IsValid)
            {
                moneyServices.Account = amount;
                moneyServices.AccountUpdated();
            }
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;

            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;

            return (localizer["command.setmoney_success", Admins.Config.CurrentValue.Prefix, adminName, amount, playerName], MessageType.Chat);
        });
    }

    [Command("melee", permission: "admins.commands.melee")]
    public void Command_Melee(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "melee", ["<player>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

            var itemServices = pawn.ItemServices;
            if (itemServices != null && itemServices.IsValid)
            {
                itemServices.RemoveItems();
                itemServices.GiveItem("weapon_knife");
            }
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;
            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;
            return (localizer["command.melee_success", Admins.Config.CurrentValue.Prefix, adminName, playerName], MessageType.Chat);
        });
    }

    [Command("disarm", permission: "admins.commands.disarm")]
    public void Command_Disarm(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "disarm", ["<player>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

            var itemServices = pawn.ItemServices;
            if (itemServices != null && itemServices.IsValid)
            {
                itemServices.RemoveItems();
            }
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;
            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;
            return (localizer["command.disarm_success", Admins.Config.CurrentValue.Prefix, adminName, playerName], MessageType.Chat);
        });
    }

    [Command("restartround", permission: "admins.commands.restartround")]
    [CommandAlias("rr")]
    public void Command_RestartRound(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "restartround", ["<delay>"]);
            return;
        }

        if (!float.TryParse(context.Args[0], out var delay))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[0], "delay", "0", "300"]);
            return;
        }

        if (delay < 0 || delay > 300)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[0], "delay", "0", "300"]);
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
            return (localizer["command.restartround", Admins.Config.CurrentValue.Prefix, adminName, delay], MessageType.Chat);
        });
    }

    [Command("freeze", permission: "admins.commands.freeze")]
    public void Command_Freeze(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "freeze", ["<player>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

            pawn.ActualMoveType = MoveType_t.MOVETYPE_INVALID;
            pawn.MoveType = MoveType_t.MOVETYPE_INVALID;
            pawn.MoveTypeUpdated();
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;
            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;
            return (localizer["command.freeze_success", Admins.Config.CurrentValue.Prefix, adminName, playerName], MessageType.Chat);
        });
    }

    [Command("unfreeze", permission: "admins.commands.unfreeze")]
    public void Command_Unfreeze(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "unfreeze", ["<player>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

            pawn.ActualMoveType = MoveType_t.MOVETYPE_WALK;
            pawn.MoveType = MoveType_t.MOVETYPE_WALK;
            pawn.MoveTypeUpdated();
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;
            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;
            return (localizer["command.unfreeze_success", Admins.Config.CurrentValue.Prefix, adminName, playerName], MessageType.Chat);
        });
    }

    [Command("noclip", permission: "admins.commands.noclip")]
    public void Command_Noclip(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        var pawn = context.Sender!.PlayerPawn;
        if (pawn == null)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.noclip_no_pawn", Admins.Config.CurrentValue.Prefix]);
            return;
        }

        if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.noclip_no_pawn", Admins.Config.CurrentValue.Prefix]);
            return;
        }

        if (pawn.MoveType == MoveType_t.MOVETYPE_NOCLIP)
        {
            pawn.ActualMoveType = MoveType_t.MOVETYPE_WALK;
            pawn.MoveType = MoveType_t.MOVETYPE_WALK;
            pawn.MoveTypeUpdated();

            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.noclip_disabled", Admins.Config.CurrentValue.Prefix]);
        }
        else
        {
            pawn.ActualMoveType = MoveType_t.MOVETYPE_NOCLIP;
            pawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
            pawn.MoveTypeUpdated();

            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.noclip_enabled", Admins.Config.CurrentValue.Prefix]);
        }
    }

    [Command("setspeed", permission: "admins.commands.setspeed")]
    public void Command_SetSpeed(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "setspeed", ["<speed_multiplier>"]);
            return;
        }

        if (!float.TryParse(context.Args[0], out var speedMultiplier))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[0], "speed_multiplier", "0.1", "10.0"]);
            return;
        }

        if (speedMultiplier < 0.1f || speedMultiplier > 10.0f)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[0], "speed_multiplier", "0.1", "10.0"]);
            return;
        }

        var pawn = context.Sender!.PlayerPawn;
        if (pawn == null)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.setspeed_no_pawn", Admins.Config.CurrentValue.Prefix]);
            return;
        }

        if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.setspeed_no_pawn", Admins.Config.CurrentValue.Prefix]);
            return;
        }

        pawn.VelocityModifier = speedMultiplier;
        pawn.VelocityModifierUpdated();

        var localizerSuccess = GetPlayerLocalizer(context);
        context.Reply(localizerSuccess["command.setspeed_success", Admins.Config.CurrentValue.Prefix, speedMultiplier]);
    }

    [Command("setgravity", permission: "admins.commands.setgravity")]
    public void Command_SetGravity(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "setgravity", ["<gravity_multiplier>"]);
            return;
        }

        if (!float.TryParse(context.Args[0], out var gravityMultiplier))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[0], "gravity_multiplier", "0.1", "10.0"]);
            return;
        }

        if (gravityMultiplier < 0.1f || gravityMultiplier > 10.0f)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[0], "gravity_multiplier", "0.1", "10.0"]);
            return;
        }

        var pawn = context.Sender!.PlayerPawn;
        if (pawn == null)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.setgravity_no_pawn", Admins.Config.CurrentValue.Prefix]);
            return;
        }

        if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.setgravity_no_pawn", Admins.Config.CurrentValue.Prefix]);
            return;
        }

        pawn.GravityScale = gravityMultiplier;
        pawn.GravityScaleUpdated();

        var localizerSuccess = GetPlayerLocalizer(context);
        context.Reply(localizerSuccess["command.setgravity_success", Admins.Config.CurrentValue.Prefix, gravityMultiplier]);
    }

    [Command("slay", permission: "admins.commands.slay")]
    public void Command_Slay(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "slay", ["<player>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

            pawn.CommitSuicide(false, false);
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;
            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;
            return (localizer["command.slay_success", Admins.Config.CurrentValue.Prefix, adminName, playerName], MessageType.Chat);
        });
    }

    [Command("slap", permission: "admins.commands.slap")]
    public void Command_Slap(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "slap", ["<player>", "[damage]"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        var damage = 0;
        if (context.Args.Length >= 2)
        {
            if (!int.TryParse(context.Args[1], out damage))
            {
                var localizer = GetPlayerLocalizer(context);
                context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "damage", "0", "100"]);
                return;
            }
        }

        if (damage < 0 || damage > 100)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.invalid_value_range", Admins.Config.CurrentValue.Prefix, context.Args[1], "damage", "0", "100"]);
            return;
        }

        foreach (var targetPlayer in players)
        {
            var pawn = targetPlayer.PlayerPawn;
            if (pawn == null) continue;
            if (pawn.IsValid == false || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

            pawn.Health = Math.Max(pawn.Health - damage, 0);
            pawn.HealthUpdated();

            if (pawn.Health == 0)
            {
                pawn.CommitSuicide(false, false);
            }

            pawn.Velocity.X.Value += (float)Random.Shared.NextInt64(50, 230) * (Random.Shared.NextDouble() < 0.5 ? -1 : 1);
            pawn.Velocity.Y.Value += (float)Random.Shared.NextInt64(50, 230) * (Random.Shared.NextDouble() < 0.5 ? -1 : 1);
            pawn.Velocity.Z.Value += Random.Shared.NextInt64(100, 300);
            pawn.VelocityUpdated();
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;
            var playerName = "Unknown";
            if (controller.IsValid) playerName = p.Controller.PlayerName;
            return (localizer["command.slap_success", Admins.Config.CurrentValue.Prefix, adminName, playerName], MessageType.Chat);
        });
    }

    [Command("rename", permission: "admins.commands.rename")]
    public void Command_Rename(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (context.Args.Length < 2)
        {
            SendSyntax(context, "rename", ["<player>", "<new_name>"]);
            return;
        }

        var players = Core.PlayerManager.FindTargettedPlayers(context.Sender!, context.Args[0], TargetSearchMode.IncludeSelf);
        if (players == null || !players.Any())
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.player_not_found", Admins.Config.CurrentValue.Prefix, context.Args[0]]);
            return;
        }

        Dictionary<IPlayer, string> oldNames = [];

        string newName = context.Args[1];
        foreach (var targetPlayer in players)
        {
            var controller = targetPlayer.Controller;
            if (controller == null) continue;
            if (controller.IsValid == false) continue;

            oldNames[targetPlayer] = controller.PlayerName;

            controller.PlayerName = newName;
            controller.PlayerNameUpdated();
        }

        var adminName = context.Sender!.Controller.PlayerName;

        SendMessageToPlayers(players, context.Sender!, (p, localizer) =>
        {
            var controller = p.Controller;

            var playerName = "Unknown";
            if (controller.IsValid) playerName = oldNames[p];

            return (localizer["command.rename_success", Admins.Config.CurrentValue.Prefix, adminName, playerName, newName], MessageType.Chat);
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

        if (context.Args.Length < 1)
        {
            SendSyntax(context, "csay", ["<message>"]);
            return;
        }

        string message = string.Join(" ", context.Args);

        var adminName = context.Sender!.Controller.PlayerName;
        Core.PlayerManager.SendCenter($"{adminName}: {message}");
    }

    [Command("rcon", permission: "admins.commands.rcon")]
    public void Command_Rcon(ICommandContext context)
    {
        if (context.Args.Length < 1)
        {
            SendSyntax(context, "rcon", ["<command>"]);
            return;
        }

        string rconCommand = string.Join(" ", context.Args);
        Core.Engine.ExecuteCommand(rconCommand);
    }

    [Command("map", permission: "admins.commands.map")]
    [CommandAlias("changelevel")]
    [CommandAlias("changemap")]
    public void Command_Map(ICommandContext context)
    {
        if (context.Args.Length < 1)
        {
            SendSyntax(context, "map", ["<map_name>"]);
            return;
        }

        string mapName = context.Args[0];
        if (!int.TryParse(mapName, out var _))
        {
            Core.Engine.ExecuteCommand($"changelevel {mapName}");
        }
        else
        {
            Core.Engine.ExecuteCommand($"host_workshop_map {mapName}");
        }
    }
}