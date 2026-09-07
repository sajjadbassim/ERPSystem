using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntry_Entity_Key_CreatedAt",
                table: "AuditLogEntries",
                columns: new[] { "EntityName", "EntityKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntry_UserId_CreatedAt",
                table: "AuditLogEntries",
                columns: new[] { "UserId", "CreatedAt" });

            // CREATE TRIGGER يجب أن يكون أول أمر في دفعته، و EF Core 10 يدمج نداءات Sql() بلا GO
            Create(migrationBuilder, "A01", AuditLogEntryPreventModification);

            // حارس ثانٍ مستقل عن التريجر، على نمط JournalEntries: التريجر يُسقَط بأمر واحد،
            // والصلاحية تبقى. الإدراج مسموح لأن التطبيق هو من يكتب السجل
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'erp_app')
                    DENY UPDATE, DELETE ON dbo.AuditLogEntries TO erp_app;
                """);
        }

        // تحذير: هذا التراجع يمحو AuditLogEntries بالكامل عبر DropTable،
        // رغم أن التريجر يمنع محو أي صف منه فردياً. هذا تناقض مقصود:
        // Down() أداة تطوير لا عملية تشغيل. يُمنع تشغيل هذا الترحيل بالتراجع
        // (rollback) على أي قاعدة فيها بيانات تدقيق حقيقية — فقدان لا رجعة فيه.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_AuditLogEntry_PreventModification;");

            migrationBuilder.DropTable(
                name: "AuditLogEntries");
        }

        private static void Create(MigrationBuilder migrationBuilder, string tag, string body) =>
            migrationBuilder.Sql(
                "DECLARE @sql" + tag + " NVARCHAR(MAX) = N'" + body.Replace("'", "''") + "';"
                + Environment.NewLine + "EXEC sp_executesql @sql" + tag + ";");

        // 51014 — السجل يُكتب ولا يُمسّ. INSTEAD OF لا AFTER: العملية تُمنع قبل وقوعها
        // فلا حاجة إلى ROLLBACK، وهو نمط TR_JournalEntry_PreventModification نفسه
        private const string AuditLogEntryPreventModification = """
            CREATE TRIGGER TR_AuditLogEntry_PreventModification ON dbo.AuditLogEntries
            INSTEAD OF UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51014, N'سجل التدقيق لا يُعدَّل ولا يُحذف.', 1;
            END
            """;
    }
}
