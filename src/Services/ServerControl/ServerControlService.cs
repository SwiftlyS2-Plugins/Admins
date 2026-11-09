using System.Globalization;
using Admins.Shared;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins.Services.ServerControl;

public sealed class ServerControlService
{
    private const float MapChangeDelaySeconds = 3f;

    // Long enough to cover the delay and the changelevel; a change that never happens (bad workshop id) stops blocking after it.
    private const long MapChangePendingMs = 15_000;

    private readonly ISwiftlyCore _core;
    private readonly Messenger _messenger;
    private long _mapChangeUntil;

    public ServerControlService(ISwiftlyCore core, Messenger messenger)
    {
        _core = core;
        _messenger = messenger;

        core.Registrator.Register(this);
    }

    [Command("restartround", permission: "admins.commands.restartround")]
    [CommandAlias("rr")]
    public void RestartRoundCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        var delay = 1f;

        if (context.Args.Length > 0
            && (!float.TryParse(context.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out delay) || delay < 0 || delay > 300))
        {
            _messenger.Reply(issuer, "command.invalid_value_range", context.Args[0], "delay", 0, 300);
            return;
        }

        RestartRound(issuer, delay);
    }

    [Command("say", permission: "admins.commands.say")]
    public void SayCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        if (context.Args.Length == 0)
        {
            _messenger.Syntax(issuer, "say", "<message>");
            return;
        }

        _messenger.Broadcast("command.say", issuer.Name, string.Join(' ', context.Args));
    }

    [Command("csay", permission: "admins.commands.csay")]
    public void CenterSayCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        if (context.Args.Length == 0)
        {
            _messenger.Syntax(issuer, "csay", "<message>");
            return;
        }

        _core.PlayerManager.SendCenter($"{issuer.Name}: {string.Join(' ', context.Args)}");
    }

    [Command("rcon", permission: "admins.commands.rcon")]
    public void RconCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        if (context.Args.Length == 0)
        {
            _messenger.Syntax(issuer, "rcon", "<command>");
            return;
        }

        _core.Engine.ExecuteCommand(string.Join(' ', context.Args));
    }

    [Command("map", permission: "admins.commands.map")]
    [CommandAlias("changemap")]
    public void MapCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        if (context.Args.Length == 0)
        {
            _messenger.Syntax(issuer, "map", "<map_name|ws:workshop_id>");
            return;
        }

        ChangeMap(issuer, context.Args[0]);
    }

    [Command("clean", permission: "admins.commands.clean")]
    public void CleanCommand(ICommandContext context) => CleanWeapons(Issuer.From(context));

    public void RestartRound(Issuer issuer, float delay)
    {
        if (_core.EntitySystem.GetGameRules() is { IsValid: true } rules)
        {
            rules.TerminateRound(RoundEndReason.RoundDraw, delay);
        }

        _messenger.Broadcast("command.restartround", issuer.Name, delay);
    }

    public bool MapChangePending => Environment.TickCount64 < _mapChangeUntil;

    /// <summary>
    /// Whether <paramref name="map"/> names an installed map or a workshop map (<c>ws:&lt;id|name&gt;</c>).
    /// </summary>
    public bool IsKnownMap(string map)
    {
        var name = map.Trim();
        return name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) ? name.Length > 3 : name.Length > 0 && _core.Engine.IsMapValid(name);
    }

    /// <summary>
    /// Changes the map after a short delay. <c>ws:&lt;id&gt;</c> or <c>ws:&lt;name&gt;</c> selects a workshop map.
    /// </summary>
    public bool ChangeMap(Issuer issuer, string map)
    {
        var name = map.Trim();
        string command;

        if (MapChangePending)
        {
            _messenger.Reply(issuer, "command.map_pending");
            return false;
        }

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            var workshop = name[3..];
            if (workshop.Length == 0)
            {
                _messenger.Reply(issuer, "command.map_not_found", name);
                return false;
            }

            command = ulong.TryParse(workshop, out var id) ? $"host_workshop_map {id}" : $"ds_workshop_changelevel {workshop}";
        }
        else
        {
            if (!_core.Engine.IsMapValid(name))
            {
                _messenger.Reply(issuer, "command.map_not_found", name);
                return false;
            }

            command = $"changelevel {name}";
        }

        _messenger.Broadcast("command.changing_map", issuer.Name, name);
        _mapChangeUntil = Environment.TickCount64 + MapChangePendingMs;
        _core.Scheduler.DelayBySeconds(MapChangeDelaySeconds, () => _core.Engine.ExecuteCommand(command));
        return true;
    }

    public void CleanWeapons(Issuer issuer)
    {
        var count = 0;

        foreach (var entity in _core.EntitySystem.GetAllEntitiesByClass<CBaseEntity>().ToList())
        {
            if (!entity.IsValid) continue;

            var name = entity.DesignerName;
            if (name.StartsWith("weapon_", StringComparison.Ordinal) && name != "weapon_c4" && !entity.OwnerEntity.IsValid)
            {
                entity.Despawn();
                count++;
            }
        }

        _messenger.Broadcast("command.clean_success", issuer.Name, count);
    }
}
