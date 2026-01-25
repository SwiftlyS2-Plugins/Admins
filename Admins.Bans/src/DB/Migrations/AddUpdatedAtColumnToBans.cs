using FluentMigrator;

namespace Admins.Bans.Database.Migrations;

[Migration(1768409350)]
public class AddUpdatedAtColumnToBans : Migration
{
    public override void Up()
    {
        Alter.Table("Bans")
            .AddColumn("UpdatedAt").AsInt64().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("UpdatedAt").FromTable("Bans");
    }
}
