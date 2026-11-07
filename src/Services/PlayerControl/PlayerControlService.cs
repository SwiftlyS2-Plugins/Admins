using Admins.Services.Access;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace Admins.Services.PlayerControl;

/// <summary>
/// Player actions shared by the chat commands and the admin menu. Every action runs on the game thread, checks
/// immunity itself, replies to the issuer on failure and announces success to the server.
/// </summary>
public sealed partial class PlayerControlService : IDisposable
{
    private const float BeaconIntervalSeconds = 1.5f;
    private const string BeaconSound = "UIPanorama.popup_accept_match_beep";
    private const int BeaconSegments = 16;
    private const float DrugIntervalSeconds = 1f;
    private const int ArmorItemDefinitionIndex = 51;

    private static readonly Vector VectorZero = new(0, 0, 0);
    private static readonly QAngle RotationZero = new(0, 0, 0);

    private readonly ISwiftlyCore _core;
    private readonly ILogger<PlayerControlService> _logger;
    private readonly AccessService _access;
    private readonly Messenger _messenger;
    private readonly TargetResolver _targets;

    // Game thread only, keyed by slot.
    private readonly Dictionary<int, (ulong SessionId, CancellationTokenSource Timer)> _beacons = [];
    private readonly Dictionary<int, (ulong SessionId, CancellationTokenSource Timer)> _drugs = [];
    private readonly Dictionary<int, (ulong SessionId, Vector Position)> _deathPositions = [];

    public PlayerControlService(ISwiftlyCore core, ILogger<PlayerControlService> logger, AccessService access, Messenger messenger, TargetResolver targets)
    {
        _core = core;
        _logger = logger;
        _access = access;
        _messenger = messenger;
        _targets = targets;

        core.Registrator.Register(this);
    }

