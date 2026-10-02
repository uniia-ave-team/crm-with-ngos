using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUserPronounsAndAvatar : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AvatarUrl",
            table: "User",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Pronouns",
            table: "User",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AvatarUrl",
            table: "User");

        migrationBuilder.DropColumn(
            name: "Pronouns",
            table: "User");
    }
}
