using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Core.Menus.OptionsBase;

namespace Admins.SuperCommands.Commands;

public partial class ServerCommands
{
    private const float voteDurationSeconds = 30f; // Duration for how long the vote should last (in seconds)
    private int SentVoteToPlayersCount = 0; // Counter to track how many players the vote has been sent to
    private Dictionary<string, int> activeVotes = new Dictionary<string, int>();
    private IMenuAPI? voteMenu = null;
    private CancellationTokenSource? voteCancellationTokenSource = null;
    
    [Command("vote", permission: "admins.commands.vote")]
    public void Command_Vote(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 3, "vote", ["<question>", "<option1>", "<option2>", "[option3]", "[option4]", "[...]"]))
        {
            return;
        }

        // return if votes are currently active to prevent spam and overlapping votes
        if (voteCancellationTokenSource != null)
        {
            context.Reply(GetPlayerLocalizer(context)["command.vote_active", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var question = context.Args[0];
        var options = context.Args.Skip(1).ToArray();

        var adminName = context.Sender!.Controller.PlayerName;
        SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
        {
            var optionsText = string.Join(", ", options.Select((opt, index) => $"[green]{index + 1}. {opt}[default]"));
            return (localizer["command.vote", ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, question, optionsText], MessageType.Chat);
        });

        SentVoteToPlayersCount = 0; // Reset counter before sending vote menus
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (player.IsFakeClient) continue; // Skip fake clients
            SendVoteMenu(player, question, options);
            SentVoteToPlayersCount++;
        }

        // Start a delayed task to end the vote after the specified duration
        var startTime = Core.Engine.GlobalVars.CurrentTime;
        voteCancellationTokenSource = Core.Scheduler.RepeatBySeconds(1f, () =>
        {
            var TimeOver = startTime + voteDurationSeconds < Core.Engine.GlobalVars.CurrentTime;
            var VoteEnd = false;
            // If totalVotes equals SentVoteToPlayersCount, it means all players done voting
            var totalVotes = activeVotes.Values.Sum(); // Get total votes cast so far
            if (totalVotes == SentVoteToPlayersCount || TimeOver)
            {
                // Announce results only if there are votes, otherwise just end the vote silently (e.g. if no one votes and time runs out, we don't need to announce results)
                SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                {
                    return (localizer["command.vote_received", ConfigurationManager.GetCurrentConfiguration()!.Prefix, totalVotes], MessageType.Chat);
                });
                if(totalVotes > 0)
                {
                    var resultsText = string.Join(", ", activeVotes.Select(kvp => $"[green]{kvp.Key}: '{kvp.Value} votes[default]"));
                    SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                    {
                        return (localizer["command.vote_results", ConfigurationManager.GetCurrentConfiguration()!.Prefix, resultsText], MessageType.Chat);
                    });
                }
                activeVotes.Clear(); // Clear active votes for next vote
                VoteEnd = true;
                voteCancellationTokenSource?.Cancel(); // Cancel the repeating task to stop it from running further
                voteCancellationTokenSource = null; // Reset cancellation token source for next vote
            }

            if (voteMenu == null) return;
            foreach (var player in Core.PlayerManager.GetAllValidPlayers())
            {
                if (Core.MenusAPI.GetCurrentMenu(player) == voteMenu) // Check if the player still has the vote menu open
                {
                    voteMenu.Configuration.Title = $"{question} ({(int)(startTime + voteDurationSeconds - Core.Engine.GlobalVars.CurrentTime)}s)"; // Update menu title with remaining time
                    if (VoteEnd) Core.MenusAPI.CloseMenuForPlayer(player, voteMenu); // Close menu for all players when vote ends
                }
            }
            if(VoteEnd) voteMenu = null;
        });
    }

    [Command("votekick", permission: "admins.commands.votekick")]
    public void Command_VoteKick(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 1, "votekick", ["<player>"]))
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

        // return if votes are currently active to prevent spam and overlapping votes
        if (voteCancellationTokenSource != null)
        {
            context.Reply(GetPlayerLocalizer(context)["command.vote_active", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var question = $"{Core.Localizer["command.vote_kick_menu_title", (players.Count == 1 ? GetPlayerName(players[0]) : context.Args[0])]}";
        var options = new[] { "Yes", "No" };

        var adminName = context.Sender!.Controller.PlayerName;
        SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
        {
            var optionsText = string.Join(", ", options.Select((opt, index) => $"[green]{index + 1}. {opt}[default]"));
            return (localizer["command.vote", ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, question, optionsText], MessageType.Chat);
        });

        SentVoteToPlayersCount = 0; // Reset counter before sending vote menus
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (player.IsFakeClient) continue; // Skip fake clients
            SendVoteMenu(player, question, options);
            SentVoteToPlayersCount++;
        }

        // Start a delayed task to end the vote after the specified duration
        var startTime = Core.Engine.GlobalVars.CurrentTime;
        voteCancellationTokenSource = Core.Scheduler.RepeatBySeconds(1f, () =>
        {
            var TimeOver = startTime + voteDurationSeconds < Core.Engine.GlobalVars.CurrentTime;
            var VoteEnd = false;
            // If totalVotes equals SentVoteToPlayersCount, it means all players done voting
            var totalVotes = activeVotes.Values.Sum(); // Get total votes cast so far
            if (totalVotes == SentVoteToPlayersCount || TimeOver)
            {
                // Announce results only if there are votes, otherwise just end the vote silently (e.g. if no one votes and time runs out, we don't need to announce results)
                SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                {
                    return (localizer["command.vote_received", ConfigurationManager.GetCurrentConfiguration()!.Prefix, totalVotes], MessageType.Chat);
                });
                if(totalVotes > 0)
                {
                    var resultsText = string.Join(", ", activeVotes.Select(kvp => $"[green]{kvp.Key}: '{kvp.Value}' [default]votes"));
                    SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                    {
                        return (localizer["command.vote_results", ConfigurationManager.GetCurrentConfiguration()!.Prefix, resultsText], MessageType.Chat);
                    });
                    // If "Yes" votes are more than "No" votes, kick the player(s)
                    if (activeVotes.ContainsKey("Yes") && activeVotes["Yes"] > activeVotes.GetValueOrDefault("No", 0))
                    {
                        foreach (var player in players)
                        {
                            player.Kick($"Kicked by Votes", SwiftlyS2.Shared.ProtobufDefinitions.ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                        }
                    }
                }
                activeVotes.Clear(); // Clear active votes for next vote
                VoteEnd = true;
                voteCancellationTokenSource?.Cancel(); // Cancel the repeating task to stop it from running further
                voteCancellationTokenSource = null; // Reset cancellation token source for next vote
            }

            if (voteMenu == null) return;
            foreach (var player in Core.PlayerManager.GetAllValidPlayers())
            {
                if (Core.MenusAPI.GetCurrentMenu(player) == voteMenu) // Check if the player still has the vote menu open
                {
                    voteMenu.Configuration.Title = $"{question} ({(int)(startTime + voteDurationSeconds - Core.Engine.GlobalVars.CurrentTime)}s)"; // Update menu title with remaining time
                    if (VoteEnd) Core.MenusAPI.CloseMenuForPlayer(player, voteMenu); // Close menu for all players when vote ends
                }
            }
            if(VoteEnd) voteMenu = null;
        });
    }

    [Command("votemap", permission: "admins.commands.votemap")]
    public void Command_VoteMap(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            SendByPlayerOnly(context);
            return;
        }

        if (!ValidateArgsCount(context, 2, "votemap", ["<map1>", "<map2>", "<...>"]))
        {
            return;
        }

        // return if votes are currently active to prevent spam and overlapping votes
        if (voteCancellationTokenSource != null)
        {
            context.Reply(GetPlayerLocalizer(context)["command.vote_active", ConfigurationManager.GetCurrentConfiguration()!.Prefix]);
            return;
        }

        var question = $"{Core.Localizer["command.vote_map_menu_title"]}";
        var options = context.Args.ToArray();

        foreach (var mapName in options)
        {
            if (!Core.Engine.IsMapValid(mapName))
            {
                context.Reply(GetPlayerLocalizer(context)["command.map_not_found", ConfigurationManager.GetCurrentConfiguration()!.Prefix, mapName]);
                return;
            }
        }

        var adminName = context.Sender!.Controller.PlayerName;
        SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
        {
            var optionsText = string.Join(", ", options.Select((opt, index) => $"[green]{index + 1}. {opt}[default]"));
            return (localizer["command.vote", ConfigurationManager.GetCurrentConfiguration()!.Prefix, adminName, question, optionsText], MessageType.Chat);
        });

        SentVoteToPlayersCount = 0; // Reset counter before sending vote menus
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (player.IsFakeClient) continue; // Skip fake clients
            SendVoteMenu(player, question, options);
            SentVoteToPlayersCount++;
        }

        // Start a delayed task to end the vote after the specified duration
        var startTime = Core.Engine.GlobalVars.CurrentTime;
        voteCancellationTokenSource = Core.Scheduler.RepeatBySeconds(1f, () =>
        {
            var TimeOver = startTime + voteDurationSeconds < Core.Engine.GlobalVars.CurrentTime;
            var VoteEnd = false;
            // If totalVotes equals SentVoteToPlayersCount, it means all players done voting
            var totalVotes = activeVotes.Values.Sum(); // Get total votes cast so far
            if (totalVotes == SentVoteToPlayersCount || TimeOver)
            {
                // Announce results only if there are votes, otherwise just end the vote silently (e.g. if no one votes and time runs out, we don't need to announce results)
                SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                {
                    return (localizer["command.vote_received", ConfigurationManager.GetCurrentConfiguration()!.Prefix, totalVotes], MessageType.Chat);
                });
                if(totalVotes > 0)
                {
                    var resultsText = string.Join(", ", activeVotes.Select(kvp => $"[green]{kvp.Key}: '{kvp.Value}' [default]votes"));
                    SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                    {
                        return (localizer["command.vote_results", ConfigurationManager.GetCurrentConfiguration()!.Prefix, resultsText], MessageType.Chat);
                    });
                    // If a map option has more votes than others, change to that map
                    var winningOption = activeVotes.OrderByDescending(kvp => kvp.Value).First().Key;
                    SendMessageToPlayers(Core.PlayerManager.GetAllValidPlayers(), (p, localizer) =>
                    {
                        return (localizer["command.vote_changing_map", ConfigurationManager.GetCurrentConfiguration()!.Prefix, winningOption], MessageType.Chat);
                    });
                    if (activeVotes.ContainsKey(winningOption))
                    {
                        Core.Scheduler.DelayBySeconds(3.0f, () => Core.Engine.ExecuteCommand($"changelevel {winningOption}"));
                    }
                }
                activeVotes.Clear(); // Clear active votes for next vote
                VoteEnd = true;
                voteCancellationTokenSource?.Cancel(); // Cancel the repeating task to stop it from running further
                voteCancellationTokenSource = null; // Reset cancellation token source for next vote
            }

            if (voteMenu == null) return;
            foreach (var player in Core.PlayerManager.GetAllValidPlayers())
            {
                if (Core.MenusAPI.GetCurrentMenu(player) == voteMenu) // Check if the player still has the vote menu open
                {
                    voteMenu.Configuration.Title = $"{question} ({(int)(startTime + voteDurationSeconds - Core.Engine.GlobalVars.CurrentTime)}s)"; // Update menu title with remaining time
                    if (VoteEnd) Core.MenusAPI.CloseMenuForPlayer(player, voteMenu); // Close menu for all players when vote ends
                }
            }
            if(VoteEnd) voteMenu = null;
        });
    }

    private void SendVoteMenu(IPlayer player, string question, string[] options)
    {
        if (player == null || !player.IsValid || player.IsFakeClient) return;

        // Create menu
        voteMenu = Core.MenusAPI.CreateBuilder().EnableExit().EnableSound().SetPlayerFrozen(false).DisableExit()
        .Design.SetMenuTitle($"{question}")
        .Design.SetGlobalScrollStyle(MenuOptionScrollStyle.CenterFixed)
        .Build();
        
        foreach (var option in options)
        {
            var MenuOption = new ButtonMenuOption($"{option}");
            MenuOption.Click += (sender, args) =>
            {
                activeVotes[option] = activeVotes.ContainsKey(option) ? activeVotes[option] + 1 : 1; // Increment vote count for the selected option
                Core.MenusAPI.CloseMenuForPlayer(args.Player, voteMenu);
                return ValueTask.CompletedTask;
            };
            // Add options to menu
            voteMenu.AddOption(MenuOption);
        }

        Core.MenusAPI.OpenMenuForPlayer(player, voteMenu);
    }
}