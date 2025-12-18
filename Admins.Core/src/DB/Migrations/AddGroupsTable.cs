using FluentMigrator;

namespace Admins.Core.Database.Migrations;

[Migration(1762730149)]
public class AddGroupsTable : Migration
{
    public override void Up()
    {
        Create.Table("Groups")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Permissions").AsString(16384).NotNullable()
            .WithColumn("Servers").AsString(16384).NotNullable()
            .WithColumn("Immunity").AsInt32().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Groups");
    }
}