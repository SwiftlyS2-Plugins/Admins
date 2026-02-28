using FluentMigrator;

namespace Admins.Bans.Database.Migrations;

[Migration(20270214103915)]
public class Admins_AddCreatedAtColumnToBans : Migration
{
    public override void Up()
    {
        if (Schema.Table("bans").Exists() && !Schema.Table("bans").Column("CreatedAt").Exists())
        {
            Alter.Table("bans")
                .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("bans").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("bans");
        }
    }
}
