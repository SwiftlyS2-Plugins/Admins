using Admins.API;
using Admins.Bans;
using Admins.Contract;
using Admins.Database;
using Admins.Database.Models;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Admins;

[PluginMetadata(Id = "Admins", Version = "1.0.0", Name = "Admins", Author = "Swiftly Development Team")]
public partial class Admins : BasePlugin
{
    public static Groups.Groups Groups = new();
    public static ServerAdmins.ServerAdmins ServerAdmins = new();
    public static string ServerGUID = string.Empty;

    public static AdminAPIv1 AdminAPI = new();

    public Admins(ISwiftlyCore core) : base(core)
    {
        var connection = core.Database.GetConnection("admins");
        var connectionString = core.Database.GetConnectionString("admins");

        MigrationRunner.RunMigrations(connection, connectionString);
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IAdminAPIv1, AdminAPIv1>("admins.api.v1", AdminAPI);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {

    }

    public override void Load(bool hotReload)
    {
        if (!File.Exists(Path.Combine(Core.PluginDataDirectory, "server_id.txt")))
        {
            ServerGUID = Guid.NewGuid().ToString();
            File.WriteAllText(Path.Combine(Core.PluginDataDirectory, "server_id.txt"), ServerGUID);
            Core.Logger.LogWarning("A new Server GUID has been generated.");
        }
        else
        {
            ServerGUID = File.ReadAllText(Path.Combine(Core.PluginDataDirectory, "server_id.txt"));
            if (ServerGUID.Length != 36)
            {
                ServerGUID = Guid.NewGuid().ToString();
                File.WriteAllText(Path.Combine(Core.PluginDataDirectory, "server_id.txt"), ServerGUID);
                Core.Logger.LogWarning("Invalid Server GUID detected. A new GUID has been generated.");
            }
        }

        global::Admins.Groups.Groups.Load();
    }

    public override void Unload()
    {
    }

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        int playerid = @event.PlayerId;

        var player = Core.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        var ban = ServerBans.FindActiveBan(player.SteamID, player.IPAddress);
        if (ban != null)
        {
            string kickMessage = Core.Translation.GetPlayerLocalizer(player)[
                "ban.kick_message",
                ban.Reason,
                ban.ExpiresAt == 0 ? Core.Translation.GetPlayerLocalizer(player)["never"] : DateTimeOffset.FromUnixTimeMilliseconds((long)ban.ExpiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                ban.AdminName,
                ban.AdminSteamId64.ToString()
            ];
            player.SendMessage(MessageType.Console, kickMessage);
            player.Kick(kickMessage, SwiftlyS2.Shared.ProtobufDefinitions.ENetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_BANNED);
            return;
        }

        Task.Run(() =>
        {
            var admin = AdminAPI.GetAdmin(playerid);
            if (admin == null) return;

            global::Admins.ServerAdmins.ServerAdmins.AssignAdmin(player, (Admin)admin);
        });
    }
}