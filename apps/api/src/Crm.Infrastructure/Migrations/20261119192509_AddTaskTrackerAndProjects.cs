using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTaskTrackerAndProjects : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Project",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Visibility = table.Column<byte>(type: "smallint", nullable: false),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Project", x => x.Id);
                table.ForeignKey(
                    name: "FK_Project_User_CreatedBy",
                    column: x => x.CreatedBy,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProjectMember",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                Claim = table.Column<byte>(type: "smallint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectMember", x => new { x.ProjectId, x.UserId });
                table.ForeignKey(
                    name: "FK_ProjectMember_Project_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Project",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectMember_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Tag",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                NormalizedTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tag", x => x.Id);
                table.ForeignKey(
                    name: "FK_Tag_Project_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Project",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Topic",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                NormalizedTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Order = table.Column<byte>(type: "smallint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Topic", x => x.Id);
                table.ForeignKey(
                    name: "FK_Topic_Project_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Project",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Task",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Priority = table.Column<byte>(type: "smallint", nullable: false),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EndDateWasAuto = table.Column<bool>(type: "boolean", nullable: false),
                Metadata = table.Column<JsonObject>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExternalLinks = table.Column<string>(type: "jsonb", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Task", x => x.Id);
                table.ForeignKey(
                    name: "FK_Task_Project_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Project",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Task_Topic_TopicId",
                    column: x => x.TopicId,
                    principalTable: "Topic",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Comment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Comment", x => x.Id);
                table.ForeignKey(
                    name: "FK_Comment_Task_TaskId",
                    column: x => x.TaskId,
                    principalTable: "Task",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Comment_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "TaskAttachment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                UploadedBy = table.Column<Guid>(type: "uuid", nullable: true),
                OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                StoredFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskAttachment", x => x.Id);
                table.ForeignKey(
                    name: "FK_TaskAttachment_Task_TaskId",
                    column: x => x.TaskId,
                    principalTable: "Task",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TaskAttachment_User_UploadedBy",
                    column: x => x.UploadedBy,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "TaskTag",
            columns: table => new
            {
                TagId = table.Column<Guid>(type: "uuid", nullable: false),
                TaskId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskTag", x => new { x.TagId, x.TaskId });
                table.ForeignKey(
                    name: "FK_TaskTag_Tag_TagId",
                    column: x => x.TagId,
                    principalTable: "Tag",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TaskTag_Task_TaskId",
                    column: x => x.TaskId,
                    principalTable: "Task",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TaskUser",
            columns: table => new
            {
                TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskUser", x => new { x.TaskId, x.UserId });
                table.ForeignKey(
                    name: "FK_TaskUser_Task_TaskId",
                    column: x => x.TaskId,
                    principalTable: "Task",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TaskUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TaskReadState",
            columns: table => new
            {
                CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskReadState", x => new { x.CommentId, x.UserId });
                table.ForeignKey(
                    name: "FK_TaskReadState_Comment_CommentId",
                    column: x => x.CommentId,
                    principalTable: "Comment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TaskReadState_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Comment_TaskId_CreatedAt",
            table: "Comment",
            columns: ["TaskId", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_Comment_UserId",
            table: "Comment",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Project_CreatedBy",
            table: "Project",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_Project_Visibility",
            table: "Project",
            column: "Visibility");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMember_UserId",
            table: "ProjectMember",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Tag_ProjectId_NormalizedTitle",
            table: "Tag",
            columns: ["ProjectId", "NormalizedTitle"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Task_ProjectId_CreatedAt",
            table: "Task",
            columns: ["ProjectId", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_Task_ProjectId_EndDate",
            table: "Task",
            columns: ["ProjectId", "EndDate"]);

        migrationBuilder.CreateIndex(
            name: "IX_Task_ProjectId_Priority",
            table: "Task",
            columns: ["ProjectId", "Priority"]);

        migrationBuilder.CreateIndex(
            name: "IX_Task_ProjectId_StartDate",
            table: "Task",
            columns: ["ProjectId", "StartDate"]);

        migrationBuilder.CreateIndex(
            name: "IX_Task_ProjectId_Status",
            table: "Task",
            columns: ["ProjectId", "Status"]);

        migrationBuilder.CreateIndex(
            name: "IX_Task_ProjectId_TopicId",
            table: "Task",
            columns: ["ProjectId", "TopicId"]);

        migrationBuilder.CreateIndex(
            name: "IX_Task_TopicId",
            table: "Task",
            column: "TopicId");

        migrationBuilder.CreateIndex(
            name: "IX_TaskAttachment_TaskId",
            table: "TaskAttachment",
            column: "TaskId");

        migrationBuilder.CreateIndex(
            name: "IX_TaskAttachment_UploadedBy",
            table: "TaskAttachment",
            column: "UploadedBy");

        migrationBuilder.CreateIndex(
            name: "IX_TaskReadState_UserId",
            table: "TaskReadState",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_TaskTag_TaskId",
            table: "TaskTag",
            column: "TaskId");

        migrationBuilder.CreateIndex(
            name: "IX_TaskUser_UserId",
            table: "TaskUser",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Topic_ProjectId_NormalizedTitle",
            table: "Topic",
            columns: ["ProjectId", "NormalizedTitle"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Topic_ProjectId_Order",
            table: "Topic",
            columns: ["ProjectId", "Order"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectMember");

        migrationBuilder.DropTable(
            name: "TaskAttachment");

        migrationBuilder.DropTable(
            name: "TaskReadState");

        migrationBuilder.DropTable(
            name: "TaskTag");

        migrationBuilder.DropTable(
            name: "TaskUser");

        migrationBuilder.DropTable(
            name: "Comment");

        migrationBuilder.DropTable(
            name: "Tag");

        migrationBuilder.DropTable(
            name: "Task");

        migrationBuilder.DropTable(
            name: "Topic");

        migrationBuilder.DropTable(
            name: "Project");
    }
}
