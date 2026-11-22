using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddEntityAuditLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                EntityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Action = table.Column<byte>(type: "smallint", nullable: false),
                Changes = table.Column<JsonObject>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ActorUserId",
            table: "AuditLogs",
            column: "ActorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EntityType_EntityKey_OccurredAtUtc",
            table: "AuditLogs",
            columns: ["EntityType", "EntityKey", "OccurredAtUtc"]);

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_OccurredAtUtc",
            table: "AuditLogs",
            column: "OccurredAtUtc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditLogs");
    }
}
