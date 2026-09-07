using ErpApi.Tests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace ErpApi.Tests.JournalPosting;

// الدفاع الذي يعمل حين يُلتف على الإجراء المخزَّن. الصلاحيات أولاً، والتريجر آخراً.
public class DatabaseDefenceTests(TestDatabase database) : PostingTestBase(database)
{
    // D1 — R-GL-05: حساب التطبيق لا يملك الكتابة المباشرة، بل EXECUTE على الإجراء فقط
    [Fact]
    public async Task D01_DirectInsert_AsApplicationUser_IsDeniedByPermissions()
    {
        var scenario = await NewScenarioAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            """
            EXECUTE AS USER = 'erp_app';
            BEGIN TRY
                INSERT INTO JournalLines (Id, JournalEntryId, LineNumber, AccountId, CurrencyId,
                                          ExchangeRate, ExchangeRateDate, DebitFC, CreditFC, DebitBase, CreditBase)
                VALUES (NEWID(), NEWID(), 1, @account, @currency, 1, '2026-06-15', 1, 0, 1, 0);
            END TRY
            BEGIN CATCH
                REVERT;
                THROW;
            END CATCH;
            REVERT;
            """,
            ("@account", scenario.CashAccountId), ("@currency", scenario.BaseCurrencyId)));

        Assert.Equal(PostingErrors.PermissionDenied, exception.Number);
    }

    // D2 — R-GL-02: تريجر التوازن يرفض سطراً واحداً غير متوازن حتى من db_owner
    [Fact]
    public async Task D02_DirectInsert_SingleUnbalancedLine_IsRejectedByBalanceTrigger()
    {
        var scenario = await NewScenarioAsync();
        var entryId = Guid.CreateVersion7();

        await InsertHeaderDirectlyAsync(scenario, entryId, "JV-D00002");

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            """
            INSERT INTO JournalLines (Id, JournalEntryId, LineNumber, AccountId, CurrencyId,
                                      ExchangeRate, ExchangeRateDate, DebitFC, CreditFC, DebitBase, CreditBase)
            VALUES (NEWID(), @entry, 1, @account, @currency, 1, '2026-06-15', 100, 0, 100, 0);
            """,
            ("@entry", entryId), ("@account", scenario.CashAccountId), ("@currency", scenario.BaseCurrencyId)));

        Assert.Equal(PostingErrors.TriggerUnbalanced, exception.Number);
    }

    // D3 — R-GL-03: المرحَّل لا يُعدَّل ولا يُحذف، ولو كان المنفِّذ db_owner
    [Fact]
    public async Task D03_DirectUpdate_OnPostedLine_IsRejectedByTrigger()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        await PostAsync(request);

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            "UPDATE JournalLines SET DebitBase = 999 WHERE JournalEntryId = @entry",
            ("@entry", request.JournalEntryId)));

        Assert.Equal(PostingErrors.TriggerModificationForbidden, exception.Number);
    }

    [Fact]
    public async Task D03b_DirectDelete_OnPostedEntry_IsRejectedByTrigger()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        await PostAsync(request);

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            "DELETE FROM JournalEntries WHERE Id = @entry", ("@entry", request.JournalEntryId)));

        Assert.Equal(PostingErrors.TriggerModificationForbidden, exception.Number);
    }

    // D4 — الثغرة المعلنة: رأس بلا سطور لا يُطلق أي تريجر.
    // الحارس الوحيد المتبقي هو استعلام وظيفة التوافق (R-TRC-03)
    [Fact]
    public async Task D04_OrphanHeader_IsInvisibleToTriggersButDetectedByReconciliationQuery()
    {
        var scenario = await NewScenarioAsync();
        var entryId = Guid.CreateVersion7();

        await InsertHeaderDirectlyAsync(scenario, entryId, "JV-D00004");

        Assert.True(await EntryExistsAsync(entryId));
        Assert.Equal(1, await OrphanHeaderCountAsync(scenario.CompanyId));
    }

    // D5 — R-BASE-02: القفل لا يُخفَض بأي مسار
    [Fact]
    public async Task D05_DirectUpdate_LoweringBaseCurrencyLock_IsRejectedByTrigger()
    {
        var scenario = await NewScenarioAsync();
        await PostAsync(scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            "UPDATE Companies SET IsBaseCurrencyLocked = 0 WHERE Id = @id", ("@id", scenario.CompanyId)));

        Assert.Equal(PostingErrors.TriggerBaseCurrencyUnlockForbidden, exception.Number);
    }

    // D6 — حذف صف العدّاد يعيد الترقيم من الصفر
    [Fact]
    public async Task D06_DirectDelete_OnNumberSequence_IsRejectedByTrigger()
    {
        var scenario = await NewScenarioAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            "DELETE FROM NumberSequences WHERE BranchId = @branch", ("@branch", scenario.BranchId)));

        Assert.Equal(PostingErrors.TriggerSequenceDeleteForbidden, exception.Number);
    }

    private Task InsertHeaderDirectlyAsync(Scenario scenario, Guid entryId, string documentNumber) =>
        PostingClient.ExecuteAsync(Database,
            """
            INSERT INTO JournalEntries (Id, TransactionId, BranchId, FiscalPeriodId, DocumentNumber,
                                        PostingDate, DocumentDate, Description, SourceModule, CreatedByUserId)
            VALUES (@id, NEWID(), @branch, @period, @number, '2026-06-15', '2026-06-15', N'إدراج مباشر', 1, @user);
            """,
            ("@id", entryId), ("@branch", scenario.BranchId), ("@period", scenario.OpenPeriodId),
            ("@number", documentNumber), ("@user", scenario.UserId));
}
