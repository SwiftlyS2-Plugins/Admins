using Admins.Models;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins.Services.Esp;

/// <summary>Through-wall outlines: a pair of bone-merged props per player, sent only to allowed viewers. Game thread only.</summary>
public sealed class EspService : IDisposable
{
    private const string Permission = "admins.commands.esp";
    // Outside admins.commands.* on purpose: outlines while alive are a wallhack in play.
    private const string AlivePermission = "admins.esp.alive";
    private const int ReconcileTicks = 8;
    private const int MapStartPauseTicks = 192;
    private const uint BoneMerge = 256;
    private const uint InStagingList = 1 << 2;
    private const uint NoEntity = uint.MaxValue;

    private static readonly Color TColor = Color.FromBuiltin(System.Drawing.Color.Red);
    private static readonly Color CtColor = Color.FromBuiltin(System.Drawing.Color.Blue);

    private readonly ISwiftlyCore _core;
    private readonly ILogger<EspService> _logger;
    private readonly IOptionsMonitor<Configuration> _config;
    private readonly Messenger _messenger;

    private readonly Dictionary<int, ulong> _viewers = [];
    private readonly Dictionary<int, Glow> _glows = [];
    private readonly List<int> _missing = [];
    private readonly HashSet<(int Slot, ulong SessionId, string Model)> _failed = [];
    private List<(int Slot, uint Observed)> _seeing = [];
    private int _ticks;
    private int _pause;

    public EspService(ISwiftlyCore core, ILogger<EspService> logger, IOptionsMonitor<Configuration> config, Messenger messenger)
    {
        _core = core;
        _logger = logger;
        _config = config;
        _messenger = messenger;

        core.Registrator.Register(this);
    }

    private sealed record Target(ulong SessionId, CCSPlayerPawn Pawn, uint PawnIndex, string Model, byte Team, bool Alive);

    private sealed class Glow
    {
        public required Target Target { get; init; }

        public required CHandle<CDynamicProp> Relay { get; init; }

        public required CHandle<CDynamicProp> Outline { get; init; }

        // -1 once the engine deleted that prop itself.
        public required int RelayIndex { get; set; }

        public required int OutlineIndex { get; set; }

        // Slots the pair is networked to; it is blocked for every other slot.
        public ulong Visible { get; set; }

        public bool Intact => RelayIndex >= 0 && OutlineIndex >= 0;

        public bool Shows(Target target) =>
            Target.SessionId == target.SessionId && Target.PawnIndex == target.PawnIndex && Target.Model == target.Model && Target.Team == target.Team;
    }

    [Command("esp", permission: Permission)]
    [CommandAlias("wh")]
    [CommandAlias("xray")]
    public void EspCommand(ICommandContext context) => Toggle(Issuer.From(context));

    public bool IsEnabled(IPlayer viewer) => _viewers.TryGetValue(viewer.Slot, out var sessionId) && sessionId == viewer.SessionId;

    public void Toggle(Issuer issuer)
    {
        if (issuer.Player is not { } player)
        {
            _messenger.Reply(issuer, "command.player_only");
            return;
        }

        if (IsEnabled(player))
        {
            _viewers.Remove(player.Slot);
            _messenger.Reply(issuer, "esp.disabled");
        }
        else
        {
            _viewers[player.Slot] = player.SessionId;
            _messenger.Reply(issuer, SeesWhileAlive(player) ? "esp.enabled" : "esp.enabled_dead");
        }

        _ticks = ReconcileTicks;
    }

    // Entity work never runs in the command: building every pair inside a chat command once took long enough for the
    // server to drop the admin who typed it.
    [EventListener<EventDelegates.OnTick>]
    public void OnTick()
    {
        // The first ticks of a map shuffle bots and kick SourceTV; player and pawn reads are unsafe then.
        if (_pause > 0)
        {
            _pause--;
            return;
        }

        if (_viewers.Count == 0 && _glows.Count == 0) return;

        try
        {
            if (++_ticks >= ReconcileTicks)
            {
                _ticks = 0;
                Reconcile();
            }

            BuildNext();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ESP update failed.");
        }
    }

    [EventListener<EventDelegates.OnClientConnected>]
    public void OnClientConnected(IOnClientConnectedEvent e)
    {
        var slot = e.PlayerId;
        _viewers.Remove(slot);
        if (_glows.Count == 0 || _core.PlayerManager.GetPlayer(slot) is not { } player) return;

        // A new connection starts with nothing blocked, so deny before its first snapshot.
        foreach (var glow in _glows.Values)
        {
            Block(player, glow, true);
            glow.Visible &= ~(1UL << slot);
        }
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        _viewers.Remove(e.PlayerId);
        foreach (var glow in _glows.Values) glow.Visible &= ~(1UL << e.PlayerId);
    }

