using FluentMigrator;

namespace Admins.Comms.Database.Migrations;

[Migration(20270214103918)]
public class Admins_AddTimeColumnToSanctions : Migration
{
    public override void Up()
    {
        if (Schema.Table("admins-sanctions").Exists())
        {
            if (!Schema.Table("admins-sanctions").Column("UpdatedAt").Exists())
            {
                Alter.Table("admins-sanctions")
                    .AddColumn("UpdatedAt").AsInt64().NotNullable().WithDefaultValue(0);
            }
            if (!Schema.Table("admins-sanctions").Column("CreatedAt").Exists())
            {
                Alter.Table("admins-sanctions")
                    .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
            }
        }
    }

    public override void Down()
    {
        if (Schema.Table("admins-sanctions").Column("UpdatedAt").Exists())
        {
            Delete.Column("UpdatedAt").FromTable("admins-sanctions");
        }
        if (Schema.Table("admins-sanctions").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("admins-sanctions");
        }
    }
}
