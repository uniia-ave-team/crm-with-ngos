using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RenameAvatarAndLogoColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "AvatarUrl",
            table: "User",
            newName: "Avatar");

        migrationBuilder.RenameColumn(
            name: "LogoUrl",
            table: "Ngo",
            newName: "Logo");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Avatar",
            table: "User",
            newName: "AvatarUrl");

        migrationBuilder.RenameColumn(
            name: "Logo",
            table: "Ngo",
            newName: "LogoUrl");
    }
}
