using System.Data;
using Admins.Database;
using Admins.Database.Models;
using Admins.Shared;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;
using ServerRow = Admins.Database.Models.Server;

namespace Admins.Services.Sessions;

public sealed partial class SessionsService
{
    private const int Shown = 10;

    // Older than this, an open session is one a crash left behind rather than someone still playing.
    private const long OnlineWithinMs = (long)(HeartbeatSeconds * 2.5f * 1000);

    // A name is looked for in this many of the newest sessions; a SteamID or IP finds all of them.
    private const int NameSearchRows = 5000;

    // A player with dozens of addresses is on a dynamic IP; their newest are what other accounts would share.
    private const int SharedIpLimit = 50;

    [Command("sessions", permission: "admins.commands.sessions")]
    public void SessionsCommand(ICommandContext context)
    {
        var issuer = Issuer.From(context);

        if (!_database.Enabled)
        {
            _messenger.Reply(issuer, "command.database_disabled");
            return;
        }

        var query = string.Join(' ', context.Args).Trim();

        if (query.Length == 0)
        {
            var guid = _server.Guid;
            Lookup(issuer, "this server", db => db.FromAsync<PlayerSession>(q => q.Select().Where(s => s.Server == guid).OrderByDescending(s => s.Id).Page(1, Shown)), rows => PrintLatest(issuer, [.. rows]));
            return;
        }

        if (Net.IsIp(query))
        {
            var ip = Net.NormalizeIp(query);
            Lookup(issuer, ip, db => db.SelectAsync<PlayerSession>(s => s.PlayerIp == ip), rows => PrintIp(issuer, ip, [.. rows]));
            return;
        }

        var players = _targets.Find(issuer.Player, query, out var error);

        if (players.Count > 1 || error == TargetError.Ambiguous)
        {
            _messenger.Reply(issuer, "command.multiple_targets", query);
            return;
        }

        if (players.Count == 1)
        {
            if (players[0] is { IsFakeClient: false, SteamID: not 0 } player) ShowAccount(issuer, player.SteamID);
            else _messenger.Reply(issuer, "sessions.none", query);
            return;
        }

        if (SteamIds.TryParse(query, out var steamId))
        {
            ShowAccount(issuer, steamId);
            return;
        }

        SearchName(issuer, query);
    }

    private void ShowAccount(Issuer issuer, ulong steamId)
    {
        var id = (long)steamId;

        Lookup(issuer, steamId.ToString(), async db =>
        {
            var rows = (await db.SelectAsync<PlayerSession>(s => s.SteamId64 == id).ConfigureAwait(false)).OrderByDescending(s => s.ConnectedAt).ToList();
            var ips = rows.Select(s => s.PlayerIp).Where(ip => ip.Length > 0).Distinct().Take(SharedIpLimit).ToList();
            var shared = ips.Count == 0 ? [] : (await db.SelectAsync<PlayerSession>(s => ips.Contains(s.PlayerIp) && s.SteamId64 != id).ConfigureAwait(false)).ToList();
            var servers = await db.GetAllAsync<ServerRow>().ConfigureAwait(false);
            return (Rows: rows, Shared: shared, Servers: servers.ToList());
        }, data => PrintAccount(issuer, steamId, data.Rows, data.Shared, data.Servers));
    }

    // Partial and case-blind like the player targets, over every name an account has used; a full name wins.
    private void SearchName(Issuer issuer, string query)
    {
        var lowered = query.ToLowerInvariant();

        Lookup(issuer, query, db => db.FromAsync<PlayerSession>(q => q.Select().OrderByDescending(s => s.Id).Page(1, NameSearchRows)), rows =>
        {
            var named = rows.Where(s => s.PlayerName.ToLowerInvariant().Contains(lowered, StringComparison.Ordinal)).ToList();
            var exact = named.Where(s => s.PlayerName.ToLowerInvariant() == lowered).ToList();
            var accounts = (exact.Count > 0 ? exact : named).GroupBy(s => s.SteamId64).Select(g => g.First()).ToList();

            switch (accounts.Count)
            {
                case 0:
                    _messenger.Reply(issuer, "sessions.none", query);
                    return;
                case 1:
                    ShowAccount(issuer, (ulong)accounts[0].SteamId64);
                    return;
            }

            _messenger.Reply(issuer, "sessions.matches", query, accounts.Count);
            foreach (var account in accounts.Take(Shown))
            {
                _messenger.Reply(issuer, "sessions.match_row", account.PlayerName, account.SteamId64, When(issuer, End(account)));
            }

            if (accounts.Count > Shown) _messenger.Reply(issuer, "sessions.more", Shown);
        });
    }

