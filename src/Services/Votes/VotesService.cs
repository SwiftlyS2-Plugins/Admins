using Admins.Services.Access;
using Admins.Services.ServerControl;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Services.Votes;

public sealed class VotesService : IDisposable
{
    private const int VoteSeconds = 30;

    private readonly ISwiftlyCore _core;
    private readonly ILogger<VotesService> _logger;
    private readonly AccessService _access;
    private readonly Messenger _messenger;
    private readonly TargetResolver _targets;
    private readonly ServerControlService _server;

    // Game thread only.
    private Vote? _vote;

    public VotesService(ISwiftlyCore core, ILogger<VotesService> logger, AccessService access, Messenger messenger, TargetResolver targets, ServerControlService server)
    {
        _core = core;
        _logger = logger;
        _access = access;
        _messenger = messenger;
        _targets = targets;
        _server = server;

        core.Registrator.Register(this);
    }

    private sealed class Vote
    {
        public required IMenu Menu { get; init; }
        public required CancellationTokenSource Timer { get; init; }
        public required int OptionCount { get; init; }
        public required Func<IPlayer?, int, string> OptionText { get; init; }
        public required HashSet<int> Electorate { get; init; }
        public required Action<int?> OnFinished { get; init; }
        public Dictionary<int, int> Ballots { get; } = [];
        public int SecondsLeft { get; set; } = VoteSeconds;
    }

    [Command("vote", permission: "admins.commands.vote")]
    public void VoteCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (args.Length < 3)
        {
            _messenger.Syntax(issuer, "vote", "<question> <option1> <option2> [option3...]");
            return;
        }

        var question = args[0];
        var options = args.Skip(1).ToArray();