    #region State tracking

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath e)
    {
        if (e.UserIdPlayer is not { } player) return HookResult.Continue;

        if (player.PlayerPawn is { IsValid: true } pawn && pawn.AbsOrigin is { } origin)
        {
            _deathPositions[player.Slot] = (player.SessionId, origin);
        }

        StopDrug(player.Slot, clearScreen: true);
        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        StopBeacon(e.PlayerId);
        StopDrug(e.PlayerId, clearScreen: false);
        _deathPositions.Remove(e.PlayerId);
    }

    [EventListener<EventDelegates.OnMapUnload>]
    public void OnMapUnload(IOnMapUnloadEvent e)
    {
        foreach (var slot in _beacons.Keys.ToList()) StopBeacon(slot);
        foreach (var slot in _drugs.Keys.ToList()) StopDrug(slot, clearScreen: false);
        _deathPositions.Clear();
    }

    public bool IsFrozen(IPlayer target) => AlivePawnOf(target) is { } pawn && pawn.ActualMoveType == MoveType_t.MOVETYPE_INVALID;

    public bool IsNoclip(IPlayer target) => AlivePawnOf(target) is { } pawn && pawn.ActualMoveType == MoveType_t.MOVETYPE_NOCLIP;

    public bool IsGod(IPlayer target) => AlivePawnOf(target) is { } pawn && !pawn.TakesDamage;

    public bool HasBeacon(IPlayer target) => _beacons.TryGetValue(target.Slot, out var beacon) && beacon.SessionId == target.SessionId;

    public bool IsDrugged(IPlayer target) => _drugs.TryGetValue(target.Slot, out var drug) && drug.SessionId == target.SessionId;

    public bool HasDeathPosition(IPlayer target) => _deathPositions.TryGetValue(target.Slot, out var death) && death.SessionId == target.SessionId;

    #endregion

    #region Actions

    public bool Kick(Issuer issuer, IPlayer target, string? reason)
    {
        if (!CanAct(issuer, target)) return false;

        var name = Names.Of(target);
        var text = string.IsNullOrWhiteSpace(reason)
            ? _messenger.Text(target, "kick.message", issuer.Name)
            : _messenger.Text(target, "kick.message_reason", issuer.Name, reason);

        target.Kick(text, ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
        _messenger.Broadcast("command.kick_success", issuer.Name, name);
        return true;
    }

    public bool Slay(Issuer issuer, IPlayer target)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        pawn.CommitSuicide(false, true);
        _messenger.Broadcast("command.slay_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool Slap(Issuer issuer, IPlayer target, int damage)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        pawn.Health = Math.Max(pawn.Health - damage, 0);
        pawn.HealthUpdated();

        if (pawn.Health == 0)
        {
            pawn.CommitSuicide(false, true);
        }
        else
        {
            pawn.Teleport(null, null, new Vector(
                Random.Shared.Next(50, 230) * (Random.Shared.Next(2) == 0 ? -1 : 1),
                Random.Shared.Next(50, 230) * (Random.Shared.Next(2) == 0 ? -1 : 1),
                Random.Shared.Next(100, 300)));
        }

        _messenger.Broadcast("command.slap_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool SetFrozen(Issuer issuer, IPlayer target, bool frozen)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        SetMoveType(pawn, frozen ? MoveType_t.MOVETYPE_INVALID : MoveType_t.MOVETYPE_WALK);
        _messenger.Broadcast(frozen ? "command.freeze_success" : "command.unfreeze_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool SetNoclip(Issuer issuer, IPlayer target, bool enabled)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        SetMoveType(pawn, enabled ? MoveType_t.MOVETYPE_NOCLIP : MoveType_t.MOVETYPE_WALK);
        _messenger.Broadcast(enabled ? "command.noclip_on" : "command.noclip_off", issuer.Name, Names.Of(target));
        return true;
    }

    public bool SetGod(Issuer issuer, IPlayer target, bool enabled)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        pawn.TakesDamage = !enabled;
        pawn.TakesDamageUpdated();
        _messenger.Broadcast(enabled ? "command.god_on" : "command.god_off", issuer.Name, Names.Of(target));
        return true;
    }

    public bool SetBeacon(Issuer issuer, IPlayer target, bool enabled)
    {
        if (!CanAct(issuer, target)) return false;

        if (enabled)
        {
            if (!HasBeacon(target))
            {
                StopBeacon(target.Slot);
                var reference = PlayerRef.Of(target);
                DrawBeacon(reference);
                var timer = _core.Scheduler.DelayAndRepeatBySeconds(BeaconIntervalSeconds, BeaconIntervalSeconds, () => DrawBeacon(reference));
                _core.Scheduler.StopOnMapChange(timer);
                _beacons[target.Slot] = (target.SessionId, timer);
            }
        }
        else
        {
            StopBeacon(target.Slot);
        }

        _messenger.Broadcast(enabled ? "command.beacon_on" : "command.beacon_off", issuer.Name, Names.Of(target));
        return true;
    }

    public bool Bury(Issuer issuer, IPlayer target, float depth)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn || pawn.AbsOrigin is not { } origin) return false;

        pawn.Teleport(new Vector(origin.X, origin.Y, origin.Z + depth), pawn.AbsRotation, VectorZero);
        _messenger.Broadcast(depth < 0 ? "command.bury_success" : "command.unbury_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool SetBlind(Issuer issuer, IPlayer target, float holdSeconds)
    {
        if (!CanAct(issuer, target)) return false;

        // Hold 0 with no fade clears an earlier blind.
        var blind = holdSeconds > 0;
        SendFade(target, blind ? 0.2f : 0f, holdSeconds, 0x0001 | 0x0010, 0xFF000000);

        _messenger.Broadcast(blind ? "command.blind_success" : "command.unblind_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool Shake(Issuer issuer, IPlayer target)
    {
        if (!CanAct(issuer, target)) return false;

        using var shake = _core.NetMessage.Create<CUserMessageShake>();
        shake.Duration = 1.0f;
        shake.Amplitude = 10.0f;
        shake.Frequency = 1.0f;
        shake.Command = 0;
        shake.Recipients.AddRecipient(target.PlayerID);
        shake.Send();

        _messenger.Broadcast("command.shake_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool SetDrug(Issuer issuer, IPlayer target, bool enabled)
    {
        if (!CanAct(issuer, target)) return false;

        if (enabled)
        {
            if (AlivePawn(issuer, target) is null) return false;

            if (!IsDrugged(target))
            {
                StopDrug(target.Slot, clearScreen: false);
                var reference = PlayerRef.Of(target);
                var timer = _core.Scheduler.DelayAndRepeatBySeconds(DrugIntervalSeconds, DrugIntervalSeconds, () => DrugTick(reference));
                _core.Scheduler.StopOnMapChange(timer);
                _drugs[target.Slot] = (target.SessionId, timer);
                DrugTick(reference);
            }
        }
        else
        {
            StopDrug(target.Slot, clearScreen: true);
        }

        _messenger.Broadcast(enabled ? "command.drug_on" : "command.drug_off", issuer.Name, Names.Of(target));
        return true;
    }

    public bool SetColor(Issuer issuer, IPlayer target, Color color, bool glow)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        if (glow) pawn.RenderMode = RenderMode_t.kRenderTransAlpha;
        pawn.Render = color;
        pawn.RenderUpdated();

        _messenger.Broadcast(glow ? "command.glow_success" : "command.color_success", issuer.Name, Names.Of(target), color.R, color.G, color.B, color.A);
        return true;
    }

    public bool SetSpeed(Issuer issuer, IPlayer target, float multiplier)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        pawn.VelocityModifier = multiplier;
        pawn.VelocityModifierUpdated();
        _messenger.Broadcast("command.setspeed_success", issuer.Name, Names.Of(target), multiplier);
        return true;
    }

    public bool SetGravity(Issuer issuer, IPlayer target, float multiplier)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        pawn.ActualGravityScale = multiplier;
        pawn.GravityScale = multiplier;
        pawn.GravityScaleUpdated();
        _messenger.Broadcast("command.setgravity_success", issuer.Name, Names.Of(target), multiplier);
        return true;
    }

    public bool SetHealth(Issuer issuer, IPlayer target, int health, int armor, bool helmet)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn) return false;

        if (health <= 0)
        {
            pawn.CommitSuicide(false, true);
        }
        else
        {
            pawn.Health = health;
            pawn.HealthUpdated();
        }

        if (pawn.ItemServices is { IsValid: true } items && pawn.WeaponServices is { IsValid: true } weapons)
        {
            if (helmet)
            {
                items.GiveItem("item_assaultsuit");
            }
            else if (weapons.MyValidWeapons.FirstOrDefault(w => w.AttributeManager.Item.ItemDefinitionIndex == ArmorItemDefinitionIndex) is { } suit)
            {
                weapons.RemoveWeapon(suit);
            }
        }

        pawn.ArmorValue = armor;
        pawn.ArmorValueUpdated();

        _messenger.Broadcast("command.hp_success", issuer.Name, Names.Of(target), health, armor, _messenger.Text(null, helmet ? "yes" : "no"));
        return true;
    }

    public bool Rename(Issuer issuer, IPlayer target, string name)
    {
        if (!CanAct(issuer, target) || target.Controller is not { IsValid: true } controller) return false;

        var old = Names.Of(target);
        controller.PlayerName = name;
        controller.PlayerNameUpdated();
        _messenger.Broadcast("command.rename_success", issuer.Name, old, name);
        return true;
    }

    public bool SetMoney(Issuer issuer, IPlayer target, int amount, bool add)
    {
        if (!CanAct(issuer, target) || target.Controller is not { IsValid: true } controller || controller.InGameMoneyServices is not { IsValid: true } money) return false;

        money.Account = add ? money.Account + amount : amount;
        money.AccountUpdated();
        _messenger.Broadcast(add ? "command.givemoney_success" : "command.setmoney_success", issuer.Name, amount, Names.Of(target));
        return true;
    }

    public bool Respawn(Issuer issuer, IPlayer target)
    {
        if (!CanAct(issuer, target)) return false;

        if (target.Controller is not { IsValid: true } controller || controller.TeamNum < (byte)Team.T)
        {
            _messenger.Reply(issuer, "command.not_in_team", Names.Of(target));
            return false;
        }

        target.Respawn();
        _messenger.Broadcast("command.respawn_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool RespawnAtDeath(Issuer issuer, IPlayer target)
    {
        if (!CanAct(issuer, target)) return false;

        if (!_deathPositions.TryGetValue(target.Slot, out var death) || death.SessionId != target.SessionId)
        {
            _messenger.Reply(issuer, "command.hrespawn_no_last_position", Names.Of(target));
            return false;
        }

        if (target.Controller is not { IsValid: true } controller || controller.TeamNum < (byte)Team.T)
        {
            _messenger.Reply(issuer, "command.not_in_team", Names.Of(target));
            return false;
        }

        target.Respawn();

        var reference = PlayerRef.Of(target);
        _core.Scheduler.NextTick(() =>
        {
            if (reference.Resolve(_core)?.PlayerPawn is { IsValid: true } pawn) pawn.Teleport(death.Position, pawn.AbsRotation, VectorZero);
        });

        _messenger.Broadcast("command.hrespawn_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool MoveToTeam(Issuer issuer, IPlayer target, Team team)
    {
        if (!CanAct(issuer, target)) return false;

        if (target.IsAlive) target.SwitchTeam(team);
        else target.ChangeTeam(team);

        _messenger.Broadcast("command.team_success", issuer.Name, Names.Of(target), TeamName(team));
        return true;
    }

    public bool Swap(Issuer issuer, IPlayer target)
    {
        if (target.Controller is not { IsValid: true } controller || controller.TeamNum < (byte)Team.T)
        {
            _messenger.Reply(issuer, "command.not_in_team", Names.Of(target));
            return false;
        }

        return MoveToTeam(issuer, target, controller.TeamNum == (byte)Team.T ? Team.CT : Team.T);
    }

    public bool Goto(Issuer issuer, IPlayer target)
    {
        if (issuer.Player is not { } admin)
        {
            _messenger.Reply(issuer, "command.player_only");
            return false;
        }

        if (AlivePawnOf(admin) is not { } adminPawn)
        {
            _messenger.Reply(issuer, "command.self_dead");
            return false;
        }

        if (AlivePawn(issuer, target) is not { } targetPawn || !TryBehind(targetPawn, out var position, out var rotation)) return false;

        adminPawn.Teleport(position, rotation, VectorZero);
        _messenger.Broadcast("command.goto_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool Bring(Issuer issuer, IPlayer target)
    {
        if (issuer.Player is not { } admin)
        {
            _messenger.Reply(issuer, "command.player_only");
            return false;
        }

        if (!CanAct(issuer, target)) return false;

        if (AlivePawnOf(admin) is not { } adminPawn || !TryBehind(adminPawn, out var position, out var rotation))
        {
            _messenger.Reply(issuer, "command.self_dead");
            return false;
        }

        if (AlivePawn(issuer, target) is not { } targetPawn) return false;

        targetPawn.Teleport(position, rotation, VectorZero);
        _messenger.Broadcast("command.bring_success", issuer.Name, Names.Of(target));
        return true;
    }

    public bool GiveItem(Issuer issuer, IPlayer target, string item)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn || pawn.ItemServices is not { IsValid: true } items) return false;

        items.GiveItem(item);
        _messenger.Broadcast("command.giveitem_success", issuer.Name, item, Names.Of(target));
        return true;
    }

    public bool Disarm(Issuer issuer, IPlayer target, bool keepKnife)
    {
        if (!CanAct(issuer, target) || AlivePawn(issuer, target) is not { } pawn || pawn.ItemServices is not { IsValid: true } items) return false;

        items.RemoveItems();
        if (keepKnife) items.GiveItem("weapon_knife");

        _messenger.Broadcast(keepKnife ? "command.melee_success" : "command.disarm_success", issuer.Name, Names.Of(target));
        return true;
    }

    #endregion

    #region Helpers

    private bool CanAct(Issuer issuer, IPlayer target)
    {
        if (_access.CanTarget(issuer.Player, target.SteamID)) return true;

        _messenger.Reply(issuer, "command.target_has_immunity", Names.Of(target), _access.GetImmunity(target.SteamID));
        return false;
    }

    private CCSPlayerPawn? AlivePawn(Issuer issuer, IPlayer target)
    {
        if (AlivePawnOf(target) is { } pawn) return pawn;

        _messenger.Reply(issuer, "command.target_not_alive", Names.Of(target));
        return null;
    }

    private static CCSPlayerPawn? AlivePawnOf(IPlayer player) =>
        player.PlayerPawn is { IsValid: true } pawn && pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE ? pawn : null;

    private static void SetMoveType(CCSPlayerPawn pawn, MoveType_t moveType)
    {
        pawn.ActualMoveType = moveType;
        pawn.MoveType = moveType;
        pawn.MoveTypeUpdated();
    }

    // 100 units behind the reference pawn, facing the same way.
    private static bool TryBehind(CCSPlayerPawn pawn, out Vector position, out QAngle rotation)
    {
        position = VectorZero;
        rotation = RotationZero;
        if (pawn.AbsOrigin is not { } origin || pawn.AbsRotation is not { } angles) return false;

        angles.ToDirectionVectors(out var forward, out _, out _);
        position = new Vector(origin.X - (forward.X * 100f), origin.Y - (forward.Y * 100f), origin.Z);
        rotation = angles;
        return true;
    }

    public string TeamName(Team team) => team switch
    {
        Team.CT => "CT",
        Team.T => "T",
        Team.Spectator => "Spectator",
        _ => "None",
    };

    private void StopBeacon(int slot)
    {
        if (_beacons.Remove(slot, out var beacon)) beacon.Timer.Cancel();
    }

    private void StopDrug(int slot, bool clearScreen)
    {
        if (!_drugs.Remove(slot, out var drug)) return;

        drug.Timer.Cancel();
        if (clearScreen && _core.PlayerManager.GetPlayer(slot) is { } player && player.SessionId == drug.SessionId) SendFade(player, 0, 0, 0x0010, 0);
    }

    // A new random tint and a shake every interval; each fade purges the previous one.
    private void DrugTick(PlayerRef reference)
    {
        if (reference.Resolve(_core) is not { } player || AlivePawnOf(player) is null)
        {
            StopDrug(reference.Slot, clearScreen: true);
            return;
        }

        using var shake = _core.NetMessage.Create<CUserMessageShake>();
        shake.Duration = DrugIntervalSeconds;
        shake.Amplitude = 12f;
        shake.Frequency = 1.5f;
        shake.Command = 0;
        shake.Recipients.AddRecipient(player.PlayerID);
        shake.Send();

        SendFade(player, 0.3f, DrugIntervalSeconds, 0x0001 | 0x0010, (uint)Random.Shared.Next(0x1000000) | (100u << 24));
    }

    private void SendFade(IPlayer player, float fadeSeconds, float holdSeconds, uint flags, uint color)
    {
        using var fade = _core.NetMessage.Create<CUserMessageFade>();
        fade.Duration = (uint)(fadeSeconds * 512);
        fade.HoldTime = (uint)(holdSeconds * 512);
        fade.Flags = flags;
        fade.Color = color;
        fade.Recipients.AddRecipient(player.PlayerID);
        fade.Send();
    }

    private void DrawBeacon(PlayerRef reference)
    {
        if (reference.Resolve(_core) is not { } player)
        {
            StopBeacon(reference.Slot);
            return;
        }

        if (AlivePawnOf(player) is not { } pawn || pawn.AbsOrigin is not { } origin) return;

        var color = player.Controller.TeamNum switch
        {
            (byte)Team.T => Color.FromBuiltin(System.Drawing.Color.Red),
            (byte)Team.CT => Color.FromBuiltin(System.Drawing.Color.Blue),
            _ => Color.FromBuiltin(System.Drawing.Color.White),
        };

        var center = new Vector(origin.X, origin.Y, origin.Z + 6f);
        DrawRing(center, 20f, color, 0.95f);
        DrawRing(center, 34f, color, 0.8f);

        using var sound = new SoundEvent(BeaconSound);
        sound.SourceEntityIndex = -1;
        sound.Recipients.AddRecipient(player.PlayerID);
        sound.Emit();
    }

    private void DrawRing(Vector center, float radius, Color color, float life)
    {
        Vector Point(int i)
        {
            var angle = i * (2.0 * Math.PI / BeaconSegments);
            return new Vector(center.X + (radius * (float)Math.Cos(angle)), center.Y + (radius * (float)Math.Sin(angle)), center.Z);
        }

        var previous = Point(BeaconSegments - 1);
        for (var i = 0; i < BeaconSegments; i++)
        {
            var current = Point(i);
            DrawBeam(previous, current, color, life);
            previous = current;
        }
    }

    private void DrawBeam(Vector start, Vector end, Color color, float life)
    {
        var beam = _core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");
        if (beam is null) return;

        beam.Render = color;
        beam.Width = 2.0f;
        beam.Teleport(start, RotationZero, VectorZero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        beam.DispatchSpawn();

        _core.Scheduler.DelayBySeconds(life, () =>
        {
            if (beam.IsValid) beam.Despawn();
        });
    }

    #endregion

    public void Dispose()
    {
        foreach (var slot in _beacons.Keys.ToList()) StopBeacon(slot);
        foreach (var slot in _drugs.Keys.ToList()) StopDrug(slot, clearScreen: true);
    }
}
