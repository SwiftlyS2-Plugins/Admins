using System.Data;
using Admins.Contract;
using Admins.Database;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using ServerRow = Admins.Database.Models.Server;

namespace Admins.Services.Server;

public sealed class ServerService : IServerManager, IDisposable
{
    private const string GuidFileName = "server_id.txt";
    private const float HeartbeatSeconds = 60f;

    // A server that reported this recently may still be running.
    private const long RecentlySeenMs = 10 * 60_000;

    // Reports a made-up GUID waits for Steam to give the server its address before registering without one.
    private const int AddressWaits = 3;

    private readonly ISwiftlyCore _core;
    private readonly ILogger<ServerService> _logger;
    private readonly AdminsDatabase _database;
    private readonly Messenger _messenger;
    private readonly CancellationTokenSource _heartbeat;
    private volatile string _guid = string.Empty;

    // This run made the GUID up, so it may stand for one whose file was lost.
    private volatile bool _generated;

    // Set while a report runs; the fields after it are touched only by that report.
    private int _reporting;
    private Written? _written;
    private Watched? _watched;
    private int _addressWaits;
    private bool _warnedShared;

    public ServerService(ISwiftlyCore core, ILogger<ServerService> logger, AdminsDatabase database, Messenger messenger)
    {
        _core = core;
        _logger = logger;
        _database = database;
        _messenger = messenger;

        LoadGuid();
        core.Registrator.Register(this);
        _heartbeat = core.Scheduler.DelayAndRepeatGuarded(HeartbeatSeconds, HeartbeatSeconds, logger, Report);
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
        _generated = false;
        WriteGuid(value);

        _core.Scheduler.NextTick(() =>
        {
            GuidChanged?.Invoke();
            Report();
        });
    }

    /// <summary>
    /// On a hot reload the Steam API is already up and OnSteamAPIActivated will not fire again.
    /// </summary>
    public void Start(bool hotReload)
    {
        if (hotReload) _core.Scheduler.NextTick(Report);
    }

    [EventListener<EventDelegates.OnSteamAPIActivated>]
    public void OnSteamAPIActivated() => Report();

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
        _generated = true;
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

    #region Reporting

    private sealed record Address(string Ip, int Port, string Hostname);

    private sealed record Written(string Guid, string Ip, int Port);

    private sealed record Watched(long Id, long LastSeenAt, long Since);

    // Keeps this server's row in servers current, registering it the first time. Game thread.
    private void Report()
    {
        if (!_database.Enabled || ReadAddress() is not { } address) return;
        if (Interlocked.Exchange(ref _reporting, 1) == 1) return;

        var guid = _guid;
        var generated = _generated;

        _ = Task.Run(async () =>
        {
            try
            {
                using var db = _database.Connect();
                var now = AdminsDatabase.NowMs;

                if (await db.FirstOrDefaultAsync<ServerRow>(s => s.GUID == guid).ConfigureAwait(false) is { } row)
                {
                    WarnIfShared(row, guid);

                    // An address Steam has not given yet does not replace the known one.
                    if (address.Ip.Length > 0)
                    {
                        row.IP = address.Ip;
                        row.Port = address.Port;
                    }

                    row.Hostname = address.Hostname;
                    row.LastSeenAt = now;
                    await db.UpdateAsync(row).ConfigureAwait(false);
                    _written = new Written(guid, row.IP, row.Port);
                    return;
                }

                // The address is all that recognises a server whose file was lost, so a made-up GUID waits a few reports
                // for Steam to give it. One Steam never gives an address (a LAN server) then registers without.
                if (generated)
                {
                    if (address.Ip.Length == 0 && ++_addressWaits < AddressWaits) return;
                    if (address.Ip.Length > 0 && await ClaimAddressAsync(db, address, now).ConfigureAwait(false)) return;
                }

                var id = await db.InsertAsync(new ServerRow
                {
                    GUID = guid,
                    IP = address.Ip,
                    Port = address.Port,
                    Hostname = address.Hostname,
                    LastSeenAt = now,
                }).ConfigureAwait(false);

                _written = new Written(guid, address.Ip, address.Port);
                _logger.LogInformation("Registered server '{Hostname}' ({Ip}:{Port}) as #{Id}, {Guid}.", address.Hostname, address.Ip, address.Port, id, guid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report the server to the database.");
            }
            finally
            {
                Volatile.Write(ref _reporting, 0);
            }
        });
    }

    // Takes over the record last seen at this address once it has gone RecentlySeenMs without a report: a server still
    // running there keeps reporting, a stopped one (this server, before its file was lost) does not.
    // Returns false when this server should register as a new one.
    private async Task<bool> ClaimAddressAsync(IDbConnection db, Address address, long now)
    {
        var rows = await db.SelectAsync<ServerRow>(s => s.IP == address.Ip && s.Port == address.Port).ConfigureAwait(false);
        if (rows.MaxBy(s => s.LastSeenAt) is not { } previous) return false;

        if (_watched is not { } watched || watched.Id != previous.Id)
        {
            watched = new Watched(previous.Id, previous.LastSeenAt, now);
            _watched = watched;
        }
        else if (watched.LastSeenAt != previous.LastSeenAt)
        {
            _watched = null;
            _logger.LogWarning(
                "Server #{Id} '{Hostname}' at this address is still running, so this server registers as a new one. If this server had a record before its server_id.txt was lost, find it with sw_serverid and run sw_serverid use <number>.",
                previous.Id, previous.Hostname);
            return false;
        }

        // A record from before LastSeenAt existed is idle from when this server first saw it.
        var idle = now - (previous.LastSeenAt != 0 ? previous.LastSeenAt : watched.Since);
        if (idle < RecentlySeenMs)
        {
            if (watched.Since == now)
            {
                _logger.LogWarning(
                    "No server_id.txt: server #{Id} '{Hostname}' reported from this address {Ago} ago. This server takes over its record if it stays silent for {Window}, or registers as a new one if it reports again; sw_serverid use {Number} skips the wait.",
                    previous.Id, previous.Hostname, Durations.Format(TimeSpan.FromMilliseconds(Math.Max(0, idle))), Durations.Format(TimeSpan.FromMilliseconds(RecentlySeenMs)), previous.Id);
            }

            return true;
        }

        _watched = null;
        _logger.LogWarning("No server_id.txt: taking over server #{Id} '{Hostname}', last seen at {Ip}:{Port}.", previous.Id, previous.Hostname, address.Ip, address.Port);
        _core.Scheduler.NextTick(() => SetServerGUID(previous.GUID));
        return true;
    }

    // Nothing else writes this row's address between two reports, unless two servers have one server_id.txt and so
    // share every admin, ban and sanction.
    private void WarnIfShared(ServerRow row, string guid)
    {
        if (_warnedShared || _written is not { } written || written.Guid != guid) return;
        if (written.Ip == row.IP && written.Port == row.Port) return;

        _warnedShared = true;
        _logger.LogWarning(
            "Another server reports as this one ({Guid}), from {Ip}:{Port} '{Hostname}'. Servers with the same server_id.txt share admins, bans and sanctions: if one was copied from the other, delete the file on the copy and restart it.",
            guid, row.IP, row.Port, row.Hostname);
    }

    private Address? ReadAddress()
    {
        try
        {
            if (_core.ConVar.Find<int>("hostport") is not { } hostport) return null;

            var ip = _core.Engine.ServerIP;
            var hostname = _core.ConVar.Find<string>("hostname")?.Value ?? "Unknown Hostname";
            return new Address(ip is null or "0.0.0.0" ? string.Empty : ip, hostport.Value, hostname);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server address not available yet; reporting deferred.");
            return null;
        }
    }

    #endregion

    #region serverid

    [Command("serverid", permission: "admins.command.serverid")]
    public void ServerIdCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);
        var args = context.Args;