        Start(issuer, _ => question, options.Length, (_, index) => options[index], _ => { });
    }

    [Command("votekick", permission: "admins.commands.votekick")]
    public void VoteKickCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (args.Length < 1)
        {
            _messenger.Syntax(issuer, "votekick", "<player>");
            return;
        }

        var players = _targets.Find(issuer.Player, args[0], out var error, excludeCaller: true);
        if (_messenger.ReportTargetError(issuer, error, args[0])) return;

        if (players.Count > 1)
        {
            _messenger.Reply(issuer, "command.multiple_targets", args[0]);
            return;
        }

        var target = players[0];
        if (!_access.CanTarget(issuer.Player, target.SteamID))
        {
            _messenger.Reply(issuer, "command.target_has_immunity", Names.Of(target), _access.GetImmunity(target.SteamID));
            return;
        }

        var reference = PlayerRef.Of(target);
        Start(
            issuer,
            viewer => _messenger.Text(viewer, "vote.kick_title", reference.Name),
            2,
            (viewer, index) => _messenger.Text(viewer, index == 0 ? "yes" : "no"),
            winner =>
            {
                // Yes has to beat No outright; a tie keeps the player.
                if (winner != 0 || reference.Resolve(_core) is not { } player) return;

                player.Kick(_messenger.Text(player, "vote.kicked"), ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                _messenger.Broadcast("vote.kick_passed", reference.Name);
            });
    }

    [Command("votemap", permission: "admins.commands.votemap")]
    public void VoteMapCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        var maps = context.Args;

        if (maps.Length < 2)
        {
            _messenger.Syntax(issuer, "votemap", "<map1> <map2> [map3...]");
            return;
        }

        if (maps.FirstOrDefault(m => !m.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) && !_core.Engine.IsMapValid(m)) is { } invalid)
        {
            _messenger.Reply(issuer, "command.map_not_found", invalid);
            return;
        }

        Start(
            issuer,
            viewer => _messenger.Text(viewer, "vote.map_title"),
            maps.Length,
            (_, index) => maps[index],
            winner =>
            {
                if (winner is { } index) _server.ChangeMap(issuer, maps[index]);
            });
    }

    /// <param name="onFinished">Receives the winning option, or null when nobody voted or the top options tie.</param>
    private void Start(Issuer issuer, Func<IPlayer?, string> title, int optionCount, Func<IPlayer?, int, string> optionText, Action<int?> onFinished)
    {
        if (_vote is not null)
        {
            _messenger.Reply(issuer, "vote.active");
            return;
        }

        var voters = _messenger.Humans().ToList();
        if (voters.Count == 0)
        {
            _messenger.Reply(issuer, "command.no_valid_targets");
            return;
        }

        var builder = _core.Menu.CreateMenu("admins.vote")
            .WithTitle(_messenger.Text(null, "vote.menu_title"))
            .AddHeader(new TextComponent { TextProvider = s => _messenger.Text(s.Player, "vote.header", title(s.Player), _vote?.SecondsLeft ?? 0) });

        for (var i = 0; i < optionCount; i++)
        {
            var index = i;
            builder.Add(new ButtonComponent(string.Empty, ctx =>
            {
                Cast(ctx.Session, index);
                return ValueTask.CompletedTask;
            })
            {
                TextProvider = s => optionText(s.Player, index),
            });
        }

        var menu = builder.Build();
        var vote = new Vote
        {
            Menu = menu,
            Timer = _core.Scheduler.DelayAndRepeatBySeconds(1f, 1f, Tick),
            OptionCount = optionCount,
            OptionText = optionText,
            Electorate = [.. voters.Select(p => p.Slot)],
            OnFinished = onFinished,
        };

        _core.Scheduler.StopOnMapChange(vote.Timer);
        _vote = vote;

        foreach (var voter in voters)
        {
            var options = string.Join(", ", Enumerable.Range(0, optionCount).Select(i => $"{i + 1}. {optionText(voter, i)}"));
            _messenger.Chat(voter, "vote.started", issuer.Name, title(voter), options);
            menu.Open(voter);
        }
    }

    private void Cast(IMenuSession session, int option)
    {
        var slot = session.Player.Slot;
        if (_vote is not { } vote || !vote.Electorate.Contains(slot) || vote.Ballots.ContainsKey(slot)) return;

        vote.Ballots[slot] = option;
        session.Close();

        // Deferred: closing every session from inside this menu's own action handler is not safe.
        if (vote.Electorate.All(vote.Ballots.ContainsKey)) _core.Scheduler.NextTick(() => Finish(vote));
    }

    private void Tick()
    {
        if (_vote is not { } vote) return;

        vote.SecondsLeft--;
        if (vote.SecondsLeft <= 0)
        {
            Finish(vote);
            return;
        }

        foreach (var session in vote.Menu.Sessions) session.Invalidate();
    }

    private void Finish(Vote vote)
    {
        if (!ReferenceEquals(_vote, vote)) return;

        _vote = null;
        vote.Timer.Cancel();
        vote.Menu.CloseAll();

        var tally = vote.Ballots.Values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
        var total = vote.Ballots.Count;

        foreach (var player in _messenger.Humans())
        {
            _messenger.Chat(player, "vote.ended", total);
            if (total > 0)
            {
                var results = string.Join(", ", Enumerable.Range(0, vote.OptionCount).Select(i => $"{vote.OptionText(player, i)}: {tally.GetValueOrDefault(i)}"));
                _messenger.Chat(player, "vote.results", results);
            }
        }

        int? winner = null;
        if (total > 0)
        {
            var best = tally.Values.Max();
            var leaders = tally.Where(t => t.Value == best).Select(t => t.Key).ToList();
            if (leaders.Count == 1) winner = leaders[0];
        }

        try
        {
            vote.OnFinished(winner);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Applying a vote result failed.");
        }
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        if (_vote is not { } vote || !vote.Electorate.Remove(e.PlayerId)) return;

        if (vote.Electorate.All(vote.Ballots.ContainsKey)) Finish(vote);
    }

    public void Dispose()
    {
        if (_vote is not { } vote) return;

        _vote = null;
        vote.Timer.Cancel();
        vote.Menu.CloseAll();
    }
}
