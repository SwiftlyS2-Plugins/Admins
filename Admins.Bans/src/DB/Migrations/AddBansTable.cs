using FluentMigrator;

namespace Admins.Bans.Database.Migrations;

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