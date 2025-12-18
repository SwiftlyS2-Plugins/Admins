using FluentMigrator;

namespace Admins.Core.Database.Migrations;

[Migration(1762730148)]
public class AddServerTable : Migration
{
    public override void Up()
    {
        Create.Table("Servers")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("IP").AsString(45).NotNullable()
            .WithColumn("Port").AsInt32().NotNullable()
            .WithColumn("Hostname").AsString(255).NotNullable()
            .WithColumn("GUID").AsString(255).NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Servers");
    }
}