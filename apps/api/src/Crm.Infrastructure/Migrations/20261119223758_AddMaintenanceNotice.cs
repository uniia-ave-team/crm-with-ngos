using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMaintenanceNotice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MaintenanceNotice",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MaintenanceNotice", x => x.Id);
                table.CheckConstraint("CK_MaintenanceNotice_EndsAfterStarts", "\"EndsAt\" > \"StartsAt\"");
                table.CheckConstraint("CK_MaintenanceNotice_Singleton", "\"Id\" = 1");
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MaintenanceNotice");
    }
}
