using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RefactorUserCustomFieldsToJsonbSchema : Migration
{
    private static readonly string[] UserCustomFieldUserIdKeyColumns = ["UserId", "Key"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<JsonObject>(
            name: "CustomFields",
            table: "User",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'{}'::jsonb");

        migrationBuilder.AddColumn<byte>(
            name: "DataType",
            table: "UserCustomField",
            type: "smallint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<bool>(
            name: "IsRequired",
            table: "UserCustomField",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "UserCustomField",
            type: "character varying(255)",
            maxLength: 255,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<byte>(
            name: "Order",
            table: "UserCustomField",
            type: "smallint",
            nullable: false,
            defaultValue: (byte)0);

        // Preserve any existing per-user key/value pairs inside User."CustomFields" JSONB before collapsing UserCustomField into a global schema table.
        migrationBuilder.Sql(
            """
            UPDATE "User" u
            SET "CustomFields" = COALESCE(
                (
                    SELECT jsonb_object_agg(cf."Key", to_jsonb(cf."Value"))
                    FROM "UserCustomField" cf
                    WHERE cf."UserId" = u."Id"
                ),
                '{}'::jsonb
            );

            DELETE FROM "UserCustomField"
            WHERE "Id" NOT IN (
                SELECT DISTINCT ON ("Key") "Id"
                FROM "UserCustomField"
                ORDER BY "Key", "CreatedAt", "Id"
            );

            UPDATE "UserCustomField" cf
            SET "Name" = cf."Key",
                "Order" = ranked.row_ord
            FROM (
                SELECT "Id", (ROW_NUMBER() OVER (ORDER BY "Key", "Id") - 1)::smallint AS row_ord
                FROM "UserCustomField"
            ) ranked
            WHERE cf."Id" = ranked."Id";
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_UserCustomField_User_UserId",
            table: "UserCustomField");

        migrationBuilder.DropIndex(
            name: "IX_UserCustomField_UserId_Key",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "Value",
            table: "UserCustomField");

        migrationBuilder.CreateIndex(
            name: "IX_UserCustomField_Key",
            table: "UserCustomField",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserCustomField_Order",
            table: "UserCustomField",
            column: "Order");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserCustomField_Key",
            table: "UserCustomField");

        migrationBuilder.DropIndex(
            name: "IX_UserCustomField_Order",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "DataType",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "IsRequired",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "Name",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "Order",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "CustomFields",
            table: "User");

        migrationBuilder.AddColumn<Guid>(
            name: "UserId",
            table: "UserCustomField",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<string>(
            name: "Value",
            table: "UserCustomField",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.CreateIndex(
            name: "IX_UserCustomField_UserId_Key",
            table: "UserCustomField",
            columns: UserCustomFieldUserIdKeyColumns,
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_UserCustomField_User_UserId",
            table: "UserCustomField",
            column: "UserId",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
