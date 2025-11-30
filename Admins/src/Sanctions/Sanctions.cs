using Admins.Bans;
using Admins.Contract;
using Admins.Database.Models;
using Dommel;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Admins.Sanctions;

public partial class ServerSanctions
{
    [SwiftlyInject]
    private static ISwiftlyCore Core = null!;

    public static List<ISanction> Sanctions { get; set; } = [];
    public static Dictionary<ulong, VoiceFlagValue> OriginalVoiceFlags = new();

    public static void Load()
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            Sanctions = [.. database.GetAll<Sanction>()];
        });
    }

    public static void ScheduleCheck()
    {
        var players = Core.PlayerManager.GetAllPlayers();
        foreach (var player in players)
        {
            if (player.IsFakeClient) continue;
            if (!ServerBans.CheckPlayer(player)) continue;

            if (IsPlayerMuted(player, out var sanction))
            {
                if (player.VoiceFlags != VoiceFlagValue.Muted)
                {
                    OriginalVoiceFlags[player.SteamID] = player.VoiceFlags;
                    player.VoiceFlags = VoiceFlagValue.Muted;
                    var localizer = Core.Translation.GetPlayerLocalizer(player);
                    string muteMessage = localizer[
                        "mute.message",
                        sanction!.ExpiresAt == 0 ? localizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds((long)sanction!.ExpiresAt).ToString("yyyy-MM-dd HH:mm:ss")
                    ];
                    player.SendChat(muteMessage);
                }
            }
            else
            {
                if (OriginalVoiceFlags.TryGetValue(player.SteamID, out var originalFlags))
                {
                    player.VoiceFlags = originalFlags;
                    OriginalVoiceFlags.Remove(player.SteamID);
                }
            }
        }
    }

    public static void AddSanction(ISanction sanction)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            var id = database.Insert((Sanction)sanction);
            sanction.Id = (ulong)id;
            Sanctions.Add(sanction);
        });
    }

    public static void RemoveSanction(ISanction sanction)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            database.Delete((Sanction)sanction);
            Sanctions.Remove(sanction);
        });
    }

    public static void UpdateSanction(ISanction sanction)
    {
        Task.Run(() =>
        {
            var database = Core.Database.GetConnection("admins");
            database.Update((Sanction)sanction);
            Sanctions.RemoveAt(Sanctions.FindIndex(s => s.Id == sanction.Id));
            Sanctions.Add(sanction);
        });
    }

    public static List<ISanction> FindActiveSanctions(ulong steamId64)
    {
        var currentTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Sanctions.FindAll(sanction =>
            sanction.SteamId64 == steamId64 &&
            (sanction.ExpiresAt == 0 || sanction.ExpiresAt > currentTime) &&
            (sanction.Server == Admins.ServerGUID || sanction.Global)
        );
    }

    public static bool IsPlayerMuted(IPlayer player, out ISanction? sanction)
    {
        var sanctions = FindActiveSanctions(player.SteamID);
        if (sanctions.Count == 0)
        {
            sanction = null;
            return false;
        }

        sanction = sanctions.Find(sanction => sanction.SanctionType == SanctionKind.Mute);
        return sanction != null;
    }

    public static bool IsPlayerGagged(IPlayer player, out ISanction? sanction)
    {
        var sanctions = FindActiveSanctions(player.SteamID);
        if (sanctions.Count == 0)
        {
            sanction = null;
            return false;
        }

        sanction = sanctions.Find(sanction => sanction.SanctionType == SanctionKind.Gag);
        return sanction != null;
    }
}