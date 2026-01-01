using FluentMigrator;

namespace Admins.Bans.Database.Migrations;

[Migration(1762760150)]
public class AddBansTable : Migration
{
    public override void Up()
    {
        Create.Table("Bans")
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

    public override void Down()
    {
        Delete.Table("Bans");
    }
}