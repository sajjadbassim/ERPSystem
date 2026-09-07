using ErpApi.Core.Constants;
using ErpApi.Core.Models;

namespace ErpApi.Tests.Infrastructure;

// كل اختبار يبني شركته وفروعه وسنته وحساباته الخاصة، فلا يتسرب أثر اختبار إلى آخر.
// هذا لازم تحديداً لاختبارات قفل عملة الأساس، لأن أول ترحيل في الشركة يغيّر حالتها.
public static class ScenarioBuilder
{
    private static int _sequence;

    public static async Task<Scenario> CreateAsync(TestDatabase database, ScenarioOptions? options = null)
    {
        options ??= new ScenarioOptions();
        var tag = Interlocked.Increment(ref _sequence);
        var now = DateTime.UtcNow;
        var user = database.TestUserId;

        var companyId = Guid.CreateVersion7();
        var companyCode = $"CO{tag:D4}";
        var branchId = Guid.CreateVersion7();
        var secondBranchId = Guid.CreateVersion7();
        var branchWithoutSequenceId = Guid.CreateVersion7();
        var fiscalYearId = Guid.CreateVersion7();
        var closedYearId = Guid.CreateVersion7();
        var roundingAccountId = Guid.CreateVersion7();

        await using var context = database.CreateContext();

        context.Companies.Add(new Company
        {
            Id = companyId,
            Code = companyCode,
            Name = $"شركة اختبار {tag}",
            BaseCurrencyId = database.IqdCurrencyId,
            IsBaseCurrencyLocked = false,
            FxRoundingToleranceBase = options.FxRoundingToleranceBase,
            CreatedAt = now,
            CreatedByUserId = user
        });

        foreach (var (id, code, name) in new[]
                 {
                     (branchId, "BR1", "الفرع الرئيسي"),
                     (secondBranchId, "BR2", "الفرع الثاني"),
                     (branchWithoutSequenceId, "BR3", "فرع بلا عدّاد")
                 })
        {
            context.Branches.Add(new Branch
            {
                Id = id,
                CompanyId = companyId,
                Code = code,
                Name = name,
                CreatedAt = now,
                CreatedByUserId = user
            });
        }

        context.FiscalYears.Add(new FiscalYear
        {
            Id = fiscalYearId,
            CompanyId = companyId,
            Code = "FY2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsClosed = false,
            CreatedAt = now,
            CreatedByUserId = user
        });

        // السنة مقفلة بينما فترتها مفتوحة، لعزل «سنة مقفلة» عن «فترة مقفلة» في الاختبار
        context.FiscalYears.Add(new FiscalYear
        {
            Id = closedYearId,
            CompanyId = companyId,
            Code = "FY2025",
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            IsClosed = true,
            CreatedAt = now,
            CreatedByUserId = user
        });

        var periodIds = new Guid[13];
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateOnly(2026, month, 1);
            periodIds[month - 1] = Guid.CreateVersion7();

            context.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = periodIds[month - 1],
                FiscalYearId = fiscalYearId,
                PeriodNumber = (byte)month,
                PeriodType = FiscalPeriodType.Regular,
                Name = $"فترة {month:D2}/2026",
                StartDate = start,
                EndDate = start.AddMonths(1).AddDays(-1),
                IsClosed = month == 1,
                CreatedAt = now,
                CreatedByUserId = user
            });
        }

        // فترة التسويات تتداخل عمداً مع الفترة العادية 12 في اليوم الأخير
        periodIds[12] = Guid.CreateVersion7();
        context.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = periodIds[12],
            FiscalYearId = fiscalYearId,
            PeriodNumber = 13,
            PeriodType = FiscalPeriodType.Adjustment,
            Name = "فترة تسويات 2026",
            StartDate = new DateOnly(2026, 12, 31),
            EndDate = new DateOnly(2026, 12, 31),
            IsClosed = false,
            CreatedAt = now,
            CreatedByUserId = user
        });

        var openPeriodInClosedYearId = Guid.CreateVersion7();
        context.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = openPeriodInClosedYearId,
            FiscalYearId = closedYearId,
            PeriodNumber = 1,
            PeriodType = FiscalPeriodType.Regular,
            Name = "فترة 01/2025",
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 1, 31),
            IsClosed = false,
            CreatedAt = now,
            CreatedByUserId = user
        });

        var cashId = Guid.CreateVersion7();
        var revenueId = Guid.CreateVersion7();
        var headerId = Guid.CreateVersion7();
        var inactiveId = Guid.CreateVersion7();
        var deletedId = Guid.CreateVersion7();
        var usdBankId = Guid.CreateVersion7();
        var fxGainId = Guid.CreateVersion7();
        var fxLossId = Guid.CreateVersion7();

        context.Accounts.AddRange(
            NewAccount(fxGainId, companyId, "4901", "أرباح فروقات صرف محققة", AccountType.Revenue, NormalBalance.Credit, now, user, systemRole: SystemAccountRole.RealizedFxGain),
            NewAccount(fxLossId, companyId, "5902", "خسائر فروقات صرف محققة", AccountType.Expense, NormalBalance.Debit, now, user, systemRole: SystemAccountRole.RealizedFxLoss),
            NewAccount(cashId, companyId, "1101", "الصندوق", AccountType.Asset, NormalBalance.Debit, now, user),
            NewAccount(revenueId, companyId, "4101", "إيرادات المبيعات", AccountType.Revenue, NormalBalance.Credit, now, user),
            NewAccount(headerId, companyId, "1000", "الأصول", AccountType.Asset, NormalBalance.Debit, now, user, isPostable: false),
            NewAccount(inactiveId, companyId, "1102", "حساب معطَّل", AccountType.Asset, NormalBalance.Debit, now, user, isActive: false),
            NewAccount(deletedId, companyId, "1103", "حساب محذوف", AccountType.Asset, NormalBalance.Debit, now, user, isDeleted: true),
            NewAccount(usdBankId, companyId, "1201", "بنك بالدولار", AccountType.Asset, NormalBalance.Debit, now, user, currencyId: database.UsdCurrencyId));

        if (options.IncludeRoundingAccount)
        {
            context.Accounts.Add(NewAccount(roundingAccountId, companyId, "5901", "فرق تقريب الصرف",
                AccountType.Expense, NormalBalance.Debit, now, user, systemRole: SystemAccountRole.FxRoundingDifference));
        }

        foreach (var id in new[] { branchId, secondBranchId })
        {
            context.NumberSequences.Add(new NumberSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                BranchId = id,
                DocumentType = SequenceDocumentType.JournalEntry,
                FiscalYearId = null,
                Prefix = "JV-",
                CurrentValue = 0,
                PaddingLength = 6,
                CreatedAt = now,
                CreatedByUserId = user
            });
        }

        await context.SaveChangesAsync();

        return new Scenario
        {
            CompanyId = companyId,
            CompanyCode = companyCode,
            BranchId = branchId,
            SecondBranchId = secondBranchId,
            BranchWithoutSequenceId = branchWithoutSequenceId,
            FiscalYearId = fiscalYearId,
            OpenPeriodId = periodIds[5],
            ClosedPeriodId = periodIds[0],
            AdjustmentPeriodId = periodIds[12],
            ClosedYearId = closedYearId,
            OpenPeriodInClosedYearId = openPeriodInClosedYearId,
            CashAccountId = cashId,
            RevenueAccountId = revenueId,
            HeaderAccountId = headerId,
            InactiveAccountId = inactiveId,
            DeletedAccountId = deletedId,
            UsdBankAccountId = usdBankId,
            RoundingAccountId = roundingAccountId,
            RealizedFxGainAccountId = fxGainId,
            RealizedFxLossAccountId = fxLossId,
            RegularPeriodIds = periodIds[..12],
            BaseCurrencyId = database.IqdCurrencyId,
            UsdCurrencyId = database.UsdCurrencyId,
            UserId = user
        };
    }

    private static Account NewAccount(
        Guid id, Guid companyId, string code, string name, AccountType type, NormalBalance normalBalance,
        DateTime now, Guid user, bool isPostable = true, bool isActive = true, bool isDeleted = false,
        Guid? currencyId = null, SystemAccountRole? systemRole = null) =>
        new()
        {
            Id = id,
            CompanyId = companyId,
            Code = code,
            Name = name,
            AccountType = type,
            NormalBalance = normalBalance,
            IsPostable = isPostable,
            IsActive = isActive,
            IsDeleted = isDeleted,
            CurrencyId = currencyId,
            SystemAccountRole = systemRole,
            CreatedAt = now,
            CreatedByUserId = user
        };
}
