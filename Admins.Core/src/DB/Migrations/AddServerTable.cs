using FluentMigrator;

namespace Admins.Core.Database.Migrations;

[Migration(20270214103911)]
public class Admins_AddServerTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("admins-servers").Exists())
        {
            Create.Table("admins-servers")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("IP").AsString(45).NotNullable()
            .WithColumn("Port").AsInt32().NotNullable()
            .WithColumn("Hostname").AsString(255).NotNullable()
            .WithColumn("GUID").AsString(255).NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("admins-servers").Exists())
        {
            Delete.Table("admins-servers");
        }
    }
}