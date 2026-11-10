using FluentMigrator;

namespace Admins.Database.Migrations;

// Migration ids and table shapes are shared with the former Admins.Core / Admins.Bans / Admins.Comms
// plugins and recorded in the same VersionInfo ledger: existing databases skip them, new ones get the
// identical schema. Never renumber or edit them; add new migrations instead.

[Migration(20270214103911)]
public class Admins_AddServerTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("servers").Exists())
        {
            Create.Table("servers")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("IP").AsString(45).NotNullable()
            .WithColumn("Port").AsInt32().NotNullable()
            .WithColumn("Hostname").AsString(255).NotNullable()
            .WithColumn("GUID").AsString(255).NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("servers").Exists())
        {
            Delete.Table("servers");
        }
    }
}

[Migration(20270214103912)]
public class Admins_AddGroupsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("groups").Exists())
        {
            Create.Table("groups")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Permissions").AsString(16384).NotNullable()
            .WithColumn("Servers").AsString(16384).NotNullable()
            .WithColumn("Immunity").AsInt32().NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("groups").Exists())
        {
            Delete.Table("groups");
        }
    }
}

[Migration(20270214103913)]
public class Admins_AddAdminsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("admins").Exists())
        {
            Create.Table("admins")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity().NotNullable()
            .WithColumn("SteamId64").AsInt64().Unique().NotNullable()
            .WithColumn("Username").AsString().Unique().NotNullable()
            .WithColumn("Permissions").AsString(16384).NotNullable()
            .WithColumn("Groups").AsString(16384).NotNullable()
            .WithColumn("Immunity").AsInt32().NotNullable()
            .WithColumn("Servers").AsString(16384).NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("admins").Exists())
        {
            Delete.Table("admins");
        }
    }
}

[Migration(20270214103914)]
public class Admins_AddBansTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("bans").Exists())
        {
            Create.Table("bans")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("SteamId64").AsInt64().NotNullable()
            .WithColumn("PlayerName").AsString().NotNullable()
            .WithColumn("PlayerIp").AsString().NotNullable()
            .WithColumn("BanType").AsInt32().NotNullable()
            .WithColumn("ExpiresAt").AsInt64().NotNullable()
            .WithColumn("Length").AsInt64().NotNullable()
            .WithColumn("Reason").AsString().NotNullable()
            .WithColumn("AdminSteamId64").AsInt64().NotNullable()
            .WithColumn("AdminName").AsString().NotNullable()
            .WithColumn("Server").AsString().NotNullable()
            .WithColumn("GlobalBan").AsBoolean().NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("bans").Exists())
        {
            Delete.Table("bans");
        }
    }
}

[Migration(20270214103915)]
public class Admins_AddCreatedAtColumnToBans : Migration
{
    public override void Up()
    {
        if (Schema.Table("bans").Exists() && !Schema.Table("bans").Column("CreatedAt").Exists())
        {
            Alter.Table("bans")
                .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("bans").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("bans");
        }
    }
}

[Migration(20270214103916)]
public class Admins_AddUpdatedAtColumnToBans : Migration
{
    public override void Up()
    {
        if (Schema.Table("bans").Exists() && !Schema.Table("bans").Column("UpdatedAt").Exists())
        {
            Alter.Table("bans")
                .AddColumn("UpdatedAt").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("bans").Column("UpdatedAt").Exists())
        {
            Delete.Column("UpdatedAt").FromTable("bans");
        }
    }
}

[Migration(20270214103917)]
public class Admins_AddSanctionsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("sanctions").Exists())
        {
            Create.Table("sanctions")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("SteamId64").AsInt64().NotNullable()
            .WithColumn("PlayerName").AsString().NotNullable()
            .WithColumn("PlayerIp").AsString().NotNullable()
            .WithColumn("SanctionType").AsInt32().NotNullable()
            .WithColumn("SanctionKind").AsInt32().NotNullable()
            .WithColumn("ExpiresAt").AsInt64().NotNullable()
            .WithColumn("Length").AsInt64().NotNullable()
            .WithColumn("Reason").AsString().NotNullable()
            .WithColumn("AdminSteamId64").AsInt64().NotNullable()
            .WithColumn("AdminName").AsString().NotNullable()
            .WithColumn("Server").AsString().NotNullable()
            .WithColumn("GlobalSanction").AsBoolean().NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("sanctions").Exists())
        {
            Delete.Table("sanctions");
        }
    }
}

[Migration(20270214103918)]
public class Admins_AddTimeColumnToSanctions : Migration
{
    public override void Up()
    {
        if (Schema.Table("sanctions").Exists())
        {
            if (!Schema.Table("sanctions").Column("UpdatedAt").Exists())
            {
                Alter.Table("sanctions")
                    .AddColumn("UpdatedAt").AsInt64().NotNullable().WithDefaultValue(0);
            }
            if (!Schema.Table("sanctions").Column("CreatedAt").Exists())
            {
                Alter.Table("sanctions")
                    .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
            }
        }
    }

    public override void Down()
    {
        if (Schema.Table("sanctions").Column("UpdatedAt").Exists())
        {
            Delete.Column("UpdatedAt").FromTable("sanctions");
        }
        if (Schema.Table("sanctions").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("sanctions");
        }
    }
}

// Not "sessions": web frameworks that share the database often own a table by that name.
[Migration(20270215000000)]
public class Admins_AddPlayerSessionsTable : Migration
{
    private const string Table = "player_sessions";

