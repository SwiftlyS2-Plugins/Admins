using FluentMigrator;

namespace Admins.Comms.Database.Migrations;

[Migration(20270214103918)]
public class Admins_AddTimeColumnToSanctions : Migration
{
    public override void Up()
    {
        if (Schema.Table("sanctions").Exists())
        {
            if (!Schema.Table("sanctions").Column("UpdatedAt").Exists())
            {
                Alter.Table("sanctions")
                    .AddColumn("UpdatedAt").AsInt64().NotNullable().WithDefaultValue(0);
            }
            if (!Schema.Table("sanctions").Column("CreatedAt").Exists())
            {
                Alter.Table("sanctions")
                    .AddColumn("CreatedAt").AsInt64().NotNullable().WithDefaultValue(0);
            }
        }
    }

    public override void Down()
    {
        if (Schema.Table("sanctions").Column("UpdatedAt").Exists())
        {
            Delete.Column("UpdatedAt").FromTable("sanctions");
        }
        if (Schema.Table("sanctions").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("sanctions");
        }
    }
}
