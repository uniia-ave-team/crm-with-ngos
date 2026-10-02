using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUserRefreshTokensAndRefactorProfile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RefreshToken",
            table: "AuthUsers");

        migrationBuilder.DropColumn(
            name: "RefreshTokenExpiryTime",
            table: "AuthUsers");

        migrationBuilder.AlterColumn<byte>(
            name: "PronounCategory",
            table: "User",
            type: "smallint",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.CreateTable(
            name: "UserRefreshTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRefreshTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserRefreshTokens_AuthUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AuthUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserRefreshTokens_Token",
            table: "UserRefreshTokens",
            column: "Token",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserRefreshTokens_UserId",
            table: "UserRefreshTokens",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserRefreshTokens");

        migrationBuilder.AlterColumn<int>(
            name: "PronounCategory",
            table: "User",
            type: "integer",
            nullable: true,
            oldClrType: typeof(byte),
            oldType: "smallint",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RefreshToken",
            table: "AuthUsers",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "RefreshTokenExpiryTime",
            table: "AuthUsers",
            type: "timestamp with time zone",
            nullable: true);
    }
}
