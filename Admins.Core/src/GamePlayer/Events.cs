using Admins.Bans.Contract;
using Admins.Comms.Contract;
using Admins.Core.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;

namespace Admins.Core.GamePlayer;

public class GamePlayer : IGamePlayer
{
    private readonly ISwiftlyCore Core;
    private IBansManager? BansManager;
    private ICommsManager? CommsManager;
    private IConfigurationManager ConfigurationManager = null!;
    private Admins.ServerAdmins? ServerAdmins;

    public GamePlayer(ISwiftlyCore core, IConfigurationManager configurationManager)
    {
        Core = core;
        ConfigurationManager = configurationManager;

        core.Registrator.Register(this);
    }

    public void SetBansManager(IBansManager? bansManager)
    {
        BansManager = bansManager;
    }

    public void SetCommsManager(ICommsManager? commsManager)
    {
        CommsManager = commsManager;
    }

    public void SetServerAdmins(Admins.ServerAdmins? serverAdmins)
    {
        ServerAdmins = serverAdmins;
    }

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var player = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (player == null) return;

        if (ServerAdmins != null && Admins.ServerAdmins.AllAdmins.ContainsKey(player.SteamID))
        {
            var admin = Admins.ServerAdmins.AllAdmins[player.SteamID];
            ServerAdmins.AssignAdmin(player, admin);
        }

        NotifyAdminsAboutPlayerRecord(player);
    }

    private void NotifyAdminsAboutPlayerRecord(IPlayer player)
    {
        Core.Scheduler.NextTick(() =>
        {
            var totalBans = 0;
            var totalGags = 0;
            var totalMutes = 0;

            if (BansManager != null)
            {
                var allBans = BansManager.FindBans((long)player.SteamID, player.IPAddress);
                totalBans = allBans.Count;
            }

            if (CommsManager != null)
            {
                var playerSanctions = CommsManager.FindSanctions((long?)player.SteamID, player.IPAddress);
                totalGags = playerSanctions.Count(s => s.SanctionKind == SanctionKind.Gag);
                totalMutes = playerSanctions.Count(s => s.SanctionKind == SanctionKind.Mute);
            }

            if (totalBans == 0 && totalGags == 0 && totalMutes == 0) return;

            var admins = Core.PlayerManager.GetAllPlayers()
                .Where(p => Core.Permission.PlayerHasPermission(p.SteamID, "admins.notify"))
                .ToList();

            if (admins.Count == 0) return;

            var playerName = player.Controller.IsValid ? player.Controller.PlayerName : "Unknown";

            foreach (var admin in admins)
            {
                var localizer = Core.Translation.GetPlayerLocalizer(admin);
                var message = localizer[
                    "notification.player_record",
                    ConfigurationManager.GetCurrentConfiguration()!.Prefix,
                    playerName,
                    totalBans,
                    totalGags,
                    totalMutes
                ];
                admin.SendChat(message);
            }
        });
    }
}
