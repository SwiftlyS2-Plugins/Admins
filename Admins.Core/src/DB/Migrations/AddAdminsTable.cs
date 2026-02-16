using FluentMigrator;

namespace Admins.Core.Database.Migrations;

[Migration(20270214103913)]
public class Admins_AddAdminsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("admins-admins").Exists())
        {
            Create.Table("admins-admins")
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
        if (Schema.Table("admins-admins").Exists())
        {
            Delete.Table("admins-admins");
        }
    }
}