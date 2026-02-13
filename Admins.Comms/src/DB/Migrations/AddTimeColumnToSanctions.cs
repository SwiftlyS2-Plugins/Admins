using FluentMigrator;

namespace Admins.Comms.Database.Migrations;

[Migration(1768409350)]
public class AddTimeColumnToSanctions : Migration
{
    public override void Up()
    {
        Alter.Table("Sanctions")
            .AddColumn("UpdatedAt").AsInt64().NotNullable().WithDefaultValue(0)
            .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("UpdatedAt").FromTable("Sanctions");
        Delete.Column("CreatedAt").FromTable("Sanctions");
    }
}
