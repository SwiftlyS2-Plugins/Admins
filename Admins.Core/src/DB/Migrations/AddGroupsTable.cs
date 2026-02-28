using FluentMigrator;

namespace Admins.Core.Database.Migrations;

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