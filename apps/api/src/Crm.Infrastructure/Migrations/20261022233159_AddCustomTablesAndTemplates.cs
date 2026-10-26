using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddCustomTablesAndTemplates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TableTemplate",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                IsSystemPreset = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TableTemplate", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CustomTable",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                SourceTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                NgoId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomTable", x => x.Id);
                table.ForeignKey(
                    name: "FK_CustomTable_Ngo_NgoId",
                    column: x => x.NgoId,
                    principalTable: "Ngo",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CustomTable_TableTemplate_SourceTemplateId",
                    column: x => x.SourceTemplateId,
                    principalTable: "TableTemplate",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "TemplateField",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                Order = table.Column<byte>(type: "smallint", nullable: false),
                TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateField", x => x.Id);
                table.ForeignKey(
                    name: "FK_TemplateField_TableTemplate_TemplateId",
                    column: x => x.TemplateId,
                    principalTable: "TableTemplate",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CustomField",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                Order = table.Column<byte>(type: "smallint", nullable: false),
                CustomTableId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomField", x => x.Id);
                table.ForeignKey(
                    name: "FK_CustomField_CustomTable_CustomTableId",
                    column: x => x.CustomTableId,
                    principalTable: "CustomTable",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CustomRecord",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CustomTableId = table.Column<Guid>(type: "uuid", nullable: false),
                Data = table.Column<JsonObject>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomRecord", x => x.Id);
                table.ForeignKey(
                    name: "FK_CustomRecord_CustomTable_CustomTableId",
                    column: x => x.CustomTableId,
                    principalTable: "CustomTable",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomField_CustomTableId_Key",
            table: "CustomField",
            columns: ["CustomTableId", "Key"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_CustomTableId",
            table: "CustomRecord",
            column: "CustomTableId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomTable_NgoId",
            table: "CustomTable",
            column: "NgoId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomTable_SourceTemplateId",
            table: "CustomTable",
            column: "SourceTemplateId");

        migrationBuilder.CreateIndex(
            name: "IX_TemplateField_TemplateId_Key",
            table: "TemplateField",
            columns: ["TemplateId", "Key"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TemplateField_TemplateId_Order",
            table: "TemplateField",
            columns: ["TemplateId", "Order"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CustomField");

        migrationBuilder.DropTable(
            name: "CustomRecord");

        migrationBuilder.DropTable(
            name: "TemplateField");

        migrationBuilder.DropTable(
            name: "CustomTable");

        migrationBuilder.DropTable(
            name: "TableTemplate");
    }
}