    [EventListener<EventDelegates.OnMapUnload>]
    public void OnMapUnload(IOnMapUnloadEvent e)
    {
        // The props go with the map, but the blocks stay with the players, who stay connected.
        foreach (var glow in _glows.Values)
        {
            Release(glow.RelayIndex);
            Release(glow.OutlineIndex);
        }

        _glows.Clear();
        _missing.Clear();
        _failed.Clear();
        _pause = MapStartPauseTicks;
    }

    [EventListener<EventDelegates.OnEntityDeleted>]
    public void OnEntityDeleted(IOnEntityDeletedEvent e)
    {
        if (_glows.Count == 0) return;

        // A round restart deletes the props and hands their indices to new entities at once, so the block goes now;
        // the next reconcile replaces the broken pair.
        var index = (int)e.Entity.Index;
        foreach (var glow in _glows.Values)
        {
            if (glow.RelayIndex == index) glow.RelayIndex = -1;
            else if (glow.OutlineIndex == index) glow.OutlineIndex = -1;
            else continue;

            Release(index);
            return;
        }
    }

    private void Reconcile()
    {
        var seeing = new List<(int Slot, uint Observed)>();

        foreach (var (slot, sessionId) in _viewers.ToList())
        {
            if (_core.PlayerManager.GetPlayer(slot) is not { } viewer || viewer.SessionId != sessionId || !_core.Permission.PlayerHasPermission(viewer.SteamID, Permission))
            {
                _viewers.Remove(slot);
                continue;
            }

            if (!IsAlive(viewer) || SeesWhileAlive(viewer)) seeing.Add((slot, ObservedInEye(viewer)));
        }

        _seeing = seeing;

        var targets = new Dictionary<int, Target>();
        if (_viewers.Count > 0)
        {
            foreach (var player in _core.PlayerManager.GetAllPlayers())
            {
                if (TargetOf(player) is { } target) targets[player.Slot] = target;
            }
        }

        foreach (var (slot, glow) in _glows.ToList())
        {
            if (targets.TryGetValue(slot, out var target) && glow.Intact && glow.Shows(target)) SetVisible(slot, glow, target.Alive);
            else Retire(slot, glow);
        }

        // A dead player keeps their pair, hidden, so respawn-heavy modes do not rebuild one on every death.
        _missing.Clear();
        foreach (var (slot, target) in targets)
        {
            if (target.Alive && !_glows.ContainsKey(slot) && !_failed.Contains((slot, target.SessionId, target.Model))) _missing.Add(slot);
        }
    }

    private void BuildNext()
    {
        while (_viewers.Count > 0 && _missing.Count > 0)
        {
            var slot = _missing[^1];
            _missing.RemoveAt(_missing.Count - 1);

            if (_glows.ContainsKey(slot) || _core.PlayerManager.GetPlayer(slot) is not { } player || TargetOf(player) is not { Alive: true } target) continue;

            if (Build(target) is { } glow)
            {
                _glows[slot] = glow;
                SetVisible(slot, glow, alive: true);
            }
            else
            {
                _failed.Add((slot, target.SessionId, target.Model));
            }

            // Spawning a pair is the expensive part; one per tick keeps every frame short.
            return;
        }
    }

