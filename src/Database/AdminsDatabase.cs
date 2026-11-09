using System.Data;
using Admins.Contract;
using Admins.Models;
using Dapper;
using Dommel;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;

namespace Admins.Database;

public sealed class AdminsDatabase(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
{
    public const string ConnectionName = "admins";

    public bool Enabled => config.CurrentValue.UseDatabase;

    public IDbConnection Connect() => core.Database.GetConnection(ConnectionName);

    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static bool StatusIs(PunishmentStatus status, RecordStatus wanted) => wanted switch
    {
        RecordStatus.Active => status == PunishmentStatus.Active,
        RecordStatus.Expired => status == PunishmentStatus.Expired,
        RecordStatus.Lifted => status == PunishmentStatus.Lifted,
        _ => true,
    };

    /// <summary>
    /// Marks the rows of <typeparamref name="T"/> (bans or sanctions) that ran out Expired. The plugin goes by ExpiresAt;
    /// this keeps the Status column true for whatever else reads the table.
    /// </summary>
    public static Task<int> MarkExpiredAsync<T>(IDbConnection db, long nowMs)
    {
        var builder = DommelMapper.GetSqlBuilder(db);
        var table = Resolvers.Table(typeof(T), builder);
        var status = Resolvers.Column(typeof(T).GetProperty("StoredStatus")!, builder, includeTableName: false);
        var expires = Resolvers.Column(typeof(T).GetProperty("ExpiresAt")!, builder, includeTableName: false);

        return db.ExecuteAsync(
            $"UPDATE {table} SET {status} = {builder.PrefixParameter("Expired")} "
            + $"WHERE {status} = {builder.PrefixParameter("Active")} AND {expires} <> 0 AND {expires} <= {builder.PrefixParameter("Now")}",
            new { Expired = (int)PunishmentStatus.Expired, Active = (int)PunishmentStatus.Active, Now = nowMs });
    }
}
