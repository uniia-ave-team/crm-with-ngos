using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SimplifyCustomTablesFieldsAndAddRecordPositions : Migration
{
    private static readonly string[] CustomRecordTablePositionColumns = ["CustomTableId", "Position"];
    private static readonly string[] CustomRecordTableIdColumns = ["CustomTableId", "Id"];
    private static readonly string[] CustomFieldTableKeyColumns = ["CustomTableId", "Key"];
    private static readonly string[] TemplateFieldTableKeyColumns = ["TemplateId", "Key"];
    private static readonly string[] TemplateFieldTableOrderColumns = ["TemplateId", "Order"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION crm_try_numeric(val text)
            RETURNS numeric
            LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE AS $$
                SELECT CASE
                    WHEN pg_input_is_valid(val, 'numeric')
                     AND NOT regexp_like(val, '^\s*(NaN|[-+]?Infinity|[-+]?inf)\s*$', 'i')
                    THEN val::numeric
                    ELSE NULL
                END;
            $$;

            CREATE OR REPLACE FUNCTION crm_try_boolean(val text)
            RETURNS boolean
            LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE AS $$
                SELECT CASE
                    WHEN pg_input_is_valid(val, 'boolean')
                    THEN val::boolean
                    ELSE NULL
                END;
            $$;

            CREATE OR REPLACE FUNCTION crm_try_date(val text)
            RETURNS date
            LANGUAGE sql STABLE STRICT PARALLEL SAFE AS $$
                SELECT CASE
                    WHEN pg_input_is_valid(val, 'timestamp with time zone')
                     AND NOT regexp_like(val, '^\s*[-+]?Infinity\s*$', 'i')
                    THEN (timezone('UTC', val::timestamp with time zone))::date
                    ELSE NULL
                END;
            $$;

            CREATE OR REPLACE FUNCTION crm_try_timestamptz(val text)
            RETURNS timestamp with time zone
            LANGUAGE sql STABLE STRICT PARALLEL SAFE AS $$
                SELECT CASE
                    WHEN pg_input_is_valid(val, 'timestamp with time zone')
                     AND NOT regexp_like(val, '^\s*[-+]?Infinity\s*$', 'i')
                    THEN val::timestamp with time zone
                    ELSE NULL
                END;
            $$;

            CREATE OR REPLACE FUNCTION crm_sync_jsonb_keys(
                data jsonb,
                drop_keys text[],
                backfill_keys text[]
            )
            RETURNS jsonb
            LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE AS $$
                SELECT (data - drop_keys) || COALESCE(
                    (
                        SELECT jsonb_object_agg(k, '""'::jsonb)
                        FROM unnest(backfill_keys) AS k
                        WHERE NOT ((data - drop_keys) ? k)
                           OR jsonb_typeof((data - drop_keys) -> k) = 'null'
                    ),
                    '{}'::jsonb
                );
            $$;

            -- Clear pre-refactor dynamic schemas/records (keyed by legacy Key/DataType without fractional Position);
            -- system table templates are re-seeded automatically on startup by TableTemplateSeeder.
            DELETE FROM "CustomTable";
            DELETE FROM "TableTemplate";
            DELETE FROM "UserCustomField";
            UPDATE "User" SET "CustomFields" = '{}'::jsonb;
            """);

        migrationBuilder.DropTable(
            name: "TemplateField");

        migrationBuilder.DropIndex(
            name: "IX_UserCustomField_Key",
            table: "UserCustomField");

        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_CustomTableId_Id",
            table: "CustomRecord");

        migrationBuilder.DropIndex(
            name: "IX_CustomField_CustomTableId_Key",
            table: "CustomField");

        migrationBuilder.DropColumn(
            name: "DataType",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "Key",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "DataType",
            table: "CustomField");

        migrationBuilder.DropColumn(
            name: "Key",
            table: "CustomField");

        migrationBuilder.AddColumn<string>(
            name: "Fields",
            table: "TableTemplate",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AlterColumn<JsonObject>(
            name: "Data",
            table: "CustomRecord",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'{}'::jsonb",
            oldClrType: typeof(JsonObject),
            oldType: "jsonb");

        migrationBuilder.AddColumn<string>(
            name: "Position",
            table: "CustomRecord",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            collation: "C");

        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_SearchVector",
            table: "CustomRecord");

        migrationBuilder.AlterColumn<NpgsqlTsVector>(
            name: "SearchVector",
            table: "CustomRecord",
            type: "tsvector",
            nullable: true,
            computedColumnSql: "jsonb_to_tsvector('simple', \"Data\", '[\"string\"]')",
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

        migrationBuilder.AddCheckConstraint(
            name: "CK_TableTemplate_Fields_IsArray",
            table: "TableTemplate",
            sql: "\"Fields\" IS NOT NULL AND jsonb_typeof(\"Fields\") = 'array'");

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_CustomTableId_Position",
            table: "CustomRecord",
            columns: CustomRecordTablePositionColumns,
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_CustomRecord_Position_NotEmpty",
            table: "CustomRecord",
            sql: "\"Position\" <> ''");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_TableTemplate_Fields_IsArray",
            table: "TableTemplate");

        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_CustomTableId_Position",
            table: "CustomRecord");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CustomRecord_Position_NotEmpty",
            table: "CustomRecord");

        migrationBuilder.DropColumn(
            name: "Fields",
            table: "TableTemplate");

        migrationBuilder.DropColumn(
            name: "Position",
            table: "CustomRecord");

        migrationBuilder.Sql(
            """
            DELETE FROM "CustomTable";
            DELETE FROM "UserCustomField";
            """);

        migrationBuilder.AddColumn<byte>(
            name: "DataType",
            table: "UserCustomField",
            type: "smallint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<string>(
            name: "Key",
            table: "UserCustomField",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AlterColumn<JsonObject>(
            name: "Data",
            table: "CustomRecord",
            type: "jsonb",
            nullable: false,
            oldClrType: typeof(JsonObject),
            oldType: "jsonb",
            oldDefaultValueSql: "'{}'::jsonb");

        migrationBuilder.AddColumn<string>(
            name: "DataType",
            table: "CustomField",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<string>(
            name: "Key",
            table: "CustomField",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: string.Empty);

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
            oldComputedColumnSql: "jsonb_to_tsvector('simple', \"Data\", '[\"string\"]')",
            oldStored: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_SearchVector",
            table: "CustomRecord",
            column: "SearchVector")
            .Annotation("Npgsql:IndexMethod", "gin");

        migrationBuilder.CreateTable(
            name: "TemplateField",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Order = table.Column<byte>(type: "smallint", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

        migrationBuilder.CreateIndex(
            name: "IX_UserCustomField_Key",
            table: "UserCustomField",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_CustomTableId_Id",
            table: "CustomRecord",
            columns: CustomRecordTableIdColumns);

        migrationBuilder.CreateIndex(
            name: "IX_CustomField_CustomTableId_Key",
            table: "CustomField",
            columns: CustomFieldTableKeyColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TemplateField_TemplateId_Key",
            table: "TemplateField",
            columns: TemplateFieldTableKeyColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TemplateField_TemplateId_Order",
            table: "TemplateField",
            columns: TemplateFieldTableOrderColumns);

        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS crm_sync_jsonb_keys(jsonb, text[], text[]);
            DROP FUNCTION IF EXISTS crm_try_timestamptz(text);
            DROP FUNCTION IF EXISTS crm_try_date(text);
            DROP FUNCTION IF EXISTS crm_try_boolean(text);
            DROP FUNCTION IF EXISTS crm_try_numeric(text);
            """);
    }
}
