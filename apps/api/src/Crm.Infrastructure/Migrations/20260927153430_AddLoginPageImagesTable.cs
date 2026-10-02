using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLoginPageImagesTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LoginPageImages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                NgoId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoginPageImages", x => x.Id);
                table.ForeignKey(
                    name: "FK_LoginPageImages_Ngo_NgoId",
                    column: x => x.NgoId,
                    principalTable: "Ngo",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LoginPageImages_NgoId",
            table: "LoginPageImages",
            column: "NgoId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LoginPageImages");
    }
}
