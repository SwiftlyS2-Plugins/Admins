using Admins.Core.Config;
using Dommel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;

namespace Admins.Core.Server;

public partial class ServerLoader
{
    private ISwiftlyCore Core = null!;

    public static string ServerGUID { get; set; } = string.Empty;
    private readonly IOptionsMonitor<CoreConfiguration>? _config;
    private bool _initialized = false;

    public ServerLoader(IOptionsMonitor<CoreConfiguration> config, ISwiftlyCore core)
    {
        core.Registrator.Register(this);
        _config = config;
        Core = core;

        Load();
    }

    public void Load()
    {
        if (_config!.CurrentValue.UseDatabase == false)
        {
            Core.Logger.LogInformation("Admins Core is configured to not use a database. Skipping server GUID setup.");
            return;
        }

        var guidPath = Path.Combine(Core.PluginDataDirectory, "server_id.txt");

        if (!File.Exists(guidPath))
        {
            ServerGUID = Guid.NewGuid().ToString();
            File.WriteAllText(guidPath, ServerGUID);
            Core.Logger.LogWarning("Generated new Server GUID: {Guid}", ServerGUID);
        }
        else
        {
            ServerGUID = File.ReadAllText(guidPath).Trim();
            Core.Logger.LogDebug("Identified Server GUID: {Guid}", ServerGUID);
        }

        if (!Guid.TryParse(ServerGUID, out _))
        {
            SetServerGUID(Guid.NewGuid().ToString());
            Core.Logger.LogWarning("Invalid Server GUID detected. Generated new GUID: {Guid}", ServerGUID);
        }
    }

    public void SetServerGUID(string guid)
    {
        if (!Guid.TryParse(guid, out _))
        {
            Core.Logger.LogError("Invalid Server GUID detected. {Guid}", guid);
            return;
        }

        var guidPath = Path.Combine(Core.PluginDataDirectory, "server_id.txt");

        ServerGUID = guid;
        File.WriteAllText(guidPath, ServerGUID);

        if (_initialized)
        {
            UpdateServerGUIDInDatabase();
        }
    }

    public void UpdateServerGUIDInDatabase()
    {
        if (_config!.CurrentValue.UseDatabase == false) return;

        Task.Run(async () =>
        {
            try
            {
                var serverIp = Core.Engine.ServerIP;
                var hostport = Core.ConVar.Find<int>("hostport");

                if (hostport == null || string.IsNullOrEmpty(serverIp))
                {
                    Core.Logger.LogError("Failed to register server in Admins Core: Missing hostport or server IP.");
                    return;
                }

                using var db = Core.Database.GetConnection("admins");
                var existingServer = await db.CountAsync<Database.Models.Server>(s => s.GUID == ServerGUID);

                if (existingServer == 0)
                {
                    var server = new Database.Models.Server
                    {
                        GUID = ServerGUID,
                        IP = serverIp,
                        Port = (ushort)hostport.Value,
                        Hostname = Core.ConVar.Find<string>("hostname")?.Value ?? "Unknown Hostname",
                    };

                    await db.InsertAsync(server);
                    Core.Logger.LogInformation("Registered server '{ServerName}' ({IP}:{Port}) in Admins DB.", server.Hostname, server.IP, server.Port);
                }
            }
            catch (Exception e)
            {
                Core.Logger.LogError(e, "An error occurred while registering the server in Admins Core.");
            }
        });
    }

    [EventListener<EventDelegates.OnSteamAPIActivated>]
    public void OnSteamAPIActivated()
    {
        UpdateServerGUIDInDatabase();
        _initialized = true;
    }
}