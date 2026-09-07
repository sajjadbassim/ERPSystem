using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.JournalPosting;

public class PostSuccessTests(TestDatabase database) : PostingTestBase(database)
{
    // A1 — R-GL-01, R-AMT-02
    [Fact]
    public async Task A01_PostAsync_BalancedEntryInBaseCurrency_CreatesTwoLines()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000_000m),
            scenario.Credit(scenario.RevenueAccountId, 1_000_000m));

        var documentNumber = await PostAsync(request);

        Assert.StartsWith("JV-", documentNumber);
        Assert.Equal(2, await LineCountAsync(request.JournalEntryId));
    }

    // A2 — R-GL-01: التوازن على Base وحده، وتوازن FC لا يُفحص
    [Fact]
    public async Task A02_PostAsync_MultiCurrencyBalancedOnBase_Succeeds()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.RevenueAccountId, 1_320_000m));

        await PostAsync(request);

        Assert.Equal(2, await LineCountAsync(request.JournalEntryId));
    }

    // A3 — R-AMT-07, R-GL-07: باقي تقريب داخل الحد يُمتص بسطر الدور 5
    [Fact]
    public async Task A03_PostAsync_RoundingResidualWithinTolerance_GeneratesRoundingLine()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000m, scenario.UsdCurrencyId, 1_320.4567m),
            scenario.Credit(scenario.RevenueAccountId, 1_320_456.7001m));

        await PostAsync(request);

        Assert.Equal(3, await LineCountAsync(request.JournalEntryId));
        Assert.Equal(0.0001m, await RoundingLineAmountAsync(request.JournalEntryId, scenario.RoundingAccountId));
    }

    // A4 — الفترة المتداخلة تُختار صراحة، والتاريخ وحده لا يحسم
    [Fact]
    public async Task A04_PostAsync_AdjustmentPeriodRequestedExplicitly_Succeeds()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 500m),
            scenario.Credit(scenario.RevenueAccountId, 500m));
        request.PostingDate = Scenario.AdjustmentDate;
        request.DocumentDate = Scenario.AdjustmentDate;
        request.FiscalPeriodId = scenario.AdjustmentPeriodId;

        await PostAsync(request);

        Assert.Equal(scenario.AdjustmentPeriodId, await StoredPeriodAsync(request.JournalEntryId));
    }

    // A5 — R-GL-03 + R-GL-04: قفل فترة الأصل لا يمنع عكسه في فترة مفتوحة
    [Fact]
    public async Task A05_ReverseAsync_OriginalInClosedPeriod_PostsIntoOpenPeriod()
    {
        var scenario = await NewScenarioAsync();
        var original = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 750m),
            scenario.Credit(scenario.RevenueAccountId, 750m));
        await PostAsync(original);

        await ClosePeriodAsync(scenario.OpenPeriodId);

        var reversal = new ReverseRequest
        {
            OriginalJournalEntryId = original.JournalEntryId,
            BranchId = scenario.BranchId,
            PostingDate = new DateOnly(2026, 7, 15),
            DocumentDate = new DateOnly(2026, 7, 15),
            CreatedByUserId = scenario.UserId,
            LineIds = [Guid.CreateVersion7(), Guid.CreateVersion7()]
        };

        await ReverseAsync(reversal);

        Assert.Equal(scenario.RegularPeriodIds[6], await StoredPeriodAsync(reversal.JournalEntryId));
        Assert.Equal(2, await LineCountAsync(reversal.JournalEntryId));
    }

    // A5ب — العكس ينسخ العملة والسعر وتاريخه من الأصل ولا يعيد التحويل بسعر اليوم
    [Fact]
    public async Task A14_ReverseAsync_CopiesOriginalRateAndDateUnchanged()
    {
        var scenario = await NewScenarioAsync();
        var original = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.RevenueAccountId, 1_320_000m));
        await PostAsync(original);

        var reversal = new ReverseRequest
        {
            OriginalJournalEntryId = original.JournalEntryId,
            BranchId = scenario.BranchId,
            PostingDate = new DateOnly(2026, 7, 15),
            DocumentDate = new DateOnly(2026, 7, 15),
            CreatedByUserId = scenario.UserId,
            LineIds = [Guid.CreateVersion7(), Guid.CreateVersion7()]
        };
        await ReverseAsync(reversal);

        var rate = await PostingClient.ScalarAsync<decimal>(Database,
            """
            SELECT ExchangeRate FROM JournalLines
            WHERE JournalEntryId = @id AND CurrencyId = @currency
            """,
            ("@id", reversal.JournalEntryId), ("@currency", scenario.UsdCurrencyId));

        Assert.Equal(1_320m, rate);
    }

    // A6 — R-BASE-02, R-BASE-03: أول ترحيل يرفع القفل
    [Fact]
    public async Task A06_PostAsync_FirstEntryForCompany_LocksBaseCurrency()
    {
        var scenario = await NewScenarioAsync();
        Assert.False(await IsBaseCurrencyLockedAsync(scenario.CompanyId));

        await PostAsync(scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));

        Assert.True(await IsBaseCurrencyLockedAsync(scenario.CompanyId));
    }

    // A7 — الترحيل الثاني لا يخطئ ولا يغيّر القفل
    [Fact]
    public async Task A07_PostAsync_SecondEntry_KeepsLockAndDoesNotFail()
    {
        var scenario = await NewScenarioAsync();
        await PostAsync(scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));

        await PostAsync(scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 200m),
            scenario.Credit(scenario.RevenueAccountId, 200m)));

        Assert.True(await IsBaseCurrencyLockedAsync(scenario.CompanyId));
    }

    // A8 — حساب بلا تقييد عملة يقبل أي عملة معاملة
    [Fact]
    public async Task A08_PostAsync_UnrestrictedAccountWithForeignCurrency_Succeeds()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.RevenueAccountId, 132_000m));

        await PostAsync(request);

        Assert.Equal(2, await LineCountAsync(request.JournalEntryId));
    }

    // A9 — R-FX-05: تاريخ السعر حقيقة مستقلة عن تاريخ الترحيل
    [Fact]
    public async Task A09_PostAsync_ExchangeRateDateEarlierThanPostingDate_Succeeds()
    {
        var scenario = await NewScenarioAsync();
        var debit = scenario.Debit(scenario.CashAccountId, 100m, scenario.UsdCurrencyId, 1_320m);
        debit.ExchangeRateDate = Scenario.DefaultPostingDate.AddDays(-2);

        var request = scenario.NewPost(debit, scenario.Credit(scenario.RevenueAccountId, 132_000m));

        await PostAsync(request);

        Assert.Equal(2, await LineCountAsync(request.JournalEntryId));
    }

    // A10 — الاستنتاج يختار الفترة العادية لا فترة التسويات
    [Fact]
    public async Task A10_PostAsync_NullPeriod_ResolvesRegularPeriodContainingPostingDate()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.FiscalPeriodId = null;

        await PostAsync(request);

        Assert.Equal(scenario.OpenPeriodId, await StoredPeriodAsync(request.JournalEntryId));
    }

    // A11 — باقٍ صفر لا يولّد سطر تقريب: سطر بصفر يخالف CK_JournalLine_DebitXorCredit
    [Fact]
    public async Task A11_PostAsync_ZeroResidual_DoesNotGenerateRoundingLine()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));

        await PostAsync(request);

        Assert.Equal(2, await LineCountAsync(request.JournalEntryId));
    }

    // A12 — حدّي: الباقي يساوي الحد تماماً، والمقارنة ≤ لا <
    [Fact]
    public async Task A12_PostAsync_ResidualExactlyAtTolerance_IsAbsorbed()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.RevenueAccountId, 1_320_001m));

        await PostAsync(request);

        Assert.Equal(3, await LineCountAsync(request.JournalEntryId));
        Assert.Equal(1.0000m, await RoundingLineAmountAsync(request.JournalEntryId, scenario.RoundingAccountId));
    }

    // A13 — سعران لنفس العملة مسموحان في مستند تسوية الصرف، وهو غرضه
    [Fact]
    public async Task A13_PostAsync_SameCurrencyDifferentRates_AllowedForFxAdjustment()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.UsdBankAccountId, 100m, scenario.UsdCurrencyId, 1_310m),
            scenario.Credit(scenario.RealizedFxGainAccountId, 1_000m));
        request.SourceModule = 4;

        await PostAsync(request);

        Assert.Equal(3, await LineCountAsync(request.JournalEntryId));
    }
}
