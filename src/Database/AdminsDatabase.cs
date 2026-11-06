using System.Data;
using Admins.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;

namespace Admins.Database;

public sealed class AdminsDatabase(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
{
    public const string ConnectionName = "admins";

    public bool Enabled => config.CurrentValue.UseDatabase;

    public IDbConnection Connect() => core.Database.GetConnection(ConnectionName);

    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
