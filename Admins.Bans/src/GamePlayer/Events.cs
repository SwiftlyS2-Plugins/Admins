using Admins.Bans.Manager;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Admins.Bans.Players;

public partial class GamePlayer
{
    private ISwiftlyCore Core = null!;
    private ServerBans _serverBans = null!;

    public GamePlayer(ISwiftlyCore core, ServerBans serverBans)
    {
        Core = core;
        _serverBans = serverBans;

        Core.Scheduler.RepeatBySeconds(5f, () =>
        {
            var players = Core.PlayerManager.GetAllPlayers();
            foreach (var player in players)
            {
                if (player.IsFakeClient || !player.IsValid) continue;

                _serverBans.CheckPlayer(player);
            }
        });

        core.Registrator.Register(this);
    }

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var player = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (player == null) return;

        _serverBans.CheckPlayer(player);
    }

    [EventListener<EventDelegates.OnClientSteamAuthorizeFail>]
    public void OnClientSteamAuthorizeFail(IOnClientSteamAuthorizeFailEvent e)
    {
        var player = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (player == null) return;

        player.Kick("Steam authorization failed.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_AUTHINVALID);
    }
}