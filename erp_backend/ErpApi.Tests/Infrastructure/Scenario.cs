namespace ErpApi.Tests.Infrastructure;

public sealed class LineDraft
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public short LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string? Description { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public DateOnly ExchangeRateDate { get; set; }
    public decimal DebitFC { get; set; }
    public decimal CreditFC { get; set; }
    public decimal DebitBase { get; set; }
    public decimal CreditBase { get; set; }
}

public sealed class PostRequest
{
    public Guid JournalEntryId { get; set; } = Guid.CreateVersion7();
    public Guid TransactionId { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public DateOnly PostingDate { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string? Description { get; set; } = "قيد اختبار آلي";
    public byte SourceModule { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public List<LineDraft> Lines { get; set; } = [];

    // مفتاح محجوز لسطر فرق التقريب إن لزم. الإجراء يولّده بنفسه فلا تعرف الخدمة عدد السطور مسبقاً،
    // وتمريره يبقي سياسة Guid v7 سارية بدل السقوط إلى NEWID العشوائي
    public Guid RoundingLineId { get; set; } = Guid.CreateVersion7();
}

public sealed class ReverseRequest
{
    public Guid JournalEntryId { get; set; } = Guid.CreateVersion7();
    public Guid TransactionId { get; set; } = Guid.CreateVersion7();
    public Guid OriginalJournalEntryId { get; set; }

    // الفرع الذي يُرحَّل فيه العكس. وجوده هو ما يجعل فحص «عكس لقيد من شركة أخرى» ممكناً أصلاً
    public Guid BranchId { get; set; }

    public DateOnly PostingDate { get; set; }
    public DateOnly DocumentDate { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public string? Description { get; set; } = "عكس قيد اختبار آلي";
    public Guid CreatedByUserId { get; set; }
    public List<Guid> LineIds { get; set; } = [];
}

public sealed class Scenario
{
    public required Guid CompanyId { get; init; }
    public required string CompanyCode { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid SecondBranchId { get; init; }
    public required Guid BranchWithoutSequenceId { get; init; }

    public required Guid FiscalYearId { get; init; }
    public required Guid OpenPeriodId { get; init; }
    public required Guid ClosedPeriodId { get; init; }
    public required Guid AdjustmentPeriodId { get; init; }
    public required Guid ClosedYearId { get; init; }
    public required Guid OpenPeriodInClosedYearId { get; init; }

    public required Guid CashAccountId { get; init; }
    public required Guid RevenueAccountId { get; init; }
    public required Guid HeaderAccountId { get; init; }
    public required Guid InactiveAccountId { get; init; }
    public required Guid DeletedAccountId { get; init; }
    public required Guid UsdBankAccountId { get; init; }
    public required Guid RoundingAccountId { get; init; }
    public required Guid RealizedFxGainAccountId { get; init; }
    public required Guid RealizedFxLossAccountId { get; init; }
    public required IReadOnlyList<Guid> RegularPeriodIds { get; init; }

    public required Guid BaseCurrencyId { get; init; }
    public required Guid UsdCurrencyId { get; init; }
    public required Guid UserId { get; init; }

    public static DateOnly DefaultPostingDate => new(2026, 6, 15);
    public static DateOnly ClosedPeriodDate => new(2026, 1, 15);
    public static DateOnly ClosedYearDate => new(2025, 1, 15);
    public static DateOnly AdjustmentDate => new(2026, 12, 31);

    // SQL Server يقرّب النصف بعيداً عن الصفر، وMath.Round الافتراضي مصرفي.
    // الاختلاف بينهما ينتج فروقاً تُرفض بـ CK_JournalLine_BaseEqualsConverted
    public static decimal SqlRound(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    public PostRequest NewPost(params LineDraft[] lines)
    {
        var request = new PostRequest
        {
            BranchId = BranchId,
            PostingDate = DefaultPostingDate,
            DocumentDate = DefaultPostingDate,
            CreatedByUserId = UserId,
            Lines = [.. lines]
        };

        for (short i = 0; i < request.Lines.Count; i++)
        {
            if (request.Lines[i].LineNumber == 0)
            {
                request.Lines[i].LineNumber = (short)(i + 1);
            }
        }

        return request;
    }

    public LineDraft Debit(Guid accountId, decimal amountFc, Guid? currencyId = null, decimal rate = 1m) =>
        new()
        {
            AccountId = accountId,
            CurrencyId = currencyId ?? BaseCurrencyId,
            ExchangeRate = rate,
            ExchangeRateDate = DefaultPostingDate,
            DebitFC = amountFc,
            DebitBase = SqlRound(amountFc * rate)
        };

    public LineDraft Credit(Guid accountId, decimal amountFc, Guid? currencyId = null, decimal rate = 1m) =>
        new()
        {
            AccountId = accountId,
            CurrencyId = currencyId ?? BaseCurrencyId,
            ExchangeRate = rate,
            ExchangeRateDate = DefaultPostingDate,
            CreditFC = amountFc,
            CreditBase = SqlRound(amountFc * rate)
        };
}

public sealed class ScenarioOptions
{
    public bool IncludeRoundingAccount { get; set; } = true;
    public decimal FxRoundingToleranceBase { get; set; } = 1.0000m;
}
