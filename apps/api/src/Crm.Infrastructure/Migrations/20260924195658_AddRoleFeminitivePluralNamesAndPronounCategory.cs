using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRoleFeminitivePluralNamesAndPronounCategory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "PronounCategory",
            table: "User",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FeminitiveName",
            table: "AuthRoles",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PluralName",
            table: "AuthRoles",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PronounCategory",
            table: "User");

        migrationBuilder.DropColumn(
            name: "FeminitiveName",
            table: "AuthRoles");

        migrationBuilder.DropColumn(
            name: "PluralName",
            table: "AuthRoles");
    }
}
