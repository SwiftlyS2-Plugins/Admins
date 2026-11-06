using System.Data;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;

namespace Admins.Database;

internal static class MigrationRunner
{
    public static void Run(ISwiftlyCore core)
    {
        // Must stay unopened: an opened MySqlConnection drops the password from ConnectionString.
        using var connection = core.Database.GetConnection(AdminsDatabase.ConnectionName);
        var connectionString = connection.ConnectionString;
        var driver = ResolveDriver(core, connection);

        var services = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb =>
            {
                ConfigureEngine(rb, driver);
                rb.WithGlobalConnectionString(connectionString)
                  .ScanIn(typeof(MigrationRunner).Assembly).For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        // Disposing releases the runner's own connection; on SQLite it would otherwise hold the file open across hot reloads.
        using var provider = services.BuildServiceProvider(false);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }

    // Dispatches on the configured driver name, not on the connection's CLR type: the plugin ships its own
    // copies of the ADO drivers, so a type check against them can miss the host's connection type.
    private static string ResolveDriver(ISwiftlyCore core, IDbConnection connection)
    {
        try
        {
            var driver = core.Database.GetConnectionInfo(AdminsDatabase.ConnectionName).Driver;
            if (!string.IsNullOrWhiteSpace(driver)) return driver.Trim().ToLowerInvariant();
        }
        catch (Exception)
        {
        }

        return connection.GetType().Name switch
        {
            "MySqlConnection" => "mysql",
            "NpgsqlConnection" => "postgresql",
            "SQLiteConnection" or "SqliteConnection" => "sqlite",
            var name => name,
        };
    }

    private static void ConfigureEngine(IMigrationRunnerBuilder rb, string driver)
    {
        switch (driver)
        {
            case "mysql" or "mariadb":
                rb.AddMySql5();
                break;
            case "postgresql" or "postgres" or "pgsql":
                rb.AddPostgres();
                break;
            case "sqlite":
                rb.AddSQLite();
                break;
            default:
                throw new NotSupportedException($"Database driver '{driver}' is not supported for the '{AdminsDatabase.ConnectionName}' connection. Use mysql, postgresql or sqlite.");
        }
    }
}
