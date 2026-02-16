using FluentMigrator;

namespace Admins.Comms.Database.Migrations;

[Migration(20270214103917)]
public class Admins_AddSanctionsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("admins-sanctions").Exists())
        {
            Create.Table("admins-sanctions")
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
        if (Schema.Table("admins-sanctions").Exists())
        {
            Delete.Table("admins-sanctions");
        }
    }
}