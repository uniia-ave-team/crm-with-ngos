using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUserActionTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserActionTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TargetEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                NormalizedTargetEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                EncryptedMetadata = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserActionTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserActionTokens_User_CreatedBy",
                    column: x => x.CreatedBy,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_UserActionTokens_User_TargetUserId",
                    column: x => x.TargetUserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserActionTokens_CreatedBy",
            table: "UserActionTokens",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_UserActionTokens_ExpiresAt",
            table: "UserActionTokens",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_UserActionTokens_TargetUserId",
            table: "UserActionTokens",
            column: "TargetUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserActionTokens_TokenHash",
            table: "UserActionTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserActionTokens_Type_ExpiresAt",
            table: "UserActionTokens",
            columns: ["Type", "ExpiresAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_UserActionTokens_Type_NormalizedTargetEmail",
            table: "UserActionTokens",
            columns: ["Type", "NormalizedTargetEmail"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserActionTokens");
    }
}
