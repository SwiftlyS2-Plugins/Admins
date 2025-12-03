using System.Data;
using System.Data.SQLite;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Npgsql;

namespace Admins.Database;

public class MigrationRunner
{
    public static void RunMigrations(IDbConnection dbConnection)
    {
        var serviceProvider = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner((rb) =>
            {
                ConfigureDatabase(rb, dbConnection);
                rb.WithGlobalConnectionString(dbConnection.ConnectionString).ScanIn(typeof(MigrationRunner).Assembly).For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(false);

        using (var scope = serviceProvider.CreateScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();
        }
    }

    private static void ConfigureDatabase(IMigrationRunnerBuilder rb, IDbConnection dbConnection)
    {
        switch (dbConnection)
        {
            case MySqlConnection:
                rb.AddMySql5();
                break;
            case NpgsqlConnection:
                rb.AddPostgres();
                break;
            case SQLiteConnection:
                rb.AddSQLite();
                break;
            default:
                throw new NotSupportedException($"Unsupported database connection type: {dbConnection.GetType().Name}");
        }

        rb.WithGlobalConnectionString(dbConnection.ConnectionString);
    }
}