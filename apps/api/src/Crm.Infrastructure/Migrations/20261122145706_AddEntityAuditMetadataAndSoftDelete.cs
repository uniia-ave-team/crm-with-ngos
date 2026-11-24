using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddEntityAuditMetadataAndSoftDelete : Migration
{
    private static readonly string[] CustomFieldOrderColumns = ["CustomTableId", "Order"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "UserCustomField",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "UserCustomField",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "LastLoginAt",
            table: "User",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "User",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "Topic",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "Topic",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "TemplateField",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "TemplateField",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedBy",
            table: "Task",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "Task",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "Task",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            table: "Task",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "Tag",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "Tag",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "TableTemplate",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedBy",
            table: "TableTemplate",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "TableTemplate",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            table: "TableTemplate",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "JoinedAt",
            table: "ProjectMember",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "Project",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "Project",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "Project",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "Project",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            table: "Project",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "Ngo",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "MaintenanceNotice",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedBy",
            table: "MaintenanceNotice",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            table: "MaintenanceNotice",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "LoginPageImages",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "CustomTable",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedBy",
            table: "CustomTable",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "CustomTable",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "CustomTable",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "CustomTable",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            table: "CustomTable",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "CustomRecord",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedBy",
            table: "CustomRecord",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "CustomRecord",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "UpdatedBy",
            table: "CustomRecord",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "CustomField",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "CustomField",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.CreateIndex(
            name: "IX_UserRefreshTokens_ExpiresAt",
            table: "UserRefreshTokens",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_Task_CreatedBy",
            table: "Task",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_Task_IsDeleted",
            table: "Task",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_Task_UpdatedBy",
            table: "Task",
            column: "UpdatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_TableTemplate_CreatedBy",
            table: "TableTemplate",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_TableTemplate_UpdatedBy",
            table: "TableTemplate",
            column: "UpdatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_Project_IsDeleted",
            table: "Project",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_Project_UpdatedBy",
            table: "Project",
            column: "UpdatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceNotice_CreatedBy",
            table: "MaintenanceNotice",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceNotice_UpdatedBy",
            table: "MaintenanceNotice",
            column: "UpdatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_CustomTable_CreatedBy",
            table: "CustomTable",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_CustomTable_IsDeleted",
            table: "CustomTable",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_CustomTable_UpdatedBy",
            table: "CustomTable",
            column: "UpdatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_CreatedBy",
            table: "CustomRecord",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_CustomRecord_UpdatedBy",
            table: "CustomRecord",
            column: "UpdatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_CustomField_CustomTableId_Order",
            table: "CustomField",
            columns: CustomFieldOrderColumns);

        migrationBuilder.AddForeignKey(
            name: "FK_CustomRecord_User_CreatedBy",
            table: "CustomRecord",
            column: "CreatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_CustomRecord_User_UpdatedBy",
            table: "CustomRecord",
            column: "UpdatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_CustomTable_User_CreatedBy",
            table: "CustomTable",
            column: "CreatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_CustomTable_User_UpdatedBy",
            table: "CustomTable",
            column: "UpdatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_MaintenanceNotice_User_CreatedBy",
            table: "MaintenanceNotice",
            column: "CreatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_MaintenanceNotice_User_UpdatedBy",
            table: "MaintenanceNotice",
            column: "UpdatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Project_User_UpdatedBy",
            table: "Project",
            column: "UpdatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_TableTemplate_User_CreatedBy",
            table: "TableTemplate",
            column: "CreatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_TableTemplate_User_UpdatedBy",
            table: "TableTemplate",
            column: "UpdatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Task_User_CreatedBy",
            table: "Task",
            column: "CreatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Task_User_UpdatedBy",
            table: "Task",
            column: "UpdatedBy",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CustomRecord_User_CreatedBy",
            table: "CustomRecord");

        migrationBuilder.DropForeignKey(
            name: "FK_CustomRecord_User_UpdatedBy",
            table: "CustomRecord");

        migrationBuilder.DropForeignKey(
            name: "FK_CustomTable_User_CreatedBy",
            table: "CustomTable");

        migrationBuilder.DropForeignKey(
            name: "FK_CustomTable_User_UpdatedBy",
            table: "CustomTable");

        migrationBuilder.DropForeignKey(
            name: "FK_MaintenanceNotice_User_CreatedBy",
            table: "MaintenanceNotice");

        migrationBuilder.DropForeignKey(
            name: "FK_MaintenanceNotice_User_UpdatedBy",
            table: "MaintenanceNotice");

        migrationBuilder.DropForeignKey(
            name: "FK_Project_User_UpdatedBy",
            table: "Project");

        migrationBuilder.DropForeignKey(
            name: "FK_TableTemplate_User_CreatedBy",
            table: "TableTemplate");

        migrationBuilder.DropForeignKey(
            name: "FK_TableTemplate_User_UpdatedBy",
            table: "TableTemplate");

        migrationBuilder.DropForeignKey(
            name: "FK_Task_User_CreatedBy",
            table: "Task");

        migrationBuilder.DropForeignKey(
            name: "FK_Task_User_UpdatedBy",
            table: "Task");

        migrationBuilder.DropIndex(
            name: "IX_UserRefreshTokens_ExpiresAt",
            table: "UserRefreshTokens");

        migrationBuilder.DropIndex(
            name: "IX_Task_CreatedBy",
            table: "Task");

        migrationBuilder.DropIndex(
            name: "IX_Task_IsDeleted",
            table: "Task");

        migrationBuilder.DropIndex(
            name: "IX_Task_UpdatedBy",
            table: "Task");

        migrationBuilder.DropIndex(
            name: "IX_TableTemplate_CreatedBy",
            table: "TableTemplate");

        migrationBuilder.DropIndex(
            name: "IX_TableTemplate_UpdatedBy",
            table: "TableTemplate");

        migrationBuilder.DropIndex(
            name: "IX_Project_IsDeleted",
            table: "Project");

        migrationBuilder.DropIndex(
            name: "IX_Project_UpdatedBy",
            table: "Project");

        migrationBuilder.DropIndex(
            name: "IX_MaintenanceNotice_CreatedBy",
            table: "MaintenanceNotice");

        migrationBuilder.DropIndex(
            name: "IX_MaintenanceNotice_UpdatedBy",
            table: "MaintenanceNotice");

        migrationBuilder.DropIndex(
            name: "IX_CustomTable_CreatedBy",
            table: "CustomTable");

        migrationBuilder.DropIndex(
            name: "IX_CustomTable_IsDeleted",
            table: "CustomTable");

        migrationBuilder.DropIndex(
            name: "IX_CustomTable_UpdatedBy",
            table: "CustomTable");

        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_CreatedBy",
            table: "CustomRecord");

        migrationBuilder.DropIndex(
            name: "IX_CustomRecord_UpdatedBy",
            table: "CustomRecord");

        migrationBuilder.DropIndex(
            name: "IX_CustomField_CustomTableId_Order",
            table: "CustomField");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "UserCustomField");

        migrationBuilder.DropColumn(
            name: "LastLoginAt",
            table: "User");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "User");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "Topic");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "Topic");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "TemplateField");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "TemplateField");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            table: "Task");

        migrationBuilder.DropColumn(
            name: "DeletedAt",
            table: "Task");

        migrationBuilder.DropColumn(
            name: "IsDeleted",
            table: "Task");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            table: "Task");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "Tag");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "Tag");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "TableTemplate");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            table: "TableTemplate");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "TableTemplate");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            table: "TableTemplate");

        migrationBuilder.DropColumn(
            name: "JoinedAt",
            table: "ProjectMember");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "Project");

        migrationBuilder.DropColumn(
            name: "DeletedAt",
            table: "Project");

        migrationBuilder.DropColumn(
            name: "IsDeleted",
            table: "Project");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "Project");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            table: "Project");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "Ngo");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "MaintenanceNotice");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            table: "MaintenanceNotice");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            table: "MaintenanceNotice");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "LoginPageImages");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "CustomTable");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            table: "CustomTable");

        migrationBuilder.DropColumn(
            name: "DeletedAt",
            table: "CustomTable");

        migrationBuilder.DropColumn(
            name: "IsDeleted",
            table: "CustomTable");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "CustomTable");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            table: "CustomTable");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "CustomRecord");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            table: "CustomRecord");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "CustomRecord");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            table: "CustomRecord");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "CustomField");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            table: "CustomField");
    }
}
