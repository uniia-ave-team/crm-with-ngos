using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddGinIndexToCustomRecord : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<NpgsqlTsVector>(
            name: "SearchVector",
            table: "CustomRecord",
            type: "tsvector",
            nullable: true,
            computedColumnSql: "to_tsvector('simple', \"Data\"::text)",
            stored: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_SearchVector",
            table: "CustomRecord",
            column: "SearchVector")
            .Annotation("Npgsql:IndexMethod", "gin");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_SearchVector",
            table: "CustomRecord");

        migrationBuilder.DropColumn(
            name: "SearchVector",
            table: "CustomRecord");
    }
}
