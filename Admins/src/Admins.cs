using Admins.API;
using Admins.Bans;
using Admins.Commands;
using Admins.Configuration;
using Admins.Contract;
using Admins.Database;
using Admins.Database.Models;
using Admins.Sanctions;
using Dommel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Plugins;

namespace Admins;

[PluginMetadata(Id = "Admins", Version = "1.0.0", Name = "Admins", Author = "Swiftly Development Team")]
public partial class Admins : BasePlugin
{
    public static Groups.Groups Groups = new();
    public static ServerAdmins.ServerAdmins ServerAdmins = new();
    public static string ServerGUID = string.Empty;
    public static AdminAPIv1 AdminAPI = new();
    public static AdminBansAPIv1 AdminBansAPI = new();
    public static AdminSanctionsAPIv1 AdminSanctionsAPI = new();
    public static AdminMenuAPIv1 AdminsMenuAPI = new();
    private AdminCommands adminCommands = new();
    public static IOptionsMonitor<AdminsConfig> Config = null!;
    public static ISwiftlyCore SwiftlyCore = null!;

    private CancellationTokenSource? syncBansTokenSource = null!;
    private CancellationTokenSource? syncSanctionsTokenSource = null!;

    public Admins(ISwiftlyCore core) : base(core)
    {
        SwiftlyCore = core;
        var connection = core.Database.GetConnection("admins");

        MigrationRunner.RunMigrations(connection);
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IAdminAPIv1, AdminAPIv1>("Admins.API.v1", AdminAPI);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<AdminsConfig>("config.jsonc", "Main")
            .Configure(builder =>
            {
                builder.AddJsonFile("config.jsonc", false, true);
            });

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

        ServiceCollection services = new();
        services.AddSwiftly(Core).AddOptions<AdminsConfig>().BindConfiguration("Main");

        var provider = services.BuildServiceProvider();
        Config = provider.GetRequiredService<IOptionsMonitor<AdminsConfig>>();

        global::Admins.Groups.Groups.Load();
        ServerBans.Load(null);
        ServerSanctions.Load(null);

        Core.Scheduler.RepeatBySeconds(10.0f, ServerSanctions.ScheduleCheck);

        syncBansTokenSource = Core.Scheduler.RepeatBySeconds(Config.CurrentValue.SyncIntervalInSeconds, ServerBans.DatabaseFetch);
        syncSanctionsTokenSource = Core.Scheduler.RepeatBySeconds(Config.CurrentValue.SyncIntervalInSeconds, ServerSanctions.DatabaseFetch);
        Config.OnChange(config =>
        {
            syncBansTokenSource?.Cancel();
            syncBansTokenSource = Core.Scheduler.RepeatBySeconds(config.SyncIntervalInSeconds, ServerBans.DatabaseFetch);

            syncSanctionsTokenSource?.Cancel();
            syncSanctionsTokenSource = Core.Scheduler.RepeatBySeconds(config.SyncIntervalInSeconds, ServerSanctions.DatabaseFetch);
        });

        ServerSanctions.RegisterAdminSubmenu();
        adminCommands.Init();
    }

    public override void Unload()
    {
    }

    [EventListener<EventDelegates.OnSteamAPIActivated>]
    public void OnSteamAPIActivated()
    {
        Task.Run(() =>
        {
            var serverIp = Core.Engine.ServerIP;
            var hostport = Core.ConVar.Find<int>("hostport");

            if (hostport == null || serverIp == null) return;
            if (!Config.CurrentValue.UseDatabase) return;

            var database = Core.Database.GetConnection("admins");
            var existingServer = database.Count<Server>(s => s.GUID == ServerGUID);
            if (existingServer == 0)
            {
                var server = new Server
                {
                    IP = serverIp,
                    Port = hostport.Value,
                    GUID = ServerGUID
                };
                database.Insert(server);
            }
        });
    }

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        int playerid = @event.PlayerId;

        var player = Core.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        if (!ServerBans.CheckPlayer(player)) return;

        Task.Run(() =>
        {
            var admin = AdminAPI.GetAdmin(playerid);
            if (admin == null) return;

            global::Admins.ServerAdmins.ServerAdmins.AssignAdmin(player, (Admin)admin);
        });
    }

    [ClientChatHookHandler]
    public HookResult OnClientChat(int playerId, string text, bool teamOnly)
    {
        var player = Core.PlayerManager.GetPlayer(playerId);
        if (player == null || player.IsFakeClient) return HookResult.Continue;

        if (teamOnly && text.StartsWith('@') && Config.CurrentValue.EnableAdminChat)
        {
            bool isAdmin = Core.Permission.PlayerHasPermission(player.SteamID, "admins.chat");
            var players = Core.PlayerManager.GetAllPlayers().Where(p => Core.Permission.PlayerHasPermission(p.SteamID, "admins.chat"));
            if (!players.Contains(player)) players = players.Append(player);

            foreach (var p in players)
            {
                var playerLocalizer = Core.Translation.GetPlayerLocalizer(p);
                p.SendChat(playerLocalizer[isAdmin ? "chat.admin_chat_format" : "chat.player_chat_format", player.Controller.PlayerName, text[1..]]);
            }
            return HookResult.Stop;
        }

        if (ServerSanctions.IsPlayerGagged(player, out var sanction))
        {
            var playerLocalizer = Core.Translation.GetPlayerLocalizer(player);
            player.SendChat(playerLocalizer[
                "gag.message",
                Config.CurrentValue.Prefix,
                sanction!.AdminName,
                sanction!.ExpiresAt == 0 ? playerLocalizer["never"] : DateTimeOffset.FromUnixTimeMilliseconds((long)sanction.ExpiresAt).ToString("yyyy-MM-dd HH:mm:ss"),
                sanction.Reason
            ]);
            return HookResult.Stop;
        }

        return HookResult.Continue;
    }
}