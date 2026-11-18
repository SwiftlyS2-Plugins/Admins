using System.Data;
using Admins.Contract;
using Admins.Database.Models;
using Dommel;
using ServerRow = Admins.Database.Models.Server;

namespace Admins.Services.Sessions;

/// <summary>A player's sessions on every server sharing the database, as their Sessions page in the menu shows them.</summary>
/// <param name="Sessions">Newest first.</param>
public sealed record AccountSessions(
    IReadOnlyList<AdminPlaytimeRow> Playtime, IReadOnlyList<AdminSessionIpRow> Ips, IReadOnlyList<AdminSessionRow> Sessions);

public sealed partial class SessionsService
{
    /// <summary>
    /// The name <paramref name="steamId"/> last connected with, to any server, or null. Read for a punishment issued by
    /// SteamID alone, so a failed read leaves the name unknown instead of failing the punishment.
    /// </summary>
    public static async Task<string?> LastNameAsync(IDbConnection db, ulong steamId)
    {
        var id = (long)steamId;

        try
        {
            var rows = await db.FromAsync<PlayerSession>(q => q.Select().Where(s => s.SteamId64 == id).OrderByDescending(s => s.ConnectedAt).Page(1, 1)).ConfigureAwait(false);
            return rows.FirstOrDefault()?.PlayerName is { Length: > 0 } name ? name : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Off the game thread.</summary>
    public async Task<AccountSessions> LoadAccountAsync(ulong steamId)
    {
        var id = (long)steamId;
        List<PlayerSession> rows;
        var hostnames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using (var db = _database.Connect())
        {
            rows = [.. (await db.SelectAsync<PlayerSession>(s => s.SteamId64 == id).ConfigureAwait(false)).OrderByDescending(s => s.ConnectedAt)];
            foreach (var server in await db.GetAllAsync<ServerRow>().ConfigureAwait(false)) hostnames.TryAdd(server.GUID, server.Hostname);
        }

        var today = _messenger.Now().Date;
        var periods = new (AdminPlaytimePeriod Period, long From)[]
        {
            (AdminPlaytimePeriod.Today, _messenger.UnixMs(today)),
            (AdminPlaytimePeriod.Week, _messenger.UnixMs(today.AddDays(-(((int)today.DayOfWeek + 6) % 7)))),
            (AdminPlaytimePeriod.Month, _messenger.UnixMs(new DateTime(today.Year, today.Month, 1))),
            (AdminPlaytimePeriod.Year, _messenger.UnixMs(new DateTime(today.Year, 1, 1))),
            (AdminPlaytimePeriod.Total, 0),
        };

        var guid = _server.Guid;
        return new AccountSessions(
            [.. periods.Select(p => new AdminPlaytimeRow(p.Period, TimeSpan.FromMilliseconds(rows.Sum(r => Math.Max(0, End(r) - Math.Max(r.ConnectedAt, p.From))))))],
            [
                .. rows.Where(r => r.PlayerIp.Length > 0)
                    .GroupBy(r => r.PlayerIp)
                    .Select(g => new AdminSessionIpRow(g.Key, g.Count(), DateTimeOffset.FromUnixTimeMilliseconds(g.Max(End))))
                    .OrderByDescending(ip => ip.Sessions)
                    .ThenByDescending(ip => ip.LastSeen),
            ],
            [
                .. rows.Select(r => new AdminSessionRow(
                    r.Id,
                    r.PlayerIp,
                    string.Equals(r.Server, guid, StringComparison.OrdinalIgnoreCase) ? null : hostnames.GetValueOrDefault(r.Server, string.Empty),
                    DateTimeOffset.FromUnixTimeMilliseconds(r.ConnectedAt),
                    TimeSpan.FromMilliseconds(Played(r)),
                    IsOnline(r))),
            ]);
    }
}
