using Admins.Shared;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

/// <summary>
/// Players who left in the last few minutes, so a player who leaves to dodge a punishment can still be punished from
/// the menu. Kept in memory only. Game thread only.
/// </summary>
public sealed class RecentPlayers
{
    private const int Capacity = 50;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ISwiftlyCore _core;
    private readonly Dictionary<int, (ulong SessionId, string Ip)> _ips = [];
    private readonly List<Entry> _entries = [];

    public RecentPlayers(ISwiftlyCore core)
    {
        _core = core;
        core.Registrator.Register(this);
    }

    public sealed record Entry(ulong SteamId, string Name, string Ip, DateTime LeftAt);

    [EventListener<EventDelegates.OnClientPutInServer>]
    public void OnClientPutInServer(IOnClientPutInServerEvent e)
    {
        if (e.Kind != ClientKind.Player) return;

        var slot = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            if (_core.PlayerManager.GetPlayer(slot) is { IsFakeClient: false } player) _ips[slot] = (player.SessionId, Net.NormalizeIp(player.IPAddress));
        });
    }

    // The leaver's SteamID and name are only readable from the event at this point.
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect e)
    {
        if (e.XuID == 0 || e.UserIdPlayer is not { } player) return HookResult.Continue;

        var ip = _ips.TryGetValue(player.Slot, out var known) && known.SessionId == player.SessionId ? known.Ip : string.Empty;

        _entries.RemoveAll(entry => entry.SteamId == e.XuID);
        _entries.Insert(0, new Entry(e.XuID, e.Name, ip, DateTime.UtcNow));
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);

        return HookResult.Continue;
    }

    /// <summary>Newest first, without anyone who has come back.</summary>
    public IReadOnlyList<Entry> List()
    {
        var cutoff = DateTime.UtcNow - Window;
        _entries.RemoveAll(entry => entry.LeftAt < cutoff);

        var online = _core.PlayerManager.GetAllPlayers().Where(p => !p.IsFakeClient).Select(p => p.SteamID).ToHashSet();
        return [.. _entries.Where(entry => !online.Contains(entry.SteamId))];
    }
}
