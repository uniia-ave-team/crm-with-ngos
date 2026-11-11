using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UpdateCustomRecordSearchVectorToJsonbValues : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_SearchVector",
            table: "CustomRecord");

        migrationBuilder.AlterColumn<NpgsqlTsVector>(
            name: "SearchVector",
            table: "CustomRecord",
            type: "tsvector",
            nullable: true,
            computedColumnSql: "jsonb_to_tsvector('simple', \"Data\", '[\"string\",\"numeric\"]')",
            stored: true,
            oldClrType: typeof(NpgsqlTsVector),
            oldType: "tsvector",
            oldNullable: true,
            oldComputedColumnSql: "to_tsvector('simple', \"Data\"::text)",
            oldStored: true);

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

        migrationBuilder.AlterColumn<NpgsqlTsVector>(
            name: "SearchVector",
            table: "CustomRecord",
            type: "tsvector",
            nullable: true,
            computedColumnSql: "to_tsvector('simple', \"Data\"::text)",
            stored: true,
            oldClrType: typeof(NpgsqlTsVector),
            oldType: "tsvector",
            oldNullable: true,
            oldComputedColumnSql: "jsonb_to_tsvector('simple', \"Data\", '[\"string\",\"numeric\"]')",
            oldStored: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_SearchVector",
            table: "CustomRecord",
            column: "SearchVector")
            .Annotation("Npgsql:IndexMethod", "gin");
    }
}
