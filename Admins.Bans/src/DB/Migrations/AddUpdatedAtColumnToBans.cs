using FluentMigrator;

namespace Admins.Bans.Database.Migrations;

[Migration(20270214103916)]
public class Admins_AddUpdatedAtColumnToBans : Migration
{
    public override void Up()
    {
        if (Schema.Table("admins-bans").Exists() && !Schema.Table("admins-bans").Column("UpdatedAt").Exists())
        {
            Alter.Table("admins-bans")
                .AddColumn("UpdatedAt").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("admins-bans").Column("UpdatedAt").Exists())
        {
            Delete.Column("UpdatedAt").FromTable("admins-bans");
        }
    }
}
