using FluentMigrator;

namespace Admins.Core.Database.Migrations;

[Migration(1762730150)]
public class AddAdminsTable : Migration
{
    public override void Up()
    {
        Create.Table("Admins")
            .WithColumn("Id").AsInt64().PrimaryKey().NotNullable()
            .WithColumn("SteamId64").AsInt64().Unique().NotNullable()
            .WithColumn("Username").AsString().Unique().NotNullable()
            .WithColumn("Permissions").AsString(16384).NotNullable()
            .WithColumn("Groups").AsString(16384).NotNullable()
            .WithColumn("Immunity").AsInt32().NotNullable()
            .WithColumn("Servers").AsString(16384).NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Admins");
    }
}