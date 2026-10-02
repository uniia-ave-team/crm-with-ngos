using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Crm.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuthRoles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AuthUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RefreshToken = table.Column<string>(type: "text", nullable: true),
                RefreshTokenExpiryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: true),
                SecurityStamp = table.Column<string>(type: "text", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                PhoneNumber = table.Column<string>(type: "text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Ngo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Ngo", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AuthRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthRoleClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuthRoleClaims_AuthRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AuthRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AuthUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuthUserClaims_AuthUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AuthUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AuthUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                ProviderKey = table.Column<string>(type: "text", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AuthUserLogins_AuthUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AuthUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AuthUserRoles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_AuthUserRoles_AuthRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AuthRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AuthUserRoles_AuthUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AuthUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AuthUserTokens",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AuthUserTokens_AuthUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AuthUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "User",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Patronymic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                InternalPosition = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                EmergencyContact = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                PreferredLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                NgoId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_User", x => x.Id);
                table.ForeignKey(
                    name: "FK_User_AuthUsers_Id",
                    column: x => x.Id,
                    principalTable: "AuthUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_User_Ngo_NgoId",
                    column: x => x.NgoId,
                    principalTable: "Ngo",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuthRoleClaims_RoleId",
            table: "AuthRoleClaims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AuthRoles",
            column: "NormalizedName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuthUserClaims_UserId",
            table: "AuthUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthUserLogins_UserId",
            table: "AuthUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthUserRoles_RoleId",
            table: "AuthUserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AuthUsers",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AuthUsers",
            column: "NormalizedUserName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_User_NgoId",
            table: "User",
            column: "NgoId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuthRoleClaims");

        migrationBuilder.DropTable(
            name: "AuthUserClaims");

        migrationBuilder.DropTable(
            name: "AuthUserLogins");

        migrationBuilder.DropTable(
            name: "AuthUserRoles");

        migrationBuilder.DropTable(
            name: "AuthUserTokens");

        migrationBuilder.DropTable(
            name: "User");

        migrationBuilder.DropTable(
            name: "AuthRoles");

        migrationBuilder.DropTable(
            name: "AuthUsers");

        migrationBuilder.DropTable(
            name: "Ngo");
    }
}
