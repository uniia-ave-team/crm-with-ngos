using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddIndexToCustomRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_CustomTableId",
            table: "CustomRecord");

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_CustomTableId_Id",
            table: "CustomRecord",
            columns: ["CustomTableId", "Id"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_CustomTableId_Id",
            table: "CustomRecord");

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_CustomTableId",
            table: "CustomRecord",
            column: "CustomTableId");
    }
}
