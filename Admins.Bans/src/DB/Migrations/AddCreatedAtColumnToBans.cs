using FluentMigrator;

namespace Admins.Bans.Database.Migrations;

[Migration(20270214103915)]
public class Admins_AddCreatedAtColumnToBans : Migration
{
    public override void Up()
    {
        if (Schema.Table("admins-bans").Exists() && !Schema.Table("admins-bans").Column("CreatedAt").Exists())
        {
            Alter.Table("admins-bans")
                .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("admins-bans").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("admins-bans");
        }
    }
}