    private void PrintLatest(Issuer issuer, IReadOnlyList<PlayerSession> rows)
    {
        if (rows.Count == 0)
        {
            _messenger.Reply(issuer, "sessions.none_here");
            return;
        }

        _messenger.Reply(issuer, "sessions.latest");
        foreach (var row in rows)
        {
            _messenger.Reply(issuer, "sessions.row", When(issuer, row.ConnectedAt), Length(issuer, row), row.PlayerName, row.SteamId64, row.PlayerIp);
        }
    }

    private void PrintAccount(Issuer issuer, ulong steamId, IReadOnlyList<PlayerSession> rows, IReadOnlyList<PlayerSession> shared, IReadOnlyList<ServerRow> servers)
    {
        if (rows.Count == 0)
        {
            _messenger.Reply(issuer, "sessions.none", steamId);
            return;
        }

        var played = TimeSpan.FromMilliseconds(rows.Sum(Played));
        _messenger.Reply(issuer, "sessions.account", rows[0].PlayerName, steamId, rows.Count, Durations.Format(played), When(issuer, rows.Min(r => r.ConnectedAt)));

        var hostnames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var server in servers) hostnames.TryAdd(server.GUID, server.Hostname);

        var guid = _server.Guid;
        foreach (var row in rows.Take(Shown))
        {
            if (string.Equals(row.Server, guid, StringComparison.OrdinalIgnoreCase))
            {
                _messenger.Reply(issuer, "sessions.account_row", When(issuer, row.ConnectedAt), Length(issuer, row), row.PlayerIp);
            }
            else
            {
                var hostname = hostnames.GetValueOrDefault(row.Server) ?? _messenger.Text(issuer.Player, "sessions.unknown_server");
                _messenger.Reply(issuer, "sessions.account_row_server", When(issuer, row.ConnectedAt), Length(issuer, row), row.PlayerIp, hostname);
            }
        }

        if (rows.Count > Shown) _messenger.Reply(issuer, "sessions.more", Shown);

        var others = shared.OrderByDescending(End).GroupBy(s => s.SteamId64).Select(g => g.First()).ToList();
        if (others.Count == 0) return;

        var names = string.Join(", ", others.Take(Shown).Select(s => $"{s.PlayerName} ({s.SteamId64})"));
        _messenger.Reply(issuer, "sessions.shared", others.Count, names);
    }

    private void PrintIp(Issuer issuer, string ip, IReadOnlyList<PlayerSession> rows)
    {
        if (rows.Count == 0)
        {
            _messenger.Reply(issuer, "sessions.none", ip);
            return;
        }

        var accounts = rows.GroupBy(s => s.SteamId64)
            .Select(g => (Latest: g.OrderByDescending(End).First(), Count: g.Count()))
            .OrderByDescending(a => End(a.Latest))
            .ToList();

        _messenger.Reply(issuer, "sessions.ip", ip, accounts.Count);
        foreach (var (latest, count) in accounts.Take(Shown))
        {
            _messenger.Reply(issuer, "sessions.ip_row", latest.PlayerName, latest.SteamId64, count, When(issuer, End(latest)));
        }

        if (accounts.Count > Shown) _messenger.Reply(issuer, "sessions.more", Shown);
    }

    private static bool IsOnline(PlayerSession row) => row.DisconnectedAt == 0 && AdminsDatabase.NowMs - row.LastSeenAt <= OnlineWithinMs;

    private static long End(PlayerSession row) =>
        row.DisconnectedAt != 0 ? row.DisconnectedAt : IsOnline(row) ? AdminsDatabase.NowMs : row.LastSeenAt;

    private static long Played(PlayerSession row) => Math.Max(0, End(row) - row.ConnectedAt);

    private string Length(Issuer issuer, PlayerSession row)
    {
        var length = Durations.Format(TimeSpan.FromMilliseconds(Played(row)));
        return IsOnline(row) ? _messenger.Text(issuer.Player, "sessions.online", length) : length;
    }

    private string When(Issuer issuer, long unixMs) => _messenger.Timestamp(issuer.Player, unixMs);

    // Loads off the game thread and prints on it, if the issuer is still there.
    private void Lookup<T>(Issuer issuer, string target, Func<IDbConnection, Task<T>> load, Action<T> print)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                T result;
                using (var db = _database.Connect())
                {
                    result = await load(db).ConfigureAwait(false);
                }

                _core.Scheduler.NextTick(() =>
                {
                    if (_messenger.IsConnected(issuer)) print(result);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Looking up the sessions of {Target} failed.", target);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }
}
