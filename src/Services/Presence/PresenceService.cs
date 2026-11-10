using Admins.Models;
using Admins.Services.Access;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Services.Presence;

/// <summary>Join and leave messages, and !hide, which fakes a leave and parks the admin on no team. Game thread only.</summary>
public sealed class PresenceService : IDisposable
{
    private const string TeamSelectConVar = "sv_disable_teamselect_menu";

    private readonly ISwiftlyCore _core;
    private readonly ILogger<PresenceService> _logger;
    private readonly IOptionsMonitor<Configuration> _config;
    private readonly Messenger _messenger;
    private readonly AccessService _access;

    // A leave is only announced for a join that was; put-in-server also repeats on every map change.
    private readonly Dictionary<int, ulong> _present = [];
    private readonly Dictionary<int, Hidden> _hidden = [];

    public PresenceService(ISwiftlyCore core, ILogger<PresenceService> logger, IOptionsMonitor<Configuration> config, Messenger messenger, AccessService access)
    {
        _core = core;
        _logger = logger;
        _config = config;
        _messenger = messenger;
        _access = access;

        core.Registrator.Register(this);

        // Covers a hot reload: these players joined before the plugin and must still get a leave message.
        core.Scheduler.NextTick(() =>
        {
            foreach (var player in _messenger.Humans())
            {
                if (player.Controller is { IsValid: true }) _present[player.Slot] = player.SessionId;
            }
        });
    }

    private sealed class Hidden(ulong sessionId, string name)
    {
        public ulong SessionId { get; } = sessionId;

        public string Name { get; } = name;

        // !hide's own hop through Spectator must not count as picking a team.
        public bool Settling { get; set; } = true;
    }

    /// <summary>Raised when a player is hidden or revealed.</summary>
    public event Action? Changed;

    private bool ConnectMessages => _config.CurrentValue.Presence.ConnectMessages;

    public bool IsHidden(IPlayer player) => _hidden.TryGetValue(player.Slot, out var hidden) && hidden.SessionId == player.SessionId;

    /// <summary>
    /// Whether <paramref name="viewer"/> is shown <paramref name="hidden"/>, marked as hidden: only other admins are.
    /// The hidden admin is left out of their own lists too, so they see what players see.
    /// </summary>
    public bool CanSeeHidden(IPlayer viewer, IPlayer hidden) =>
        viewer.SessionId != hidden.SessionId
        && (_access.GetAdmin(viewer) is not null || _core.Permission.PlayerHasPermission(viewer.SteamID, "admins.commands.hide"));

    #region Commands

    [Command("hide", permission: "admins.commands.hide")]
    public void HideCommand(ICommandContext context) => Hide(Issuer.From(context));

    // Same permission on purpose: an admin able to hide but not unhide would be stuck with no team picker.
    [Command("unhide", permission: "admins.commands.hide")]
    public void UnhideCommand(ICommandContext context) => Unhide(Issuer.From(context));

    public bool Hide(Issuer issuer)
    {
        if (issuer.Player is not { } player)
        {
            _messenger.Reply(issuer, "command.player_only");
            return false;
        }

        if (IsHidden(player))
        {
            _messenger.Reply(issuer, "hide.already_hidden");
            return false;
        }

        if (player.Controller is not { IsValid: true }) return false;

        _hidden[player.Slot] = new Hidden(player.SessionId, Names.Of(player));

        // Before the team change: the client opens the team picker as soon as it sees no team.
        SetTeamSelect(player, disabled: true);
        player.ChangeTeam(Team.Spectator);
        player.ChangeTeam(Team.None);
        Changed?.Invoke();

        var slot = player.Slot;
        var sessionId = player.SessionId;
        _core.Scheduler.NextTick(() => Settle(slot, sessionId));
        return true;
    }

    public bool Unhide(Issuer issuer)
    {
        if (issuer.Player is not { } player)
        {
            _messenger.Reply(issuer, "command.player_only");
            return false;
        }

        if (!_hidden.TryGetValue(player.Slot, out var hidden) || hidden.SessionId != player.SessionId)
        {
            _messenger.Reply(issuer, "hide.not_hidden");
            return false;
        }

        Reveal(player, hidden);
        player.ChangeTeam(Team.Spectator);
        _messenger.Reply(issuer, "hide.disabled");
        return true;
    }

    private void Settle(int slot, ulong sessionId)
    {
        if (!_hidden.TryGetValue(slot, out var hidden) || hidden.SessionId != sessionId) return;
        if (_core.PlayerManager.GetPlayer(slot) is not { } player) return;

        if (player.Controller is not { IsValid: true } controller || controller.TeamNum != (byte)Team.None)
        {
            _hidden.Remove(slot);
            SetTeamSelect(player, disabled: false);
            Changed?.Invoke();
            _logger.LogWarning("Hiding {Name} failed: the player did not end up without a team.", hidden.Name);
            _messenger.Chat(player, "hide.failed");
            return;
        }

        hidden.Settling = false;
        if (ConnectMessages) Announce("presence.disconnected", hidden.Name, "presence.reason.disconnect");
        _messenger.Chat(player, "hide.enabled");
    }

