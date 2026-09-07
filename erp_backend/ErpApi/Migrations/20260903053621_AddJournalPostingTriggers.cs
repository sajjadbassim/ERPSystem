using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpApi.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalPostingTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntry_DocumentDateNotAfterPostingDate",
                table: "JournalEntries",
                sql: "[DocumentDate] <= [PostingDate]");

            // CREATE TRIGGER كـ CREATE PROCEDURE: يجب أن يكون أول أمر في دفعته، و EF Core 10
            // يدمج نداءات Sql() بلا GO. اللف في sp_executesql بمتغير مستقل لكل واحد هو الحل.
            Create(migrationBuilder, "T01", CompanyProtectBaseCurrencyLock);
            Create(migrationBuilder, "T02", AccountValidateConfiguration);
            Create(migrationBuilder, "T03", FiscalYearPreventOverlap);
            Create(migrationBuilder, "T04", FiscalYearPreventCloseWithOpenPeriods);
            Create(migrationBuilder, "T05", FiscalPeriodWithinYearRange);
            Create(migrationBuilder, "T06", FiscalPeriodPreventOverlap);
            Create(migrationBuilder, "T07", NumberSequencePreventDelete);
            Create(migrationBuilder, "T08", JournalEntryValidatePostingContext);
            Create(migrationBuilder, "T09", JournalEntryLockBaseCurrencyOnFirstPosting);
            Create(migrationBuilder, "T10", JournalEntryPreventModification);
            Create(migrationBuilder, "T11", JournalLineValidateLineContext);
            Create(migrationBuilder, "T12", JournalLineEnforceEntryBalance);
            Create(migrationBuilder, "T13", JournalLinePreventModification);

            // ترتيب صريح بدل ترتيب غير معرَّف: التحقق السياقي قبل فحص التوازن،
            // وتحقق سياق الترحيل قبل رفع قفل عملة الأساس
            migrationBuilder.Sql("""
                EXEC sp_settriggerorder @triggername = N'dbo.TR_JournalLine_ValidateLineContext', @order = N'First',  @stmttype = N'INSERT';
                EXEC sp_settriggerorder @triggername = N'dbo.TR_JournalLine_EnforceEntryBalance', @order = N'Last',   @stmttype = N'INSERT';
                EXEC sp_settriggerorder @triggername = N'dbo.TR_JournalEntry_ValidatePostingContext', @order = N'First', @stmttype = N'INSERT';
                EXEC sp_settriggerorder @triggername = N'dbo.TR_JournalEntry_LockBaseCurrencyOnFirstPosting', @order = N'Last', @stmttype = N'INSERT';
                """);

            // سلسلة الملكية تتجاوز فحص صلاحيات الجدول داخل الإجراء، فيبقى DENY نافذاً
            // على الكتابة المباشرة وحدها. هذا هو فرض R-GL-05 في القاعدة لا بالانضباط.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'erp_app')
                    CREATE USER erp_app WITHOUT LOGIN;

                ALTER ROLE db_datareader ADD MEMBER erp_app;
                ALTER ROLE db_datawriter ADD MEMBER erp_app;

                DENY INSERT, UPDATE, DELETE ON dbo.JournalEntries TO erp_app;
                DENY INSERT, UPDATE, DELETE ON dbo.JournalLines   TO erp_app;

                GRANT EXECUTE ON dbo.usp_JournalEntry_Post    TO erp_app;
                GRANT EXECUTE ON dbo.usp_JournalEntry_Reverse TO erp_app;
                GRANT EXECUTE ON TYPE::dbo.JournalLineInput   TO erp_app;
                GRANT EXECUTE ON TYPE::dbo.GuidList           TO erp_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP USER IF EXISTS erp_app;

                DROP TRIGGER IF EXISTS dbo.TR_JournalLine_PreventModification;
                DROP TRIGGER IF EXISTS dbo.TR_JournalLine_EnforceEntryBalance;
                DROP TRIGGER IF EXISTS dbo.TR_JournalLine_ValidateLineContext;
                DROP TRIGGER IF EXISTS dbo.TR_JournalEntry_PreventModification;
                DROP TRIGGER IF EXISTS dbo.TR_JournalEntry_LockBaseCurrencyOnFirstPosting;
                DROP TRIGGER IF EXISTS dbo.TR_JournalEntry_ValidatePostingContext;
                DROP TRIGGER IF EXISTS dbo.TR_NumberSequence_PreventDelete;
                DROP TRIGGER IF EXISTS dbo.TR_FiscalPeriod_PreventOverlap;
                DROP TRIGGER IF EXISTS dbo.TR_FiscalPeriod_WithinYearRange;
                DROP TRIGGER IF EXISTS dbo.TR_FiscalYear_PreventCloseWithOpenPeriods;
                DROP TRIGGER IF EXISTS dbo.TR_FiscalYear_PreventOverlap;
                DROP TRIGGER IF EXISTS dbo.TR_Account_ValidateConfiguration;
                DROP TRIGGER IF EXISTS dbo.TR_Company_ProtectBaseCurrencyLock;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntry_DocumentDateNotAfterPostingDate",
                table: "JournalEntries");
        }

        private static void Create(MigrationBuilder migrationBuilder, string tag, string body) =>
            migrationBuilder.Sql(
                "DECLARE @sql" + tag + " NVARCHAR(MAX) = N'" + body.Replace("'", "''") + "';"
                + Environment.NewLine + "EXEC sp_executesql @sql" + tag + ";");

        // 51005 — R-BASE-02: القفل يُرفع ولا يُخفض، والعملة لا تتغير بعده
        private const string CompanyProtectBaseCurrencyLock = """
            CREATE TRIGGER TR_Company_ProtectBaseCurrencyLock ON dbo.Companies
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN deleted d ON d.Id = i.Id
                           WHERE (d.IsBaseCurrencyLocked = 1 AND i.IsBaseCurrencyLocked = 0)
                              OR (d.IsBaseCurrencyLocked = 1 AND i.BaseCurrencyId <> d.BaseCurrencyId))
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51005, N'قفل عملة الأساس لا يُخفض ولا تتغير العملة بعده.', 1;
                END
            END
            """;

        // 51007 — سلامة شجرة الحسابات وحسابات الأدوار النظامية
        private const string AccountValidateConfiguration = """
            CREATE TRIGGER TR_Account_ValidateConfiguration ON dbo.Accounts
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.Accounts p ON p.Id = i.ParentAccountId
                           WHERE p.CompanyId <> i.CompanyId)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51007, N'الحساب الأب يخص شركة أخرى.', 1;
                END

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.Companies c ON c.Id = i.CompanyId
                           WHERE i.SystemAccountRole IS NOT NULL
                             AND i.CurrencyId IS NOT NULL
                             AND i.CurrencyId <> c.BaseCurrencyId)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51007, N'حساب الدور النظامي يجب أن يكون بلا تقييد عملة أو بعملة الدفاتر.', 1;
                END

                DECLARE @Cycles INT;

                ;WITH Ancestors AS
                (
                    SELECT i.Id AS RootId, a.ParentAccountId AS CurrentId, 1 AS Depth
                    FROM inserted i
                    INNER JOIN dbo.Accounts a ON a.Id = i.Id
                    WHERE a.ParentAccountId IS NOT NULL
                    UNION ALL
                    SELECT an.RootId, p.ParentAccountId, an.Depth + 1
                    FROM Ancestors an
                    INNER JOIN dbo.Accounts p ON p.Id = an.CurrentId
                    WHERE p.ParentAccountId IS NOT NULL AND an.Depth < 50
                )
                SELECT @Cycles = COUNT(*) FROM Ancestors WHERE CurrentId = RootId
                OPTION (MAXRECURSION 100);

                IF @Cycles > 0
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51007, N'شجرة الحسابات لا تقبل دورة.', 1;
                END
            END
            """;

        // 51008 — تداخل السنوات يجعل تحويل PostingDate إلى فترة ملتبساً، وهو أساس R-GL-04
        private const string FiscalYearPreventOverlap = """
            CREATE TRIGGER TR_FiscalYear_PreventOverlap ON dbo.FiscalYears
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.FiscalYears y
                                   ON y.CompanyId = i.CompanyId AND y.Id <> i.Id AND y.IsDeleted = 0
                           WHERE i.IsDeleted = 0
                             AND i.StartDate <= y.EndDate
                             AND y.StartDate <= i.EndDate)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51008, N'السنة المالية تتداخل مع سنة أخرى لنفس الشركة.', 1;
                END
            END
            """;

        // 51009 — AFTER UPDATE حصراً: البذر يُدرج سنة مقفلة وفترتها مفتوحة عمداً،
        // وامتداد التريجر إلى INSERT يمنع تهيئة بيانات الاختبار
        private const string FiscalYearPreventCloseWithOpenPeriods = """
            CREATE TRIGGER TR_FiscalYear_PreventCloseWithOpenPeriods ON dbo.FiscalYears
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN deleted d ON d.Id = i.Id
                           WHERE d.IsClosed = 0 AND i.IsClosed = 1
                             AND EXISTS (SELECT 1 FROM dbo.FiscalPeriods p
                                         WHERE p.FiscalYearId = i.Id
                                           AND p.IsDeleted = 0
                                           AND p.IsClosed = 0))
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51009, N'لا تُقفل السنة المالية وفيها فترة مفتوحة.', 1;
                END
            END
            """;

        // 51010
        private const string FiscalPeriodWithinYearRange = """
            CREATE TRIGGER TR_FiscalPeriod_WithinYearRange ON dbo.FiscalPeriods
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.FiscalYears y ON y.Id = i.FiscalYearId
                           WHERE i.StartDate < y.StartDate OR i.EndDate > y.EndDate)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51010, N'مدى الفترة المالية يخرج عن مدى سنتها.', 1;
                END
            END
            """;

        // 51011 — الفترات العادية وحدها. فترة التسويات تتداخل مع آخر فترة عادية بحكم تعريفها
        private const string FiscalPeriodPreventOverlap = """
            CREATE TRIGGER TR_FiscalPeriod_PreventOverlap ON dbo.FiscalPeriods
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.FiscalPeriods p
                                   ON p.FiscalYearId = i.FiscalYearId AND p.Id <> i.Id
                           WHERE i.PeriodType = 1 AND p.PeriodType = 1
                             AND i.IsDeleted = 0 AND p.IsDeleted = 0
                             AND i.StartDate <= p.EndDate
                             AND p.StartDate <= i.EndDate)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51011, N'الفترة العادية تتداخل مع فترة عادية أخرى في نفس السنة.', 1;
                END
            END
            """;

        // 51006 — حذف العدّاد يعيد الترقيم من الصفر
        private const string NumberSequencePreventDelete = """
            CREATE TRIGGER TR_NumberSequence_PreventDelete ON dbo.NumberSequences
            INSTEAD OF DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51006, N'حذف عدّاد الترقيم ممنوع.', 1;
            END
            """;

        // 51003 — R-GL-04 وحماية العكس. يتكرر مع فحص الإجراء عمداً: الإجراء يعطي الرسالة،
        // والتريجر يمنع الالتفاف عليه
        private const string JournalEntryValidatePostingContext = """
            CREATE TRIGGER TR_JournalEntry_ValidatePostingContext ON dbo.JournalEntries
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.Branches b ON b.Id = i.BranchId
                           INNER JOIN dbo.FiscalPeriods p ON p.Id = i.FiscalPeriodId
                           INNER JOIN dbo.FiscalYears y ON y.Id = p.FiscalYearId
                           WHERE y.CompanyId <> b.CompanyId
                              OR i.PostingDate < p.StartDate
                              OR i.PostingDate > p.EndDate
                              OR p.IsClosed = 1
                              OR y.IsClosed = 1)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51003, N'سياق الترحيل غير صالح: الفترة أو تاريخها أو قفلها.', 1;
                END

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.JournalEntries o ON o.Id = i.ReversalOfJournalEntryId
                           INNER JOIN dbo.Branches ib ON ib.Id = i.BranchId
                           INNER JOIN dbo.Branches ob ON ob.Id = o.BranchId
                           WHERE o.ReversalOfJournalEntryId IS NOT NULL
                              OR ob.CompanyId <> ib.CompanyId)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51003, N'ربط العكس غير صالح: عكس لعكس أو قيد من شركة أخرى.', 1;
                END
            END
            """;

        // بلا خطأ — R-BASE-02/03: أول ترحيل يرفع القفل، والترحيل التالي لا يغيّر شيئاً
        private const string JournalEntryLockBaseCurrencyOnFirstPosting = """
            CREATE TRIGGER TR_JournalEntry_LockBaseCurrencyOnFirstPosting ON dbo.JournalEntries
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;

                UPDATE c
                SET c.IsBaseCurrencyLocked = 1
                FROM dbo.Companies c
                INNER JOIN dbo.Branches b ON b.CompanyId = c.Id
                INNER JOIN inserted i ON i.BranchId = b.Id
                WHERE c.IsBaseCurrencyLocked = 0;
            END
            """;

        // 51002 — R-GL-03 و R-LIFE-03
        private const string JournalEntryPreventModification = """
            CREATE TRIGGER TR_JournalEntry_PreventModification ON dbo.JournalEntries
            INSTEAD OF UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51002, N'القيد المرحَّل لا يُعدَّل ولا يُحذف. التصحيح بقيد عكسي.', 1;
            END
            """;

        // 51004 — سياق السطر: الحساب وعملته وقاعدة عملة الأساس
        private const string JournalLineValidateLineContext = """
            CREATE TRIGGER TR_JournalLine_ValidateLineContext ON dbo.JournalLines
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i
                           INNER JOIN dbo.JournalEntries e ON e.Id = i.JournalEntryId
                           INNER JOIN dbo.Branches b ON b.Id = e.BranchId
                           INNER JOIN dbo.Companies c ON c.Id = b.CompanyId
                           INNER JOIN dbo.Accounts a ON a.Id = i.AccountId
                           WHERE a.CompanyId <> b.CompanyId
                              OR a.IsPostable = 0
                              OR a.IsActive = 0
                              OR a.IsDeleted = 1
                              OR (a.CurrencyId IS NOT NULL AND a.CurrencyId <> i.CurrencyId)
                              OR (i.CurrencyId = c.BaseCurrencyId
                                  AND (i.ExchangeRate <> 1
                                       OR i.DebitFC  <> i.DebitBase
                                       OR i.CreditFC <> i.CreditBase)))
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51004, N'سياق السطر غير صالح: الحساب أو عملته أو قاعدة عملة الدفاتر.', 1;
                END
            END
            """;

        // 51001 — R-GL-02: يقرأ كل سطور كل قيد متأثر لا صفوف inserted وحدها
        private const string JournalLineEnforceEntryBalance = """
            CREATE TRIGGER TR_JournalLine_EnforceEntryBalance ON dbo.JournalLines
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT l.JournalEntryId
                           FROM dbo.JournalLines l
                           WHERE l.JournalEntryId IN (SELECT JournalEntryId FROM inserted)
                           GROUP BY l.JournalEntryId
                           HAVING SUM(l.DebitBase) <> SUM(l.CreditBase))
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51001, N'القيد غير متوازن على مستوى عملة الدفاتر.', 1;
                END
            END
            """;

        // 51002 — R-GL-03 و R-LIFE-03
        private const string JournalLinePreventModification = """
            CREATE TRIGGER TR_JournalLine_PreventModification ON dbo.JournalLines
            INSTEAD OF UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51002, N'سطر القيد المرحَّل لا يُعدَّل ولا يُحذف. التصحيح بقيد عكسي.', 1;
            END
            """;
    }
}
