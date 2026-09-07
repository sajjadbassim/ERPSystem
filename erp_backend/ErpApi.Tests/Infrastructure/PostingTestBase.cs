using Microsoft.Data.SqlClient;

namespace ErpApi.Tests.Infrastructure;

[Collection(TestDatabaseCollection.Name)]
public abstract class PostingTestBase(TestDatabase database)
{
    protected TestDatabase Database { get; } = database;

    protected Task<Scenario> NewScenarioAsync(ScenarioOptions? options = null) =>
        ScenarioBuilder.CreateAsync(Database, options);

    protected Task<string> PostAsync(PostRequest request) => PostingClient.PostAsync(Database, request);

    protected Task<string> ReverseAsync(ReverseRequest request) => PostingClient.ReverseAsync(Database, request);

    protected async Task AssertRejectsAsync(int expectedError, PostRequest request)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => PostAsync(request));
        Assert.Equal(expectedError, exception.Number);
    }

    protected async Task AssertReverseRejectsAsync(int expectedError, ReverseRequest request)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => ReverseAsync(request));
        Assert.Equal(expectedError, exception.Number);
    }

    protected Task<int> LineCountAsync(Guid journalEntryId) =>
        PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalLines WHERE JournalEntryId = @id",
            ("@id", journalEntryId))!;

    protected Task<bool> EntryExistsAsync(Guid journalEntryId) =>
        PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalEntries WHERE Id = @id",
            ("@id", journalEntryId))
        .ContinueWith(t => t.Result == 1);

    protected Task<bool> IsBaseCurrencyLockedAsync(Guid companyId) =>
        PostingClient.ScalarAsync<bool>(Database,
            "SELECT IsBaseCurrencyLocked FROM Companies WHERE Id = @id",
            ("@id", companyId));

    protected Task<Guid> StoredPeriodAsync(Guid journalEntryId) =>
        PostingClient.ScalarAsync<Guid>(Database,
            "SELECT FiscalPeriodId FROM JournalEntries WHERE Id = @id",
            ("@id", journalEntryId));

    protected Task<decimal> RoundingLineAmountAsync(Guid journalEntryId, Guid roundingAccountId) =>
        PostingClient.ScalarAsync<decimal>(Database,
            "SELECT DebitBase - CreditBase FROM JournalLines WHERE JournalEntryId = @id AND AccountId = @account",
            ("@id", journalEntryId), ("@account", roundingAccountId));

    protected Task ClosePeriodAsync(Guid periodId) =>
        PostingClient.ExecuteAsync(Database,
            "UPDATE FiscalPeriods SET IsClosed = 1 WHERE Id = @id", ("@id", periodId));

    // كل رأس بلا سطور خلل صامت لا يلتقطه أي تريجر — هذا هو استعلام وظيفة التوافق (R-TRC-03).
    // مقصور على شركة السيناريو: الاستعلام على مستوى القاعدة كلها يلتقط أيتاماً تُنشئها
    // اختبارات أخرى عمداً، فيتحول الاختبار إلى رهينة ترتيب التشغيل.
    protected Task<int> OrphanHeaderCountAsync(Guid companyId) =>
        PostingClient.ScalarAsync<int>(Database,
            """
            SELECT COUNT(*)
            FROM JournalEntries e
            INNER JOIN Branches b ON b.Id = e.BranchId
            WHERE b.CompanyId = @company
              AND NOT EXISTS (SELECT 1 FROM JournalLines l WHERE l.JournalEntryId = e.Id)
            """,
            ("@company", companyId))!;
}
