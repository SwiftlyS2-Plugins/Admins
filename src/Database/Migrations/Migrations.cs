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