    private void Reveal(IPlayer player, Hidden hidden)
    {
        _hidden.Remove(player.Slot);
        SetTeamSelect(player, disabled: false);
        if (ConnectMessages) Announce("presence.connected", hidden.Name);
        Changed?.Invoke();
    }

    #endregion

    #region Join, leave and team events

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerConnect(EventPlayerConnect e)
    {
        if (!e.Bot && ConnectMessages) e.DontBroadcast = true;
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull e)
    {
        if (ConnectMessages) e.DontBroadcast = true;
        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnClientPutInServer>]
    public void OnClientPutInServer(IOnClientPutInServerEvent e)
    {
        if (e.Kind != ClientKind.Player) return;

        var slot = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            if (_core.PlayerManager.GetPlayer(slot) is not { IsFakeClient: false } player) return;

            // A map change re-sends the server's convars, which brings the team menu back.
            if (IsHidden(player)) SetTeamSelect(player, disabled: true);

            if (_present.TryGetValue(slot, out var sessionId) && sessionId == player.SessionId) return;

            _present[slot] = player.SessionId;
            if (ConnectMessages) Announce("presence.connected", Names.Of(player));
        });
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect e)
    {
        if (e.UserIdPlayer is not { } player) return HookResult.Continue;

        var slot = player.Slot;
        var wasHidden = _hidden.Remove(slot, out var hidden) && hidden.SessionId == player.SessionId;
        var wasPresent = _present.Remove(slot, out var sessionId) && sessionId == player.SessionId;

        // A hidden admin already "left"; the game must not say so a second time even when it owns the messages.
        if (wasHidden || (ConnectMessages && e.XuID != 0)) e.DontBroadcast = true;

        if (ConnectMessages && wasPresent && !wasHidden)
        {
            // The leaver's own name read is unsafe from here on; the event carries it.
            var name = e.Name;
            var reason = ReasonKey((ENetworkDisconnectionReason)e.Reason);
            _core.Scheduler.NextTick(() => Announce("presence.disconnected", name, reason));
        }

        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        _present.Remove(e.PlayerId);
        _hidden.Remove(e.PlayerId);
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerTeam(EventPlayerTeam e)
    {
        if (e.UserIdPlayer is not { } player || !_hidden.TryGetValue(player.Slot, out var hidden) || hidden.SessionId != player.SessionId)
        {
            return HookResult.Continue;
        }

        if (hidden.Settling || e.Disconnect || e.Team == (byte)Team.None)
        {
            e.DontBroadcast = true;
            if (e.Team == (byte)Team.None) hidden.Settling = false;
            return HookResult.Continue;
        }

        // Taking any team ends the hide. From Pre, so the rejoin is announced before the team message.
        Reveal(player, hidden);
        return HookResult.Continue;
    }

    #endregion

    private void Announce(string key, string name, string? reasonKey = null)
    {
        foreach (var player in _messenger.Humans())
        {
            player.SendChat(reasonKey is null
                ? _messenger.Text(player, key, name)
                : _messenger.Text(player, key, name, _messenger.Text(player, reasonKey)));
        }
    }

    private void SetTeamSelect(IPlayer player, bool disabled)
    {
        // A bool convar replicates as "1"/"0"; the client does not parse "true".
        var value = disabled || _core.ConVar.FindAsString(TeamSelectConVar)?.ValueAsString is "1" or "true" ? "1" : "0";

        try
        {
            _core.ConVar.ReplicateToClient(player.PlayerID, TeamSelectConVar, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to replicate {ConVar} to slot {Slot}.", TeamSelectConVar, player.Slot);
        }
    }

    private static string ReasonKey(ENetworkDisconnectionReason reason) => reason switch
    {
        ENetworkDisconnectionReason.NETWORK_DISCONNECT_BANADDED
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_VACBANSTATE => "presence.reason.banned",
        ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED
            or (>= ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED_TEAMKILLING and <= ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED_INSECURECLIENT) => "presence.reason.kicked",
        ENetworkDisconnectionReason.NETWORK_DISCONNECT_TIMEDOUT
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_LOST
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_REMOTE_TIMEOUT
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_REMOTE_TIMEOUT_CONNECTING
            or ENetworkDisconnectionReason.NETWORK_DISCONNECT_CONNECT_REQUEST_TIMEDOUT => "presence.reason.timeout",
        _ => "presence.reason.disconnect",
    };

    public void Dispose()
    {
        // Players left without a team still need the picker back, or they cannot rejoin.
        foreach (var (slot, hidden) in _hidden)
        {
            if (_core.PlayerManager.GetPlayer(slot) is { } player && player.SessionId == hidden.SessionId) SetTeamSelect(player, disabled: false);
        }

        _hidden.Clear();
    }
}
