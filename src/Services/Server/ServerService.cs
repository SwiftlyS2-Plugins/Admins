using Admins.Contract;
using Admins.Database;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using ServerRow = Admins.Database.Models.Server;

namespace Admins.Services.Server;

public sealed class ServerService : IServerManager
{
    private const string GuidFileName = "server_id.txt";

    private readonly ISwiftlyCore _core;
    private readonly ILogger<ServerService> _logger;
    private readonly AdminsDatabase _database;
    private volatile string _guid = string.Empty;

    public ServerService(ISwiftlyCore core, ILogger<ServerService> logger, AdminsDatabase database)
    {
        _core = core;
        _logger = logger;
        _database = database;

        LoadGuid();
        core.Registrator.Register(this);
    }

    /// <summary>
    /// Raised on the game thread after the GUID changed.
    /// </summary>
    public event Action? GuidChanged;

    public string Guid => _guid;

    public string GetServerGUID() => _guid;

    public void SetServerGUID(string serverGUID)
    {
        var value = serverGUID?.Trim() ?? string.Empty;

        if (!System.Guid.TryParse(value, out _))
        {
            _logger.LogError("Ignoring invalid server GUID '{Guid}'.", serverGUID);
            return;
        }

        if (string.Equals(value, _guid, StringComparison.OrdinalIgnoreCase)) return;

        _guid = value;
        WriteGuid(value);

        _core.Scheduler.NextTick(() =>
        {
            GuidChanged?.Invoke();
            RegisterInDatabase();
        });
    }

    /// <summary>
    /// On a hot reload the Steam API is already up and OnSteamAPIActivated will not fire again.
    /// </summary>
    public void Start(bool hotReload)
    {
        if (hotReload) _core.Scheduler.NextTick(RegisterInDatabase);
    }

    [EventListener<EventDelegates.OnSteamAPIActivated>]
    public void OnSteamAPIActivated() => RegisterInDatabase();

    private string GuidPath => Path.Combine(_core.PluginDataDirectory, GuidFileName);

    // Admins.Core kept the GUID in its own data folder. Every admin, group and ban row is scoped by it, so a
    // fresh GUID would silently detach this server from all of them.
    private string LegacyGuidPath =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(_core.PluginDataDirectory)) ?? _core.PluginDataDirectory, "Admins.Core", GuidFileName);

    private void LoadGuid()
    {
        foreach (var (path, legacy) in new[] { (GuidPath, false), (LegacyGuidPath, true) })
        {
            if (!File.Exists(path)) continue;

            var text = File.ReadAllText(path).Trim();
            if (!System.Guid.TryParse(text, out _))
            {
                _logger.LogWarning("Ignoring invalid server GUID in {Path}.", path);
                continue;
            }

            _guid = text;

            if (legacy)
            {
                WriteGuid(text);
                _logger.LogInformation("Imported server GUID {Guid} from {Path}.", text, path);
            }

            return;
        }

        _guid = System.Guid.NewGuid().ToString();
        WriteGuid(_guid);
        _logger.LogWarning("Generated a new server GUID: {Guid}.", _guid);
    }

    private void WriteGuid(string guid)
    {
        try
        {
            Directory.CreateDirectory(_core.PluginDataDirectory);
            File.WriteAllText(GuidPath, guid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write the server GUID to {Path}.", GuidPath);
        }
    }

    private void RegisterInDatabase()
    {
        if (!_database.Enabled) return;

        string? ip;
        int port;
        string hostname;

        try
        {
            ip = _core.Engine.ServerIP;
            var hostport = _core.ConVar.Find<int>("hostport");
            if (string.IsNullOrEmpty(ip) || hostport is null) return;

            port = hostport.Value;
            hostname = _core.ConVar.Find<string>("hostname")?.Value ?? "Unknown Hostname";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server address not available yet; registration deferred.");
            return;
        }

        var guid = _guid;

        _ = Task.Run(async () =>
        {
            try
            {
                using var db = _database.Connect();

                if (await db.CountAsync<ServerRow>(s => s.GUID == guid) > 0) return;

                var existing = await db.FirstOrDefaultAsync<ServerRow>(s => s.IP == ip && s.Port == port);
                if (existing is null)
                {
                    await db.InsertAsync(new ServerRow { GUID = guid, IP = ip, Port = port, Hostname = hostname });
                    _logger.LogInformation("Registered server '{Hostname}' ({Ip}:{Port}) as {Guid}.", hostname, ip, port, guid);
                    return;
                }

                _logger.LogWarning("Server {Ip}:{Port} is already registered as {Guid}; adopting that GUID.", ip, port, existing.GUID);
                _core.Scheduler.NextTick(() => SetServerGUID(existing.GUID));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register the server in the database.");
            }
        });
    }
}