    private Glow? Build(Target target)
    {
        CDynamicProp? relay = null;
        CDynamicProp? outline = null;

        try
        {
            relay = _core.EntitySystem.CreateEntityByDesignerName<CDynamicProp>("prop_dynamic");
            outline = _core.EntitySystem.CreateEntityByDesignerName<CDynamicProp>("prop_dynamic");
            if (relay is null || outline is null) throw new InvalidOperationException("prop_dynamic could not be created.");

            relay.Spawnflags = BoneMerge;
            relay.RenderMode = RenderMode_t.kRenderNone;
            outline.Spawnflags = BoneMerge;
            Spawn(relay, target.Model);
            Spawn(outline, target.Model);

            // Blocked for everyone before the first snapshot; SetVisible opens them to the allowed viewers.
            _core.PlayerManager.ShouldBlockTransmitEntity((int)relay.Index, true);
            _core.PlayerManager.ShouldBlockTransmitEntity((int)outline.Index, true);

            outline.Render = new Color(0, 0, 0, 1);
            var glow = outline.Glow;
            glow.GlowColorOverride = target.Team == (byte)Team.T ? TColor : CtColor;
            glow.GlowRange = 5000;
            glow.GlowRangeMin = 0;
            glow.GlowTeam = -1;
            glow.GlowType = 3;

            relay.AcceptInput("FollowEntity", "!activator", target.Pawn, relay);
            outline.AcceptInput("FollowEntity", "!activator", relay, outline);

            return new Glow
            {
                Target = target,
                Relay = _core.EntitySystem.GetRefEHandle(relay),
                Outline = _core.EntitySystem.GetRefEHandle(outline),
                RelayIndex = (int)relay.Index,
                OutlineIndex = (int)outline.Index,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build an ESP outline for model {Model}.", target.Model);
            Discard(relay);
            Discard(outline);
            return null;
        }
    }

    // A prop_dynamic given its model through spawn keyvalues drew no outline. SetModel on a skeletal model asserts while
    // the entity is staged, hence clearing EF_IN_STAGING_LIST first.
    private static void Spawn(CDynamicProp prop, string model)
    {
        prop.Entity!.Flags &= ~InStagingList;
        prop.SetModel(model);
        prop.DispatchSpawn();
    }

    private void SetVisible(int targetSlot, Glow glow, bool alive)
    {
        ulong mask = 0;
        foreach (var (slot, observed) in _seeing)
        {
            // Seen from the target's own eyes the outline wraps the screen.
            if (alive && slot != targetSlot && observed != glow.Target.PawnIndex) mask |= 1UL << slot;
        }

        var changed = glow.Visible ^ mask;
        for (var slot = 0; changed != 0; slot++, changed >>= 1)
        {
            if ((changed & 1) == 0 || _core.PlayerManager.GetPlayer(slot) is not { } viewer) continue;

            Block(viewer, glow, (mask & (1UL << slot)) == 0);
        }

        glow.Visible = mask;
    }

    private static void Block(IPlayer player, Glow glow, bool block)
    {
        if (glow.RelayIndex >= 0) player.ShouldBlockTransmitEntity(glow.RelayIndex, block);
        if (glow.OutlineIndex >= 0) player.ShouldBlockTransmitEntity(glow.OutlineIndex, block);
    }

    private void Retire(int slot, Glow glow)
    {
        _glows.Remove(slot);
        Discard(glow.Relay.IsValid ? glow.Relay.Value : null, glow.RelayIndex);
        Discard(glow.Outline.IsValid ? glow.Outline.Value : null, glow.OutlineIndex);
    }

    private void Discard(CDynamicProp? prop) => Discard(prop, prop is { IsValid: true } ? (int)prop.Index : -1);

    // Blocks are kept per entity index and the engine reuses indices, so they are released before the despawn.
    private void Discard(CDynamicProp? prop, int index)
    {
        Release(index);
        if (prop is { IsValid: true }) prop.Despawn();
    }

    private void Release(int index)
    {
        if (index >= 0) _core.PlayerManager.ShouldBlockTransmitEntity(index, false);
    }

    private static bool IsAlive(IPlayer player) =>
        player.PlayerPawn is { IsValid: true } pawn && pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE;

    private bool SeesWhileAlive(IPlayer viewer) =>
        _config.CurrentValue.Esp.AllowWhileAlive || _core.Permission.PlayerHasPermission(viewer.SteamID, AlivePermission);

    private static Target? TargetOf(IPlayer player)
    {
        if (player.Controller is not { IsValid: true, IsHLTV: false }) return null;
        if (player.PlayerPawn is not { IsValid: true } pawn) return null;

        var team = pawn.TeamNum;
        if (team != (byte)Team.T && team != (byte)Team.CT) return null;

        var model = pawn.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState.ModelName;
        return string.IsNullOrEmpty(model)
            ? null
            : new Target(player.SessionId, pawn, pawn.Index, model, team, pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE);
    }

    private static uint ObservedInEye(IPlayer viewer)
    {
        if (viewer.Controller is not { IsValid: true } controller || controller.ObserverPawn is not { IsValid: true } handle) return NoEntity;
        if (handle.Value?.ObserverServices is not { } services || services.ObserverMode != (byte)ObserverMode_t.OBS_MODE_IN_EYE) return NoEntity;

        return services.ObserverTarget is { IsValid: true } target && target.Value is { IsValid: true } entity ? entity.Index : NoEntity;
    }

    public void Dispose()
    {
        foreach (var (slot, glow) in _glows.ToList()) Retire(slot, glow);
    }
}
