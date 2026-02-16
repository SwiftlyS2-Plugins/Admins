using System.Collections.Concurrent;
using Admins.Comms.Contract;
using Admins.Comms.Database.Models;
using Admins.Core.Contract;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Admins.Comms.Manager;

public class ServerComms
{
    private ISwiftlyCore Core = null!;
    private IConfigurationManager _configurationManager = null!;
    private IServerManager _serverManager = null!;

    public static ConcurrentDictionary<ulong, List<ISanction>> OnlinePlayerSanctions { get; set; } = [];

    public ServerComms(ISwiftlyCore core)
    {
        Core = core;
    }

    public void SetServerManager(IServerManager serverManager)
    {
        _serverManager = serverManager;
    }

    public void SetConfigurationManager(IConfigurationManager configurationManager)
    {
        _configurationManager = configurationManager;
    }

    public async Task LoadPlayerSanctionsAsync(ulong steamId, string playerIp)
    {
        if (_configurationManager.GetConfigurationMonitor()!.CurrentValue.UseDatabase == false)
            return;

        try
        {
            var db = Core.Database.GetConnection("admins");
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var serverGuid = _serverManager.GetServerGUID();
            var steamId64 = (long)steamId;

            var steamIdSanctions = await db.SelectAsync<Sanction>(s =>
                s.SteamId64 == steamId64 &&
                s.SanctionType == SanctionType.SteamID
            );

            var ipSanctions = !string.IsNullOrEmpty(playerIp)
                ? await db.SelectAsync<Sanction>(s =>
                    s.PlayerIp == playerIp &&
                    s.SanctionType == SanctionType.IP
                )
                : [];

            var activeSanctions = steamIdSanctions.Concat(ipSanctions)
                .Where(s =>
                    (s.ExpiresAt == 0 || s.ExpiresAt > currentTime) &&
                    (s.Server == serverGuid || s.GlobalSanction)
                )
                .Cast<ISanction>()
                .ToList();

            OnlinePlayerSanctions[steamId] = activeSanctions;
        }
        catch (Exception ex)
        {
            Core.Logger.LogError($"[Comms] Error loading sanctions for SteamID {steamId}: {ex.Message}");
        }
    }

    public void UnloadPlayer(ulong steamId)
    {
        OnlinePlayerSanctions.TryRemove(steamId, out _);
    }

    public void AddToOnlineCache(ulong steamId, ISanction sanction)
    {
        OnlinePlayerSanctions.AddOrUpdate(
            steamId,
            [sanction],
            (_, existing) =>
            {
                existing.Add(sanction);
                return existing;
            }
        );
    }

    public void RemoveFromOnlineCache(ulong steamId, long sanctionId)
    {
        if (OnlinePlayerSanctions.TryGetValue(steamId, out var sanctions))
        {
            sanctions.RemoveAll(s => s.Id == sanctionId);
        }
    }

    public async Task RefreshOnlinePlayerSanctionsAsync()
    {
        var players = Core.PlayerManager.GetAllPlayers();
        foreach (var player in players)
        {
            if (player.IsFakeClient || !player.IsValid)
                continue;

            await LoadPlayerSanctionsAsync(player.SteamID, player.IPAddress);
        }
    }

    public ISanction? FindActiveSanction(long steamId64, string playerIp, SanctionKind sanctionKind)
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (OnlinePlayerSanctions.TryGetValue((ulong)steamId64, out var sanctions))
        {
            return sanctions.FirstOrDefault(s =>
                s.SanctionKind == sanctionKind &&
                (s.ExpiresAt == 0 || s.ExpiresAt > currentTime)
            );
        }

        return null;
    }
}
