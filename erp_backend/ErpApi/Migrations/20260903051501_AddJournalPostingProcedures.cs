using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpApi.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalPostingProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ترتيب الأعمدة ملزم: SqlClient يربط أعمدة الـ TVP بالموضع لا بالاسم.
            // بلا مفتاح أساسي عمداً، حتى يصل تكرار LineNumber إلى فحص الإجراء برسالة واضحة
            // بدل أن يُرفض داخل النوع برسالة غامضة.
            migrationBuilder.Sql("""
                CREATE TYPE dbo.JournalLineInput AS TABLE
                (
                    Id               UNIQUEIDENTIFIER NOT NULL,
                    LineNumber       SMALLINT         NOT NULL,
                    AccountId        UNIQUEIDENTIFIER NOT NULL,
                    [Description]    NVARCHAR(500)    NULL,
                    CurrencyId       UNIQUEIDENTIFIER NOT NULL,
                    ExchangeRate     DECIMAL(28,12)   NOT NULL,
                    ExchangeRateDate DATE             NOT NULL,
                    DebitFC          DECIMAL(19,4)    NOT NULL,
                    CreditFC         DECIMAL(19,4)    NOT NULL,
                    DebitBase        DECIMAL(19,4)    NOT NULL,
                    CreditBase       DECIMAL(19,4)    NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE TYPE dbo.GuidList AS TABLE (Id UNIQUEIDENTIFIER NOT NULL);
                """);

            // EF Core 10 يدمج نداءات Sql() في دفعة واحدة ولا يفصلها بـ GO، و CREATE PROCEDURE
            // يجب أن يكون أول أمر في دفعته. اللف في sp_executesql يجعله أمراً واحداً صالحاً في أي موضع.
            // المتغيران باسمين مختلفين لأن الإجراءين قد يقعان في نفس الدفعة.
            migrationBuilder.Sql(
                "DECLARE @sqlPost NVARCHAR(MAX) = N'" + PostProcedure.Replace("'", "''") + "';"
                + Environment.NewLine + "EXEC sp_executesql @sqlPost;");

            migrationBuilder.Sql(
                "DECLARE @sqlReverse NVARCHAR(MAX) = N'" + ReverseProcedure.Replace("'", "''") + "';"
                + Environment.NewLine + "EXEC sp_executesql @sqlReverse;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_JournalEntry_Reverse;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_JournalEntry_Post;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.GuidList;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.JournalLineInput;");
        }

        // المسار الوحيد للكتابة في JournalEntries و JournalLines (R-GL-05).
        // ترتيب الفحوص مقصود ومُختبَر: الأعم قبل الأخص، وقواعد عملة الأساس قبل قاعدة التحويل العامة،
        // لأن السطر المخالف يقع تحت أكثر من قاعدة والرسالة الأدق هي الأنفع.
        private const string PostProcedure = """
            CREATE PROCEDURE dbo.usp_JournalEntry_Post
                @JournalEntryId   UNIQUEIDENTIFIER,
                @TransactionId    UNIQUEIDENTIFIER,
                @BranchId         UNIQUEIDENTIFIER,
                @PostingDate      DATE,
                @DocumentDate     DATE,
                @SourceModule     TINYINT,
                @CreatedByUserId  UNIQUEIDENTIFIER,
                @Lines            dbo.JournalLineInput READONLY,
                @FiscalPeriodId   UNIQUEIDENTIFIER = NULL,
                @Description      NVARCHAR(500)    = NULL,
                @RoundingLineId   UNIQUEIDENTIFIER = NULL,
                @DocumentNumber   NVARCHAR(30)     OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @Work TABLE
                (
                    Id               UNIQUEIDENTIFIER NOT NULL,
                    LineNumber       SMALLINT         NOT NULL,
                    AccountId        UNIQUEIDENTIFIER NOT NULL,
                    [Description]    NVARCHAR(500)    NULL,
                    CurrencyId       UNIQUEIDENTIFIER NOT NULL,
                    ExchangeRate     DECIMAL(28,12)   NOT NULL,
                    ExchangeRateDate DATE             NOT NULL,
                    DebitFC          DECIMAL(19,4)    NOT NULL,
                    CreditFC         DECIMAL(19,4)    NOT NULL,
                    DebitBase        DECIMAL(19,4)    NOT NULL,
                    CreditBase       DECIMAL(19,4)    NOT NULL
                );

                INSERT INTO @Work (Id, LineNumber, AccountId, [Description], CurrencyId,
                                   ExchangeRate, ExchangeRateDate, DebitFC, CreditFC, DebitBase, CreditBase)
                SELECT Id, LineNumber, AccountId, [Description], CurrencyId,
                       ExchangeRate, ExchangeRateDate, DebitFC, CreditFC, DebitBase, CreditBase
                FROM @Lines;

                IF @TransactionId IS NULL OR @TransactionId = 0x0
                    THROW 50019, N'معرّف الحركة مطلوب، والصفر ليس هوية.', 1;

                IF @DocumentDate > @PostingDate
                    THROW 50026, N'تاريخ المستند لا يجوز أن يسبقه تاريخ الترحيل.', 1;

                IF @SourceModule = 1 AND (@Description IS NULL OR LEN(LTRIM(RTRIM(@Description))) < 5)
                    THROW 50025, N'القيد اليدوي يتطلب وصفاً لا يقل عن خمسة أحرف.', 1;

                DECLARE @LineCount INT = (SELECT COUNT(*) FROM @Work);

                IF @LineCount = 0
                    THROW 50016, N'القيد بلا سطور.', 1;

                IF @LineCount = 1
                    THROW 50017, N'القيد يتطلب سطرين على الأقل.', 1;

                IF EXISTS (SELECT LineNumber FROM @Work GROUP BY LineNumber HAVING COUNT(*) > 1)
                    THROW 50018, N'رقم السطر مكرر داخل القيد.', 1;

                DECLARE @CompanyId UNIQUEIDENTIFIER;

                SELECT @CompanyId = b.CompanyId
                FROM dbo.Branches b
                WHERE b.Id = @BranchId AND b.IsDeleted = 0;

                IF @CompanyId IS NULL
                    THROW 50022, N'الفرع غير موجود.', 1;

                DECLARE @BaseCurrencyId UNIQUEIDENTIFIER, @Tolerance DECIMAL(19,4);

                SELECT @BaseCurrencyId = c.BaseCurrencyId, @Tolerance = c.FxRoundingToleranceBase
                FROM dbo.Companies c
                WHERE c.Id = @CompanyId AND c.IsDeleted = 0;

                IF @BaseCurrencyId IS NULL
                    THROW 50022, N'الشركة غير موجودة.', 1;

                IF @FiscalPeriodId IS NULL
                BEGIN
                    SELECT @FiscalPeriodId = p.Id
                    FROM dbo.FiscalPeriods p
                    INNER JOIN dbo.FiscalYears y ON y.Id = p.FiscalYearId
                    WHERE y.CompanyId = @CompanyId
                      AND p.PeriodType = 1
                      AND p.IsDeleted = 0
                      AND y.IsDeleted = 0
                      AND @PostingDate BETWEEN p.StartDate AND p.EndDate;

                    IF @FiscalPeriodId IS NULL
                        THROW 50023, N'لا توجد فترة عادية تغطي تاريخ الترحيل.', 1;
                END

                DECLARE @PeriodCompanyId UNIQUEIDENTIFIER, @FiscalYearId UNIQUEIDENTIFIER,
                        @PeriodStart DATE, @PeriodEnd DATE, @PeriodClosed BIT, @YearClosed BIT;

                SELECT @PeriodCompanyId = y.CompanyId,
                       @FiscalYearId    = y.Id,
                       @PeriodStart     = p.StartDate,
                       @PeriodEnd       = p.EndDate,
                       @PeriodClosed    = p.IsClosed,
                       @YearClosed      = y.IsClosed
                FROM dbo.FiscalPeriods p
                INNER JOIN dbo.FiscalYears y ON y.Id = p.FiscalYearId
                WHERE p.Id = @FiscalPeriodId AND p.IsDeleted = 0;

                IF @PeriodCompanyId IS NULL
                    THROW 50022, N'الفترة المالية غير موجودة.', 1;

                IF @PeriodCompanyId <> @CompanyId
                    THROW 50007, N'الفترة المالية تخص شركة أخرى غير شركة الفرع.', 1;

                IF @PostingDate < @PeriodStart OR @PostingDate > @PeriodEnd
                    THROW 50006, N'تاريخ الترحيل خارج مدى الفترة المالية المحددة.', 1;

                IF @PeriodClosed = 1
                    THROW 50004, N'الفترة المالية مقفلة.', 1;

                IF @YearClosed = 1
                    THROW 50005, N'السنة المالية مقفلة.', 1;

                IF EXISTS (SELECT 1 FROM @Work w
                           WHERE NOT EXISTS (SELECT 1 FROM dbo.Currencies c
                                             WHERE c.Id = w.CurrencyId AND c.IsDeleted = 0))
                    THROW 50022, N'عملة السطر غير موجودة.', 1;

                IF EXISTS (SELECT 1 FROM @Work w
                           WHERE NOT EXISTS (SELECT 1 FROM dbo.Accounts a WHERE a.Id = w.AccountId))
                    THROW 50022, N'الحساب غير موجود.', 1;

                IF EXISTS (SELECT 1 FROM @Work w
                           INNER JOIN dbo.Accounts a ON a.Id = w.AccountId
                           WHERE a.CompanyId <> @CompanyId)
                    THROW 50008, N'الحساب يخص شركة أخرى غير شركة الفرع.', 1;

                IF EXISTS (SELECT 1 FROM @Work w
                           INNER JOIN dbo.Accounts a ON a.Id = w.AccountId
                           WHERE a.IsPostable = 0)
                    THROW 50009, N'لا يجوز الترحيل على حساب تجميعي.', 1;

                IF EXISTS (SELECT 1 FROM @Work w
                           INNER JOIN dbo.Accounts a ON a.Id = w.AccountId
                           WHERE a.IsActive = 0 OR a.IsDeleted = 1)
                    THROW 50010, N'الحساب معطَّل أو محذوف.', 1;

                IF EXISTS (SELECT 1 FROM @Work w
                           INNER JOIN dbo.Accounts a ON a.Id = w.AccountId
                           WHERE a.CurrencyId IS NOT NULL AND a.CurrencyId <> w.CurrencyId)
                    THROW 50011, N'عملة السطر تخالف العملة المقيَّد بها الحساب.', 1;

                IF EXISTS (SELECT 1 FROM @Work WHERE ExchangeRate <= 0)
                    THROW 50020, N'سعر الصرف يجب أن يكون موجباً.', 1;

                IF EXISTS (SELECT 1 FROM @Work WHERE CurrencyId = @BaseCurrencyId AND ExchangeRate <> 1)
                    THROW 50012, N'سطر بعملة الدفاتر يجب أن يكون سعر صرفه واحداً.', 1;

                IF EXISTS (SELECT 1 FROM @Work
                           WHERE CurrencyId = @BaseCurrencyId
                             AND (DebitFC <> DebitBase OR CreditFC <> CreditBase))
                    THROW 50013, N'سطر بعملة الدفاتر يجب أن يتساوى فيه المبلغ بعملة المعاملة والمبلغ بعملة الدفاتر.', 1;

                IF EXISTS (SELECT 1 FROM @Work
                           WHERE ABS(DebitBase  - ROUND(DebitFC  * ExchangeRate, 4)) > 0.0001
                              OR ABS(CreditBase - ROUND(CreditFC * ExchangeRate, 4)) > 0.0001)
                    THROW 50015, N'المبلغ بعملة الدفاتر لا يساوي حاصل التحويل بسعر الصرف المذكور.', 1;

                IF EXISTS (SELECT 1 FROM @Work
                           WHERE NOT ((DebitFC  > 0 AND CreditFC = 0 AND DebitBase  > 0 AND CreditBase = 0)
                                   OR (CreditFC > 0 AND DebitFC  = 0 AND CreditBase > 0 AND DebitBase  = 0)))
                    THROW 50014, N'السطر يجب أن يكون مديناً أو دائناً حصراً، بمبلغ موجب في العملتين.', 1;

                IF @SourceModule <> 4
                   AND EXISTS (SELECT CurrencyId FROM @Work
                               GROUP BY CurrencyId
                               HAVING COUNT(DISTINCT ExchangeRate) > 1)
                    THROW 50024, N'سعران مختلفان لعملة واحدة لا يُقبلان إلا في مستند تسوية صرف.', 1;

                DECLARE @Residual DECIMAL(19,4) =
                    (SELECT ISNULL(SUM(DebitBase), 0) - ISNULL(SUM(CreditBase), 0) FROM @Work);

                IF @Residual <> 0
                BEGIN
                    -- R-AMT-07-a: الامتصاص مشروط بوجود تحويل. بلا تحويل لا ينشأ تقريب، فالفرق خلل إدخال
                    IF NOT EXISTS (SELECT 1 FROM @Work WHERE CurrencyId <> @BaseCurrencyId)
                        THROW 50001, N'القيد غير متوازن. كل سطوره بعملة الدفاتر فلا مجال لباقي تقريب.', 1;

                    IF ABS(@Residual) > @Tolerance
                        THROW 50002, N'الفرق يتجاوز حد التقريب المسموح للشركة: خلل تحويل لا باقي تقريب.', 1;

                    DECLARE @RoundingAccountId UNIQUEIDENTIFIER =
                        (SELECT a.Id FROM dbo.Accounts a
                         WHERE a.CompanyId = @CompanyId AND a.SystemAccountRole = 5
                           AND a.IsDeleted = 0 AND a.IsActive = 1);

                    IF @RoundingAccountId IS NULL
                        THROW 50003, N'حساب فرق تقريب الصرف غير معرَّف لهذه الشركة.', 1;

                    INSERT INTO @Work (Id, LineNumber, AccountId, [Description], CurrencyId,
                                       ExchangeRate, ExchangeRateDate, DebitFC, CreditFC, DebitBase, CreditBase)
                    SELECT ISNULL(@RoundingLineId, NEWID()),
                           (SELECT MAX(LineNumber) + 1 FROM @Work),
                           @RoundingAccountId,
                           N'فرق تقريب صرف',
                           @BaseCurrencyId,
                           1,
                           @PostingDate,
                           CASE WHEN @Residual < 0 THEN ABS(@Residual) ELSE 0 END,
                           CASE WHEN @Residual > 0 THEN ABS(@Residual) ELSE 0 END,
                           CASE WHEN @Residual < 0 THEN ABS(@Residual) ELSE 0 END,
                           CASE WHEN @Residual > 0 THEN ABS(@Residual) ELSE 0 END;
                END

                -- إعادة تحقق دفاعية بعد الامتصاص: يجب ألا تُطلق أبداً
                IF (SELECT SUM(DebitBase) - SUM(CreditBase) FROM @Work) <> 0
                    THROW 50001, N'القيد غير متوازن بعد معالجة باقي التقريب.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    DECLARE @Prefix NVARCHAR(10), @Padding TINYINT, @NextValue BIGINT;

                    -- تحديث ذرّي: يزيد ويسحب في عملية واحدة، والقفل على الصف يبقى لنهاية المعاملة
                    UPDATE ns
                    SET ns.CurrentValue = ns.CurrentValue + 1,
                        @NextValue      = ns.CurrentValue + 1,
                        @Prefix         = ns.Prefix,
                        @Padding        = ns.PaddingLength
                    FROM dbo.NumberSequences ns
                    WHERE ns.CompanyId    = @CompanyId
                      AND ns.BranchId     = @BranchId
                      AND ns.DocumentType = 1
                      AND ns.FiscalYearId = @FiscalYearId;

                    IF @NextValue IS NULL
                        UPDATE ns
                        SET ns.CurrentValue = ns.CurrentValue + 1,
                            @NextValue      = ns.CurrentValue + 1,
                            @Prefix         = ns.Prefix,
                            @Padding        = ns.PaddingLength
                        FROM dbo.NumberSequences ns
                        WHERE ns.CompanyId    = @CompanyId
                          AND ns.BranchId     = @BranchId
                          AND ns.DocumentType = 1
                          AND ns.FiscalYearId IS NULL;

                    IF @NextValue IS NULL
                        THROW 50021, N'لا يوجد عدّاد ترقيم مهيّأ لهذا الفرع.', 1;

                    DECLARE @Digits NVARCHAR(20) = CAST(@NextValue AS NVARCHAR(20));

                    IF LEN(@Digits) > @Padding
                        RAISERROR (N'تجاوز التسلسل طول الحشو المحدد للعدّاد؛ الرقم يطول ويستمر.', 10, 1) WITH NOWAIT;

                    SET @DocumentNumber = @Prefix + RIGHT(REPLICATE(N'0', @Padding) + @Digits,
                        CASE WHEN LEN(@Digits) > @Padding THEN LEN(@Digits) ELSE @Padding END);

                    INSERT INTO dbo.JournalEntries
                        (Id, TransactionId, BranchId, FiscalPeriodId, DocumentNumber,
                         PostingDate, DocumentDate, [Description], SourceModule,
                         ReversalOfJournalEntryId, CreatedAt, CreatedByUserId)
                    VALUES
                        (@JournalEntryId, @TransactionId, @BranchId, @FiscalPeriodId, @DocumentNumber,
                         @PostingDate, @DocumentDate, @Description, @SourceModule,
                         NULL, SYSUTCDATETIME(), @CreatedByUserId);

                    -- أمر واحد لكل السطور: هذا ما يجعل تريجر التوازن يرى المجموعة كاملة دفعة واحدة
                    INSERT INTO dbo.JournalLines
                        (Id, JournalEntryId, LineNumber, AccountId, [Description], CurrencyId,
                         ExchangeRate, ExchangeRateDate, DebitFC, CreditFC, DebitBase, CreditBase, CreatedAt)
                    SELECT w.Id, @JournalEntryId, w.LineNumber, w.AccountId, w.[Description], w.CurrencyId,
                           w.ExchangeRate, w.ExchangeRateDate, w.DebitFC, w.CreditFC, w.DebitBase, w.CreditBase,
                           SYSUTCDATETIME()
                    FROM @Work w;

                    COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH
            END
            """;

        // العكس يبني سطوره من الأصل بنفسه، فلا يكون «عكساً» بالتسمية وحدها (R-GL-03).
        // العملة وسعر الصرف وتاريخه تُنسخ كما هي: العكس لا يعيد التقييم بسعر اليوم.
        private const string ReverseProcedure = """
            CREATE PROCEDURE dbo.usp_JournalEntry_Reverse
                @JournalEntryId         UNIQUEIDENTIFIER,
                @TransactionId          UNIQUEIDENTIFIER,
                @OriginalJournalEntryId UNIQUEIDENTIFIER,
                @BranchId               UNIQUEIDENTIFIER,
                @PostingDate            DATE,
                @DocumentDate           DATE,
                @CreatedByUserId        UNIQUEIDENTIFIER,
                @LineIds                dbo.GuidList READONLY,
                @FiscalPeriodId         UNIQUEIDENTIFIER = NULL,
                @Description            NVARCHAR(500)    = NULL,
                @DocumentNumber         NVARCHAR(30)     OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF @TransactionId IS NULL OR @TransactionId = 0x0
                    THROW 50019, N'معرّف الحركة مطلوب، والصفر ليس هوية.', 1;

                IF @DocumentDate > @PostingDate
                    THROW 50026, N'تاريخ المستند لا يجوز أن يسبقه تاريخ الترحيل.', 1;

                DECLARE @OriginalBranchId UNIQUEIDENTIFIER, @OriginalSourceModule TINYINT,
                        @OriginalReversalOf UNIQUEIDENTIFIER;

                SELECT @OriginalBranchId    = e.BranchId,
                       @OriginalSourceModule = e.SourceModule,
                       @OriginalReversalOf  = e.ReversalOfJournalEntryId
                FROM dbo.JournalEntries e
                WHERE e.Id = @OriginalJournalEntryId;

                IF @OriginalBranchId IS NULL
                    THROW 50022, N'القيد الأصلي غير موجود.', 1;

                IF @OriginalReversalOf IS NOT NULL
                    THROW 50028, N'لا يجوز عكس قيد عكسي.', 1;

                IF EXISTS (SELECT 1 FROM dbo.JournalEntries
                           WHERE ReversalOfJournalEntryId = @OriginalJournalEntryId)
                    THROW 50027, N'القيد الأصلي معكوس مسبقاً.', 1;

                DECLARE @CompanyId UNIQUEIDENTIFIER, @OriginalCompanyId UNIQUEIDENTIFIER;

                SELECT @CompanyId = b.CompanyId FROM dbo.Branches b WHERE b.Id = @BranchId AND b.IsDeleted = 0;

                IF @CompanyId IS NULL
                    THROW 50022, N'الفرع غير موجود.', 1;

                SELECT @OriginalCompanyId = b.CompanyId FROM dbo.Branches b WHERE b.Id = @OriginalBranchId;

                IF @OriginalCompanyId <> @CompanyId
                    THROW 50029, N'القيد الأصلي يخص شركة أخرى.', 1;

                DECLARE @OriginalLineCount INT =
                    (SELECT COUNT(*) FROM dbo.JournalLines WHERE JournalEntryId = @OriginalJournalEntryId);

                IF (SELECT COUNT(*) FROM @LineIds) <> @OriginalLineCount
                    THROW 50030, N'عدد المفاتيح الممرَّرة لا يطابق عدد سطور القيد الأصلي.', 1;

                IF @OriginalSourceModule = 1 AND (@Description IS NULL OR LEN(LTRIM(RTRIM(@Description))) < 5)
                    THROW 50025, N'عكس القيد اليدوي يتطلب وصفاً لا يقل عن خمسة أحرف.', 1;

                IF @FiscalPeriodId IS NULL
                BEGIN
                    SELECT @FiscalPeriodId = p.Id
                    FROM dbo.FiscalPeriods p
                    INNER JOIN dbo.FiscalYears y ON y.Id = p.FiscalYearId
                    WHERE y.CompanyId = @CompanyId
                      AND p.PeriodType = 1
                      AND p.IsDeleted = 0
                      AND y.IsDeleted = 0
                      AND @PostingDate BETWEEN p.StartDate AND p.EndDate;

                    IF @FiscalPeriodId IS NULL
                        THROW 50023, N'لا توجد فترة عادية تغطي تاريخ ترحيل العكس.', 1;
                END

                DECLARE @PeriodCompanyId UNIQUEIDENTIFIER, @FiscalYearId UNIQUEIDENTIFIER,
                        @PeriodStart DATE, @PeriodEnd DATE, @PeriodClosed BIT, @YearClosed BIT;

                SELECT @PeriodCompanyId = y.CompanyId,
                       @FiscalYearId    = y.Id,
                       @PeriodStart     = p.StartDate,
                       @PeriodEnd       = p.EndDate,
                       @PeriodClosed    = p.IsClosed,
                       @YearClosed      = y.IsClosed
                FROM dbo.FiscalPeriods p
                INNER JOIN dbo.FiscalYears y ON y.Id = p.FiscalYearId
                WHERE p.Id = @FiscalPeriodId AND p.IsDeleted = 0;

                IF @PeriodCompanyId IS NULL
                    THROW 50022, N'الفترة المالية غير موجودة.', 1;

                IF @PeriodCompanyId <> @CompanyId
                    THROW 50007, N'الفترة المالية تخص شركة أخرى غير شركة الفرع.', 1;

                IF @PostingDate < @PeriodStart OR @PostingDate > @PeriodEnd
                    THROW 50006, N'تاريخ الترحيل خارج مدى الفترة المالية المحددة.', 1;

                IF @PeriodClosed = 1
                    THROW 50004, N'الفترة المالية مقفلة.', 1;

                IF @YearClosed = 1
                    THROW 50005, N'السنة المالية مقفلة.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    DECLARE @Prefix NVARCHAR(10), @Padding TINYINT, @NextValue BIGINT;

                    UPDATE ns
                    SET ns.CurrentValue = ns.CurrentValue + 1,
                        @NextValue      = ns.CurrentValue + 1,
                        @Prefix         = ns.Prefix,
                        @Padding        = ns.PaddingLength
                    FROM dbo.NumberSequences ns
                    WHERE ns.CompanyId    = @CompanyId
                      AND ns.BranchId     = @BranchId
                      AND ns.DocumentType = 1
                      AND ns.FiscalYearId = @FiscalYearId;

                    IF @NextValue IS NULL
                        UPDATE ns
                        SET ns.CurrentValue = ns.CurrentValue + 1,
                            @NextValue      = ns.CurrentValue + 1,
                            @Prefix         = ns.Prefix,
                            @Padding        = ns.PaddingLength
                        FROM dbo.NumberSequences ns
                        WHERE ns.CompanyId    = @CompanyId
                          AND ns.BranchId     = @BranchId
                          AND ns.DocumentType = 1
                          AND ns.FiscalYearId IS NULL;

                    IF @NextValue IS NULL
                        THROW 50021, N'لا يوجد عدّاد ترقيم مهيّأ لهذا الفرع.', 1;

                    DECLARE @Digits NVARCHAR(20) = CAST(@NextValue AS NVARCHAR(20));

                    IF LEN(@Digits) > @Padding
                        RAISERROR (N'تجاوز التسلسل طول الحشو المحدد للعدّاد؛ الرقم يطول ويستمر.', 10, 1) WITH NOWAIT;

                    SET @DocumentNumber = @Prefix + RIGHT(REPLICATE(N'0', @Padding) + @Digits,
                        CASE WHEN LEN(@Digits) > @Padding THEN LEN(@Digits) ELSE @Padding END);

                    INSERT INTO dbo.JournalEntries
                        (Id, TransactionId, BranchId, FiscalPeriodId, DocumentNumber,
                         PostingDate, DocumentDate, [Description], SourceModule,
                         ReversalOfJournalEntryId, CreatedAt, CreatedByUserId)
                    VALUES
                        (@JournalEntryId, @TransactionId, @BranchId, @FiscalPeriodId, @DocumentNumber,
                         @PostingDate, @DocumentDate, @Description, @OriginalSourceModule,
                         @OriginalJournalEntryId, SYSUTCDATETIME(), @CreatedByUserId);

                    -- المدين يصبح دائناً والعكس، وكل ما عداهما منسوخ حرفياً من الأصل
                    WITH OriginalLines AS
                    (
                        SELECT l.*, ROW_NUMBER() OVER (ORDER BY l.LineNumber) AS Position
                        FROM dbo.JournalLines l
                        WHERE l.JournalEntryId = @OriginalJournalEntryId
                    ),
                    NewIds AS
                    (
                        SELECT i.Id, ROW_NUMBER() OVER (ORDER BY i.Id) AS Position
                        FROM @LineIds i
                    )
                    INSERT INTO dbo.JournalLines
                        (Id, JournalEntryId, LineNumber, AccountId, [Description], CurrencyId,
                         ExchangeRate, ExchangeRateDate, DebitFC, CreditFC, DebitBase, CreditBase, CreatedAt)
                    SELECT n.Id,
                           @JournalEntryId,
                           CAST(o.Position AS SMALLINT),
                           o.AccountId,
                           o.[Description],
                           o.CurrencyId,
                           o.ExchangeRate,
                           o.ExchangeRateDate,
                           o.CreditFC,
                           o.DebitFC,
                           o.CreditBase,
                           o.DebitBase,
                           SYSUTCDATETIME()
                    FROM OriginalLines o
                    INNER JOIN NewIds n ON n.Position = o.Position;

                    COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH
            END
            """;
    }
}