    public override void Up()
    {
        if (!Schema.Table(Table).Exists())
        {
            Create.Table(Table)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("SteamId64").AsInt64().NotNullable()
            .WithColumn("PlayerName").AsString(128).NotNullable()
            .WithColumn("PlayerIp").AsString(64).NotNullable()
            .WithColumn("Server").AsString(64).NotNullable()
            .WithColumn("ConnectedAt").AsInt64().NotNullable()
            .WithColumn("LastSeenAt").AsInt64().NotNullable()
            .WithColumn("DisconnectedAt").AsInt64().NotNullable().WithDefaultValue(0);
        }

        if (!Schema.Table(Table).Index("IX_player_sessions_SteamId64").Exists())
        {
            Create.Index("IX_player_sessions_SteamId64").OnTable(Table).OnColumn("SteamId64").Ascending();
        }

        if (!Schema.Table(Table).Index("IX_player_sessions_PlayerIp").Exists())
        {
            Create.Index("IX_player_sessions_PlayerIp").OnTable(Table).OnColumn("PlayerIp").Ascending();
        }

        if (!Schema.Table(Table).Index("IX_player_sessions_Server_ConnectedAt").Exists())
        {
            Create.Index("IX_player_sessions_Server_ConnectedAt").OnTable(Table).OnColumn("Server").Ascending().OnColumn("ConnectedAt").Ascending();
        }

        if (!Schema.Table(Table).Index("IX_player_sessions_Server_DisconnectedAt").Exists())
        {
            Create.Index("IX_player_sessions_Server_DisconnectedAt").OnTable(Table).OnColumn("Server").Ascending().OnColumn("DisconnectedAt").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Table(Table).Exists())
        {
            Delete.Table(Table);
        }
    }
}

// Lifting used to only end a punishment, so it read as expired. Rows that ended before this read Active until a sync
// marks them Expired, and earlier lifts cannot be told apart: they end up Expired too.
[Migration(20270216000000)]
public class Admins_AddStatusToPunishments : Migration
{
    private static readonly string[] Tables = ["bans", "sanctions"];

    public override void Up()
    {
        foreach (var table in Tables)
        {
            if (!Schema.Table(table).Exists()) continue;

            if (!Schema.Table(table).Column("Status").Exists())
            {
                Alter.Table(table).AddColumn("Status").AsInt32().NotNullable().WithDefaultValue(0);
            }

            if (!Schema.Table(table).Index($"IX_{table}_Status_ExpiresAt").Exists())
            {
                Create.Index($"IX_{table}_Status_ExpiresAt").OnTable(table).OnColumn("Status").Ascending().OnColumn("ExpiresAt").Ascending();
            }
        }
    }

    public override void Down()
    {
        foreach (var table in Tables)
        {
            if (Schema.Table(table).Index($"IX_{table}_Status_ExpiresAt").Exists())
            {
                Delete.Index($"IX_{table}_Status_ExpiresAt").OnTable(table);
            }

            if (Schema.Table(table).Column("Status").Exists())
            {
                Delete.Column("Status").FromTable(table);
            }
        }
    }
}

[Migration(20270216000001)]
public class Admins_AddIpBanExclusionsTable : Migration
{
    private const string Table = "ip_ban_exclusions";

    public override void Up()
    {
        if (!Schema.Table(Table).Exists())
        {
            Create.Table(Table)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("SteamId64").AsInt64().NotNullable().Unique()
            .WithColumn("PlayerName").AsString(128).NotNullable()
            .WithColumn("AdminSteamId64").AsInt64().NotNullable()
            .WithColumn("AdminName").AsString(128).NotNullable()
            .WithColumn("CreatedAt").AsInt64().NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table(Table).Exists())
        {
            Delete.Table(Table);
        }
    }
}

// Every enforcement query finds punishments by SteamID or address; without these each one scans the whole table.
[Migration(20270217000000)]
public class Admins_IndexPunishmentLookups : Migration
{
    private static readonly string[] Tables = ["bans", "sanctions"];
    private static readonly string[] Columns = ["SteamId64", "PlayerIp"];

    public override void Up()
    {
        foreach (var table in Tables)
        {
            if (!Schema.Table(table).Exists()) continue;

            foreach (var column in Columns)
            {
                if (!Schema.Table(table).Index($"IX_{table}_{column}").Exists())
                {
                    Create.Index($"IX_{table}_{column}").OnTable(table).OnColumn(column).Ascending();
                }
            }
        }
    }

    public override void Down()
    {
        foreach (var table in Tables)
        {
            foreach (var column in Columns)
            {
                if (Schema.Table(table).Index($"IX_{table}_{column}").Exists())
                {
                    Delete.Index($"IX_{table}_{column}").OnTable(table);
                }
            }
        }
    }
}

// Refreshed every minute while a server runs: panels see which servers are up, and a server without its server_id.txt
// does not take over the record of one still running at the same address.
[Migration(20270218000000)]
public class Admins_AddLastSeenAtToServers : Migration
{
    public override void Up()
    {
        if (Schema.Table("servers").Exists() && !Schema.Table("servers").Column("LastSeenAt").Exists())
        {
            Alter.Table("servers").AddColumn("LastSeenAt").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("servers").Column("LastSeenAt").Exists())
        {
            Delete.Column("LastSeenAt").FromTable("servers");
        }
    }
}