        if (!_database.Enabled)
        {
            _messenger.Reply(issuer, "command.database_disabled");
            return;
        }

        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            ListServers(issuer);
        }
        else if (args.Length > 1 && args[0].Equals("use", StringComparison.OrdinalIgnoreCase) && long.TryParse(args[1], out var id) && id > 0)
        {
            UseServer(issuer, id);
        }
        else
        {
            _messenger.Syntax(issuer, "serverid", "[list|use <number>]");
        }
    }

    private void ListServers(Issuer issuer) =>
        _ = Task.Run(async () =>
        {
            try
            {
                List<ServerRow> rows;
                using (var db = _database.Connect())
                {
                    rows = [.. await db.GetAllAsync<ServerRow>().ConfigureAwait(false)];
                }

                _core.Scheduler.NextTick(() =>
                {
                    if (!_messenger.IsConnected(issuer)) return;

                    if (rows.Count == 0)
                    {
                        _messenger.Reply(issuer, "command.serverid.list.empty");
                        return;
                    }

                    var now = AdminsDatabase.NowMs;
                    _messenger.Reply(issuer, "command.serverid.list.header", rows.Count);

                    foreach (var row in rows.OrderBy(r => r.Id))
                    {
                        var seen = string.Equals(row.GUID, _guid, StringComparison.OrdinalIgnoreCase) ? _messenger.Text(issuer.Player, "command.serverid.this")
                            : row.LastSeenAt == 0 ? _messenger.Text(issuer.Player, "command.serverid.not_seen")
                            : _messenger.Text(issuer.Player, "command.serverid.seen", Durations.Format(TimeSpan.FromMilliseconds(Math.Max(0, now - row.LastSeenAt))));

                        _messenger.Reply(issuer, "command.serverid.list.entry", row.Id, row.Hostname, $"{(row.IP.Length > 0 ? row.IP : "?")}:{row.Port}", seen, row.GUID);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Listing the servers failed.");
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });

    private void UseServer(Issuer issuer, long id) =>
        _ = Task.Run(async () =>
        {
            try
            {
                ServerRow? row;
                using (var db = _database.Connect())
                {
                    row = await db.GetAsync<ServerRow>(id).ConfigureAwait(false);
                }

                _core.Scheduler.NextTick(() =>
                {
                    if (row is null || !System.Guid.TryParse(row.GUID, out _))
                    {
                        _messenger.Reply(issuer, "command.serverid.not_found", id);
                        return;
                    }

                    if (string.Equals(row.GUID, _guid, StringComparison.OrdinalIgnoreCase))
                    {
                        _messenger.Reply(issuer, "command.serverid.use.already", id, row.Hostname);
                        return;
                    }

                    var ago = AdminsDatabase.NowMs - row.LastSeenAt;
                    if (row.LastSeenAt != 0 && ago < RecentlySeenMs)
                    {
                        _messenger.Reply(issuer, "command.serverid.use.running", id, Durations.Format(TimeSpan.FromMilliseconds(Math.Max(0, ago))));
                    }

                    _logger.LogInformation("{Issuer} gave this server the identity of server #{Id} '{Hostname}' ({Guid}); it was {Previous}.", issuer.Name, id, row.Hostname, row.GUID, _guid);
                    SetServerGUID(row.GUID);
                    _messenger.Reply(issuer, "command.serverid.use.success", id, row.Hostname);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Taking over server #{Id} failed.", id);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });

    #endregion

    public void Dispose() => _heartbeat.Cancel();
}
