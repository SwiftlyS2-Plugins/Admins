using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins.SuperCommands.Commands;

public partial class ServerCommands
{
    [Command("hp", permission: "admins.commands.hp")]
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
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

        foreach (var player in players)
        {
            ApplyHealthAndArmor(player, health, armour, helmet);
        }

        NotifyHealthChanged(players, context.Sender!, health, armour, helmet);
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        foreach (var player in players)
        {
            SetPlayerMoveType(player, MoveType_t.MOVETYPE_INVALID);
        }

        NotifyPlayersAction(players, context.Sender!, "command.freeze_success");
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        foreach (var player in players)
        {
            SetPlayerMoveType(player, MoveType_t.MOVETYPE_WALK);
        }

        NotifyPlayersAction(players, context.Sender!, "command.unfreeze_success");
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
        var localizer = GetPlayerLocalizer(context);

        if (!IsValidAlivePawn(pawn))
        {
            context.Reply(localizer["command.noclip_no_pawn", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        if (pawn!.MoveType == MoveType_t.MOVETYPE_NOCLIP)
        {
            SetPlayerMoveType(context.Sender!, MoveType_t.MOVETYPE_WALK);
            context.Reply(localizer["command.noclip_disabled", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
        }
        else
        {
            SetPlayerMoveType(context.Sender!, MoveType_t.MOVETYPE_NOCLIP);
            context.Reply(localizer["command.noclip_enabled", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
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

        if (!ValidateArgsCount(context, 1, "setspeed", ["<speed_multiplier>"]))
        {
            return;
        }

        if (!TryParseFloat(context, context.Args[0], "speed_multiplier", 0.1f, 10.0f, out var speedMultiplier))
        {
            return;
        }

        var pawn = context.Sender!.PlayerPawn;
        var localizer = GetPlayerLocalizer(context);

        if (!IsValidAlivePawn(pawn))
        {
            context.Reply(localizer["command.setspeed_no_pawn", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        pawn!.VelocityModifier = speedMultiplier;
        pawn.VelocityModifierUpdated();

        context.Reply(localizer["command.setspeed_success", ConfigurationManager.GetCurrentConfiguration()!.Prefix, speedMultiplier]);
    }

    [Command("setgravity", permission: "admins.commands.setgravity")]
    public void Command_SetGravity(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "setgravity", ["<gravity_multiplier>"]))
        {
            return;
        }

        if (!TryParseFloat(context, context.Args[0], "gravity_multiplier", 0.1f, 10.0f, out var gravityMultiplier))
        {
            return;
        }

        var pawn = context.Sender!.PlayerPawn;
        var localizer = GetPlayerLocalizer(context);

        if (!IsValidAlivePawn(pawn))
        {
            context.Reply(localizer["command.setgravity_no_pawn", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        pawn!.GravityScale = gravityMultiplier;
        pawn.GravityScaleUpdated();

        context.Reply(localizer["command.setgravity_success", ConfigurationManager.GetCurrentConfiguration()!.Prefix, gravityMultiplier]);
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        foreach (var player in players)
        {
            SlayPlayer(player);
        }

        NotifyPlayersAction(players, context.Sender!, "command.slay_success");
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        var damage = 0;
        if (context.Args.Length >= 2 && !TryParseInt(context, context.Args[1], "damage", 0, 100, out damage))
        {
            return;
        }

        foreach (var player in players)
        {
            ApplySlap(player, damage);
        }

        NotifyPlayersAction(players, context.Sender!, "command.slap_success");
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        var oldNames = new Dictionary<IPlayer, string>();
        var newName = context.Args[1];

        foreach (var player in players)
        {
            if (player.Controller != null && player.Controller.IsValid)
            {
                oldNames[player] = player.Controller.PlayerName;
                player.Controller.PlayerName = newName;
                player.Controller.PlayerNameUpdated();
            }
        }

        NotifyRename(players, context.Sender!, oldNames, newName);
    }

    [Command("givemoney", permission: "admins.commands.givemoney")]
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        if (!TryParseInt(context, context.Args[1], "amount", 1, 16000, out var amount))
        {
            return;
        }

        foreach (var player in players)
        {
            ModifyPlayerMoney(player, amount, isAdditive: true);
        }

        NotifyMoneyChanged(players, context.Sender!, amount, "command.givemoney_success");
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null)
        {
            return;
        }

        if (!TryParseInt(context, context.Args[1], "amount", 0, 16000, out var amount))
        {
            return;
        }

        foreach (var player in players)
        {
            ModifyPlayerMoney(player, amount, isAdditive: false);
        }

        NotifyMoneyChanged(players, context.Sender!, amount, "command.setmoney_success");
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

    private void NotifyHealthChanged(List<IPlayer> players, IPlayer sender, int health, int armour, bool helmet)
    {
        var adminName = sender.Controller.PlayerName;

        SendMessageToPlayers(players, sender, (p, localizer) =>
        {
            var playerName = GetPlayerName(p);
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

    private void NotifyMoneyChanged(List<IPlayer> players, IPlayer sender, int amount, string messageKey)
    {
        var adminName = sender.Controller.PlayerName;

        SendMessageToPlayers(players, sender, (p, localizer) =>
        {
            var playerName = GetPlayerName(p);
            return (localizer[
                messageKey,
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                adminName,
                amount,
                playerName
            ], MessageType.Chat);
        });
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

    private void NotifyPlayersAction(List<IPlayer> players, IPlayer sender, string messageKey)
    {
        var adminName = sender.Controller.PlayerName;

        SendMessageToPlayers(players, sender, (p, localizer) =>
        {
            var playerName = GetPlayerName(p);
            return (localizer[
                messageKey,
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                adminName,
                playerName
            ], MessageType.Chat);
        });
    }

    private void NotifyRename(List<IPlayer> players, IPlayer sender, Dictionary<IPlayer, string> oldNames, string newName)
    {
        var adminName = sender.Controller.PlayerName;

        SendMessageToPlayers(players, sender, (p, localizer) =>
        {
            var oldName = oldNames.TryGetValue(p, out string? value) ? value : "Unknown";
            return (localizer[
                "command.rename_success",
                ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                adminName,
                oldName,
                newName
            ], MessageType.Chat);
        });
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

        foreach (var player in players)
        {
            ToggleGodMode(player);
        }

        NotifyPlayersAction(players, context.Sender!, "command.god_success");
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

        foreach (var player in players)
        {
            RespawnPlayer(player);
        }

        NotifyPlayersAction(players, context.Sender!, "command.respawn_success");
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

        foreach (var player in players)
        {
            SwapPlayerTeam(player);
        }

        NotifyPlayersAction(players, context.Sender!, "command.swap_success");
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

        foreach (var player in players)
        {
            player.ChangeTeam(targetTeam.Value);
        }

        NotifyTeamChange(players, context.Sender!, targetTeam.Value);
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null || players.Count == 0)
        {
            return;
        }

        var targetPlayer = players.FirstOrDefault(p => p.Slot != context.Sender!.Slot);
        if (targetPlayer == null)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.goto_no_valid_targets", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var targetPawn = targetPlayer.PlayerPawn;
        if (!IsValidAlivePawn(targetPawn))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.goto_target_dead", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var adminPawn = context.Sender!.PlayerPawn;
        if (!IsValidAlivePawn(adminPawn))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.goto_self_dead", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
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
            NotifyPlayersAction(new List<IPlayer> { targetPlayer }, context.Sender!, "command.goto_success");
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

        var players = FindTargetPlayers(context, context.Args[0]);
        if (players == null || players.Count == 0)
        {
            return;
        }

        var adminPawn = context.Sender!.PlayerPawn;
        if (!IsValidAlivePawn(adminPawn))
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.bring_self_dead", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var validPlayers = players.Where(p => 
            p.Slot != context.Sender.Slot && 
            IsValidAlivePawn(p.PlayerPawn)
        ).ToList();

        if (validPlayers.Count == 0)
        {
            var localizer = GetPlayerLocalizer(context);
            context.Reply(localizer["command.bring_no_valid_targets", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
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

            foreach (var player in validPlayers)
            {
                player.PlayerPawn!.Teleport(safeOrigin, rotation, new Vector(0, 0, 0));
            }
            
            NotifyPlayersAction(validPlayers, context.Sender!, "command.bring_success");
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

    private void NotifyTeamChange(List<IPlayer> players, IPlayer sender, Team team)
    {
        var adminName = sender.Controller.PlayerName;
        var teamName = team switch
        {
            Team.CT => "CT",
            Team.T => "T",
            Team.Spectator => "Spectator",
            Team.None => "None",
            _ => "Unknown"
        };

        SendMessageToPlayers(players, sender, (p, localizer) =>
        {
            var playerName = GetPlayerName(p);
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