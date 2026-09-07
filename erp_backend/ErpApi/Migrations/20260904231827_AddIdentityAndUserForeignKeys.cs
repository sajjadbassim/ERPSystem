using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpApi.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndUserForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_User_SystemUserHasNoCompany", "([Id] = '00000000-0000-0000-0000-000000000001' AND [CompanyId] IS NULL) OR ([Id] <> '00000000-0000-0000-0000-000000000001' AND [CompanyId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Users_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshToken_ExpiresAfterCreation", "[ExpiresAt] > [CreatedAt]");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "RefreshTokens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Roles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Roles_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserBranches",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranches", x => new { x.UserId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_UserBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionCode });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RolePermissions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            SeedSystemUser(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_CreatedByUserId",
                table: "NumberSequences",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_UpdatedByUserId",
                table: "NumberSequences",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CreatedByUserId",
                table: "JournalEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_CreatedByUserId",
                table: "FxRates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_UpdatedByUserId",
                table: "FxRates",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_CreatedByUserId",
                table: "FiscalYears",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_UpdatedByUserId",
                table: "FiscalYears",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_CreatedByUserId",
                table: "FiscalPeriods",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_UpdatedByUserId",
                table: "FiscalPeriods",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_CreatedByUserId",
                table: "Currencies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_UpdatedByUserId",
                table: "Currencies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CreatedByUserId",
                table: "Companies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_UpdatedByUserId",
                table: "Companies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CreatedByUserId",
                table: "Branches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_UpdatedByUserId",
                table: "Branches",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CreatedByUserId",
                table: "Accounts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UpdatedByUserId",
                table: "Accounts",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReplacedByTokenId",
                table: "RefreshTokens",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "UQ_RefreshToken_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_CreatedByUserId",
                table: "RolePermissions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CreatedByUserId",
                table: "Roles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_UpdatedByUserId",
                table: "Roles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Role_CompanyId_Code",
                table: "Roles",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_BranchId",
                table: "UserBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_CreatedByUserId",
                table: "UserBranches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_CreatedByUserId",
                table: "UserRoles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedByUserId",
                table: "Users",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UpdatedByUserId",
                table: "Users",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_User_CompanyId_Email",
                table: "Users",
                columns: new[] { "CompanyId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_User_CompanyId_UserName",
                table: "Users",
                columns: new[] { "CompanyId", "UserName" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_CreatedByUserId",
                table: "Accounts",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_UpdatedByUserId",
                table: "Accounts",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Users_CreatedByUserId",
                table: "Branches",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Users_UpdatedByUserId",
                table: "Branches",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_CreatedByUserId",
                table: "Companies",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_UpdatedByUserId",
                table: "Companies",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Currencies_Users_CreatedByUserId",
                table: "Currencies",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Currencies_Users_UpdatedByUserId",
                table: "Currencies",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FiscalPeriods_Users_CreatedByUserId",
                table: "FiscalPeriods",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FiscalPeriods_Users_UpdatedByUserId",
                table: "FiscalPeriods",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FiscalYears_Users_CreatedByUserId",
                table: "FiscalYears",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FiscalYears_Users_UpdatedByUserId",
                table: "FiscalYears",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FxRates_Users_CreatedByUserId",
                table: "FxRates",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FxRates_Users_UpdatedByUserId",
                table: "FxRates",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Users_CreatedByUserId",
                table: "JournalEntries",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NumberSequences_Users_CreatedByUserId",
                table: "NumberSequences",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NumberSequences_Users_UpdatedByUserId",
                table: "NumberSequences",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            // CREATE TRIGGER يجب أن يكون أول أمر في دفعته، و EF Core 10 يدمج نداءات Sql() بلا GO
            Create(migrationBuilder, "U01", UserBranchValidateCompanyBoundary);
            Create(migrationBuilder, "U02", UserRoleValidateCompanyBoundary);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS dbo.TR_UserRole_ValidateCompanyBoundary;
                DROP TRIGGER IF EXISTS dbo.TR_UserBranch_ValidateCompanyBoundary;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_CreatedByUserId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_UpdatedByUserId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Users_CreatedByUserId",
                table: "Branches");

            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Users_UpdatedByUserId",
                table: "Branches");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_CreatedByUserId",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_UpdatedByUserId",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Currencies_Users_CreatedByUserId",
                table: "Currencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Currencies_Users_UpdatedByUserId",
                table: "Currencies");

            migrationBuilder.DropForeignKey(
                name: "FK_FiscalPeriods_Users_CreatedByUserId",
                table: "FiscalPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_FiscalPeriods_Users_UpdatedByUserId",
                table: "FiscalPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_FiscalYears_Users_CreatedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropForeignKey(
                name: "FK_FiscalYears_Users_UpdatedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropForeignKey(
                name: "FK_FxRates_Users_CreatedByUserId",
                table: "FxRates");

            migrationBuilder.DropForeignKey(
                name: "FK_FxRates_Users_UpdatedByUserId",
                table: "FxRates");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Users_CreatedByUserId",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_NumberSequences_Users_CreatedByUserId",
                table: "NumberSequences");

            migrationBuilder.DropForeignKey(
                name: "FK_NumberSequences_Users_UpdatedByUserId",
                table: "NumberSequences");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserBranches");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_NumberSequences_CreatedByUserId",
                table: "NumberSequences");

            migrationBuilder.DropIndex(
                name: "IX_NumberSequences_UpdatedByUserId",
                table: "NumberSequences");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_CreatedByUserId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_FxRates_CreatedByUserId",
                table: "FxRates");

            migrationBuilder.DropIndex(
                name: "IX_FxRates_UpdatedByUserId",
                table: "FxRates");

            migrationBuilder.DropIndex(
                name: "IX_FiscalYears_CreatedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropIndex(
                name: "IX_FiscalYears_UpdatedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropIndex(
                name: "IX_FiscalPeriods_CreatedByUserId",
                table: "FiscalPeriods");

            migrationBuilder.DropIndex(
                name: "IX_FiscalPeriods_UpdatedByUserId",
                table: "FiscalPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_CreatedByUserId",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_UpdatedByUserId",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_CreatedByUserId",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_UpdatedByUserId",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Branches_CreatedByUserId",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Branches_UpdatedByUserId",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_CreatedByUserId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UpdatedByUserId",
                table: "Accounts");
        }

        // المستخدم الجذر: منشئ نفسه، بلا شركة، ومعطَّل فلا يُسجَّل دخوله.
        // التجزئة قيمة سليمة الشكل بطول تنسيق PasswordHasher لا رمزاً حراً، حتى لا يرمي
        // التحقق استثناء تنسيق بدل أن يُرجع فشلاً نظيفاً لو استُدعي يوماً.
        private static void SeedSystemUser(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("""
                INSERT INTO dbo.Users
                    (Id, CompanyId, UserName, FullName, Email, PasswordHash, SecurityStamp,
                     IsActive, CreatedAt, CreatedByUserId)
                SELECT '00000000-0000-0000-0000-000000000001', NULL, N'system', N'مستخدم النظام', NULL,
                       N'AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==',
                       '00000000-0000-0000-0000-000000000002', 0,
                       '2026-01-01T00:00:00', '00000000-0000-0000-0000-000000000001'
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = '00000000-0000-0000-0000-000000000001');
                """);

        private static void Create(MigrationBuilder migrationBuilder, string tag, string body) =>
            migrationBuilder.Sql(
                "DECLARE @sql" + tag + " NVARCHAR(MAX) = N'" + body.Replace("'", "''") + "';"
                + Environment.NewLine + "EXEC sp_executesql @sql" + tag + ";");

        // 51012 — نطاق الفرع لا يعبر حدود الشركة. المستخدم الجذر بلا شركة فلا يُمنح فرعاً
        private const string UserBranchValidateCompanyBoundary = """
            CREATE TRIGGER TR_UserBranch_ValidateCompanyBoundary ON dbo.UserBranches
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.Users u ON u.Id = i.UserId
                           INNER JOIN dbo.Branches b ON b.Id = i.BranchId
                           WHERE u.CompanyId IS NULL OR u.CompanyId <> b.CompanyId)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51012, N'الفرع يخص شركة أخرى غير شركة المستخدم.', 1;
                END
            END
            """;

        // 51013 — الدور لا يُمنح عبر حدود الشركة
        private const string UserRoleValidateCompanyBoundary = """
            CREATE TRIGGER TR_UserRole_ValidateCompanyBoundary ON dbo.UserRoles
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.Users u ON u.Id = i.UserId
                           INNER JOIN dbo.Roles r ON r.Id = i.RoleId
                           WHERE u.CompanyId IS NULL OR u.CompanyId <> r.CompanyId)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51013, N'الدور يخص شركة أخرى غير شركة المستخدم.', 1;
                END
            END
            """;
    }
}
