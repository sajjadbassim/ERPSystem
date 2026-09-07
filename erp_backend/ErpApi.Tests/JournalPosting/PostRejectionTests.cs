using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.JournalPosting;

public class PostRejectionTests(TestDatabase database) : PostingTestBase(database)
{
    // B1 — قيد بعملة الأساس وحدها لا تحدث فيه أي تحويل، فأي فرق خلل لا تقريب
    [Fact]
    public async Task B01_PostAsync_BaseCurrencyOnlyAndUnbalanced_ThrowsUnbalanced()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.Unbalanced, scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000_000m),
            scenario.Credit(scenario.RevenueAccountId, 900_000m)));
    }

    // B2 — R-GL-06: تجاوز الحد رفض لا امتصاص
    [Fact]
    public async Task B02_PostAsync_ResidualExceedsTolerance_ThrowsResidualExceeded()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.ResidualExceedsTolerance, scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.RevenueAccountId, 1_320_050m)));
    }

    // B3 — R-GL-07: باقٍ بلا حساب دور 5 يُرفض ولا يُجتهد له بديل
    [Fact]
    public async Task B03_PostAsync_ResidualWithoutRoundingAccount_ThrowsRoundingAccountMissing()
    {
        var scenario = await NewScenarioAsync(new ScenarioOptions { IncludeRoundingAccount = false });

        await AssertRejectsAsync(PostingErrors.RoundingAccountMissing, scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000m, scenario.UsdCurrencyId, 1_320.4567m),
            scenario.Credit(scenario.RevenueAccountId, 1_320_456.7001m)));
    }

    // B4 — R-GL-04
    [Fact]
    public async Task B04_PostAsync_ClosedPeriod_ThrowsPeriodClosed()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.PostingDate = Scenario.ClosedPeriodDate;
        request.DocumentDate = Scenario.ClosedPeriodDate;
        request.FiscalPeriodId = scenario.ClosedPeriodId;

        await AssertRejectsAsync(PostingErrors.PeriodClosed, request);
    }

    // B5 — سنة مقفلة وفترتها مفتوحة: حالة مستقلة عن B4
    [Fact]
    public async Task B05_PostAsync_OpenPeriodInsideClosedYear_ThrowsFiscalYearClosed()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.PostingDate = Scenario.ClosedYearDate;
        request.DocumentDate = Scenario.ClosedYearDate;
        request.FiscalPeriodId = scenario.OpenPeriodInClosedYearId;

        await AssertRejectsAsync(PostingErrors.FiscalYearClosed, request);
    }

    // B6
    [Fact]
    public async Task B06_PostAsync_PostingDateOutsideNamedPeriod_ThrowsOutsidePeriod()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.FiscalPeriodId = scenario.RegularPeriodIds[6];

        await AssertRejectsAsync(PostingErrors.PostingDateOutsidePeriod, request);
    }

    // B7
    [Fact]
    public async Task B07_PostAsync_PeriodFromAnotherCompany_ThrowsCrossCompanyPeriod()
    {
        var scenario = await NewScenarioAsync();
        var other = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.FiscalPeriodId = other.OpenPeriodId;

        await AssertRejectsAsync(PostingErrors.PeriodBelongsToAnotherCompany, request);
    }

    // B8
    [Fact]
    public async Task B08_PostAsync_AccountFromAnotherCompany_ThrowsCrossCompanyAccount()
    {
        var scenario = await NewScenarioAsync();
        var other = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.AccountBelongsToAnotherCompany, scenario.NewPost(
            scenario.Debit(other.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B9
    [Fact]
    public async Task B09_PostAsync_HeaderAccount_ThrowsNotPostable()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.AccountNotPostable, scenario.NewPost(
            scenario.Debit(scenario.HeaderAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B10
    [Fact]
    public async Task B10_PostAsync_InactiveAccount_ThrowsAccountInactive()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.AccountInactive, scenario.NewPost(
            scenario.Debit(scenario.InactiveAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B10ب — الحساب المحذوف منطقياً يُرفض بنفس السبب
    [Fact]
    public async Task B10b_PostAsync_SoftDeletedAccount_ThrowsAccountInactive()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.AccountInactive, scenario.NewPost(
            scenario.Debit(scenario.DeletedAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B11
    [Fact]
    public async Task B11_PostAsync_LineCurrencyConflictsWithRestrictedAccount_Throws()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.LineCurrencyConflictsWithAccount, scenario.NewPost(
            scenario.Debit(scenario.UsdBankAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B12
    [Fact]
    public async Task B12_PostAsync_BaseCurrencyWithRateOtherThanOne_Throws()
    {
        var scenario = await NewScenarioAsync();
        var debit = scenario.Debit(scenario.CashAccountId, 100m);
        debit.ExchangeRate = 1.5m;
        debit.DebitBase = 150m;

        await AssertRejectsAsync(PostingErrors.BaseCurrencyRateMustBeOne,
            scenario.NewPost(debit, scenario.Credit(scenario.RevenueAccountId, 150m)));
    }

    // B13 — عملة الأساس بسعر 1 لكن FC ≠ Base
    [Fact]
    public async Task B13_PostAsync_BaseCurrencyAmountsDoNotMatch_Throws()
    {
        var scenario = await NewScenarioAsync();
        var debit = scenario.Debit(scenario.CashAccountId, 100m);
        debit.DebitBase = 200m;

        await AssertRejectsAsync(PostingErrors.BaseCurrencyAmountsMustMatch,
            scenario.NewPost(debit, scenario.Credit(scenario.RevenueAccountId, 200m)));
    }

    // B14
    [Fact]
    public async Task B14_PostAsync_LineWithBothDebitAndCredit_Throws()
    {
        var scenario = await NewScenarioAsync();
        var line = scenario.Debit(scenario.CashAccountId, 100m);
        line.CreditFC = 50m;
        line.CreditBase = 50m;

        await AssertRejectsAsync(PostingErrors.DebitCreditInvalid,
            scenario.NewPost(line, scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B15
    [Fact]
    public async Task B15_PostAsync_ZeroValueLine_Throws()
    {
        var scenario = await NewScenarioAsync();
        var zero = scenario.Debit(scenario.CashAccountId, 0m);

        await AssertRejectsAsync(PostingErrors.DebitCreditInvalid, scenario.NewPost(
            zero,
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B16 — R-AMT-02: المبلغ بعملة الدفاتر لا يطابق حاصل التحويل
    [Fact]
    public async Task B16_PostAsync_BaseNotEqualConvertedAmount_Throws()
    {
        var scenario = await NewScenarioAsync();
        var debit = scenario.Debit(scenario.CashAccountId, 100m, scenario.UsdCurrencyId, 1_320m);
        debit.DebitBase = 130_000m;

        await AssertRejectsAsync(PostingErrors.BaseNotEqualConverted,
            scenario.NewPost(debit, scenario.Credit(scenario.RevenueAccountId, 130_000m)));
    }

    // B20 — الحالة الأخطر: SUM على مجموعة فارغة يُرجع NULL لا صفراً
    [Fact]
    public async Task B20_PostAsync_NoLines_ThrowsNoLines()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.NoLines, scenario.NewPost());
    }

    // B21
    [Fact]
    public async Task B21_PostAsync_SingleLine_ThrowsSingleLine()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.SingleLine,
            scenario.NewPost(scenario.Debit(scenario.CashAccountId, 100m)));
    }

    // B22
    [Fact]
    public async Task B22_PostAsync_DuplicateLineNumber_Throws()
    {
        var scenario = await NewScenarioAsync();
        var first = scenario.Debit(scenario.CashAccountId, 100m);
        var second = scenario.Credit(scenario.RevenueAccountId, 100m);
        first.LineNumber = 1;
        second.LineNumber = 1;

        await AssertRejectsAsync(PostingErrors.DuplicateLineNumber, scenario.NewPost(first, second));
    }

    // B23 — R-TRC-01: الصفر ليس هوية، وNOT NULL لا يكفي
    [Fact]
    public async Task B23_PostAsync_EmptyTransactionId_Throws()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.TransactionId = Guid.Empty;

        await AssertRejectsAsync(PostingErrors.EmptyTransactionId, request);
    }

    // B24
    [Fact]
    public async Task B24_PostAsync_NonPositiveExchangeRate_Throws()
    {
        var scenario = await NewScenarioAsync();
        var debit = scenario.Debit(scenario.CashAccountId, 100m, scenario.UsdCurrencyId, 1_320m);
        debit.ExchangeRate = 0m;

        await AssertRejectsAsync(PostingErrors.NonPositiveExchangeRate,
            scenario.NewPost(debit, scenario.Credit(scenario.RevenueAccountId, 132_000m)));
    }

    // B25 — لا إنشاء تلقائي لصف العدّاد: الإنشاء وقت الطلب يعيد سباق أول استخدام
    [Fact]
    public async Task B25_PostAsync_BranchWithoutSequence_ThrowsSequenceNotConfigured()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.BranchId = scenario.BranchWithoutSequenceId;

        await AssertRejectsAsync(PostingErrors.NumberSequenceNotConfigured, request);
    }

    // B26 — رسالة واضحة بدل انتهاك FK خام
    [Fact]
    public async Task B26_PostAsync_UnknownAccount_ThrowsEntityNotFound()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.ReferencedEntityNotFound, scenario.NewPost(
            scenario.Debit(Guid.CreateVersion7(), 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B27 — الاستنتاج لا يلتقط فترات التسويات أبداً
    [Fact]
    public async Task B27_PostAsync_NullPeriodAndDateOutsideAnyRegularPeriod_Throws()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.FiscalPeriodId = null;
        request.PostingDate = new DateOnly(2027, 6, 15);
        request.DocumentDate = new DateOnly(2027, 6, 15);

        await AssertRejectsAsync(PostingErrors.NoRegularPeriodForDate, request);
    }

    // B28 — سطر غبار: مبلغ بعملة المعاملة موجب يتلاشى إلى صفر بعملة الدفاتر
    [Fact]
    public async Task B28_PostAsync_DustLineRoundingToZeroBase_Throws()
    {
        var scenario = await NewScenarioAsync();
        var dust = scenario.Debit(scenario.CashAccountId, 0.0001m, scenario.UsdCurrencyId, 0.0001m);

        await AssertRejectsAsync(PostingErrors.DebitCreditInvalid, scenario.NewPost(
            dust,
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));
    }

    // B29 — R-FX-05: سعران لنفس العملة في قيد غير تسوية صرف
    [Fact]
    public async Task B29_PostAsync_SameCurrencyTwoRatesOutsideFxAdjustment_Throws()
    {
        var scenario = await NewScenarioAsync();

        await AssertRejectsAsync(PostingErrors.SameCurrencyDifferentRates, scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.UsdBankAccountId, 100m, scenario.UsdCurrencyId, 1_310m),
            scenario.Credit(scenario.RealizedFxGainAccountId, 1_000m)));
    }

    // قرار ٣ — وصف إلزامي للقيد اليدوي
    [Fact]
    public async Task B30_PostAsync_ManualEntryWithoutDescription_Throws()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.SourceModule = 1;
        request.Description = null;

        await AssertRejectsAsync(PostingErrors.ManualDescriptionRequired, request);
    }

    [Fact]
    public async Task B30b_PostAsync_ManualEntryWithTooShortDescription_Throws()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.SourceModule = 1;
        request.Description = "قيد";

        await AssertRejectsAsync(PostingErrors.ManualDescriptionRequired, request);
    }

    // قرار ٢ — تاريخ المستند بعد تاريخ الترحيل
    [Fact]
    public async Task B31_PostAsync_DocumentDateAfterPostingDate_Throws()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        request.DocumentDate = request.PostingDate.AddDays(1);

        await AssertRejectsAsync(PostingErrors.DocumentDateAfterPostingDate, request);
    }

    // B17 — R-GL-03: قيد واحد لا يُعكس مرتين
    [Fact]
    public async Task B17_ReverseAsync_AlreadyReversedEntry_Throws()
    {
        var scenario = await NewScenarioAsync();
        var original = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        await PostAsync(original);

        await ReverseAsync(NewReversal(scenario, original.JournalEntryId));

        await AssertReverseRejectsAsync(PostingErrors.ReversalOriginalAlreadyReversed,
            NewReversal(scenario, original.JournalEntryId));
    }

    // B18 — لا عكس لعكس
    [Fact]
    public async Task B18_ReverseAsync_ReversalEntry_Throws()
    {
        var scenario = await NewScenarioAsync();
        var original = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        await PostAsync(original);

        var reversal = NewReversal(scenario, original.JournalEntryId);
        await ReverseAsync(reversal);

        await AssertReverseRejectsAsync(PostingErrors.ReversalOfReversal,
            NewReversal(scenario, reversal.JournalEntryId));
    }

    // B19
    [Fact]
    public async Task B19_ReverseAsync_OriginalFromAnotherCompany_Throws()
    {
        var scenario = await NewScenarioAsync();
        var other = await NewScenarioAsync();
        var foreign = other.NewPost(
            other.Debit(other.CashAccountId, 100m),
            other.Credit(other.RevenueAccountId, 100m));
        await PostAsync(foreign);

        await AssertReverseRejectsAsync(PostingErrors.ReversalCrossCompany,
            NewReversal(scenario, foreign.JournalEntryId));
    }

    // عدد المفاتيح المُمرَّرة يجب أن يطابق عدد سطور الأصل
    [Fact]
    public async Task B32_ReverseAsync_LineIdCountMismatch_Throws()
    {
        var scenario = await NewScenarioAsync();
        var original = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        await PostAsync(original);

        var reversal = NewReversal(scenario, original.JournalEntryId);
        reversal.LineIds = [Guid.CreateVersion7()];

        await AssertReverseRejectsAsync(PostingErrors.ReversalLineIdCountMismatch, reversal);
    }

    private static ReverseRequest NewReversal(Scenario scenario, Guid originalId) => new()
    {
        OriginalJournalEntryId = originalId,
        BranchId = scenario.BranchId,
        PostingDate = new DateOnly(2026, 7, 15),
        DocumentDate = new DateOnly(2026, 7, 15),
        CreatedByUserId = scenario.UserId,
        LineIds = [Guid.CreateVersion7(), Guid.CreateVersion7()]
    };
}
