using FluentMigrator;

namespace Admins.Bans.Database.Migrations;

[Migration(1762800000)]
public class AddCreatedAtColumnToBans : Migration
{
    public override void Up()
    {
        Alter.Table("Bans")
            .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("CreatedAt").FromTable("Bans");
    }
}
