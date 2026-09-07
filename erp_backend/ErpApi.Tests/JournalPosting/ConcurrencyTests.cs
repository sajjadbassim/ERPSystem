using ErpApi.Tests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace ErpApi.Tests.JournalPosting;

public class ConcurrencyTests(TestDatabase database) : PostingTestBase(database)
{
    // C1 — ترحيلان متزامنان على نفس الفرع لا ينتجان رقماً واحداً
    [Fact]
    public async Task C01_PostAsync_TwoConcurrentPostingsOnSameBranch_ProduceDistinctNumbers()
    {
        var scenario = await NewScenarioAsync();

        var first = PostAsync(scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m)));

        var second = PostAsync(scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 200m),
            scenario.Credit(scenario.RevenueAccountId, 200m)));

        var numbers = await Task.WhenAll(first, second);

        Assert.Equal(2, numbers.Distinct().Count());
        Assert.Contains("JV-000001", numbers);
        Assert.Contains("JV-000002", numbers);
    }

    // C2 — تراجع المعاملة يعيد العدّاد فلا تنشأ فجوة.
    // الفشل هنا مقصود بعد سحب الرقم: مفتاح مكرر يفشل عند إدراج الرأس
    [Fact]
    public async Task C02_PostAsync_FailureAfterNumberDrawn_LeavesNoGap()
    {
        var scenario = await NewScenarioAsync();
        var first = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));

        Assert.Equal("JV-000001", await PostAsync(first));

        var duplicate = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        duplicate.JournalEntryId = first.JournalEntryId;
        await Assert.ThrowsAsync<SqlException>(() => PostAsync(duplicate));

        var third = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 300m),
            scenario.Credit(scenario.RevenueAccountId, 300m));

        Assert.Equal("JV-000002", await PostAsync(third));
    }

    // C3 — القفل على صف العدّاد لا على الجدول: فرعان مستقلان لا ينتظر أحدهما الآخر
    [Fact]
    public async Task C03_PostAsync_ConcurrentPostingsOnDifferentBranches_EachStartAtOne()
    {
        var scenario = await NewScenarioAsync();

        var onFirstBranch = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));

        var onSecondBranch = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 100m),
            scenario.Credit(scenario.RevenueAccountId, 100m));
        onSecondBranch.BranchId = scenario.SecondBranchId;

        var numbers = await Task.WhenAll(PostAsync(onFirstBranch), PostAsync(onSecondBranch));

        Assert.All(numbers, number => Assert.Equal("JV-000001", number));
    }

    // C4 — R-API-06: لا رأس يتيم بعد أي فشل
    [Fact]
    public async Task C04_PostAsync_FailedPosting_LeavesNoHeaderWithoutLines()
    {
        var scenario = await NewScenarioAsync();
        var request = scenario.NewPost(
            scenario.Debit(scenario.CashAccountId, 1_000m, scenario.UsdCurrencyId, 1_320m),
            scenario.Credit(scenario.RevenueAccountId, 1_320_050m));

        await Assert.ThrowsAsync<SqlException>(() => PostAsync(request));

        Assert.False(await EntryExistsAsync(request.JournalEntryId));
        Assert.Equal(0, await OrphanHeaderCountAsync(scenario.CompanyId));
    }
}
