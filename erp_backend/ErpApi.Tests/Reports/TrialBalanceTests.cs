using System.Globalization;
using System.Net;
using System.Text.Json;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Reports;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Reports;

// ميزان المراجعة — مصفوفة R. البادئة R حرّة بكنسة ErpApi.Tests (A..N وX مأخوذة).
//
// المصدر الوحيد للأرقام الدفتر المرحَّل (بند 10.1، R-RPT-01)، والتجميع في القاعدة.
// والقيود تُرحَّل بالإجراء مباشرة (PostingClient) لا عبر الواجهة: المقيس هنا القراءة لا الترحيل.
//
// أسماء الحقول مربوطة بالـDTO بـnameof — فغيابه يكسر الترجمة (حمرة بالاستيراد)، وتحريف
// اسم حقل فيه لا يمرّ على اختبار يبحث عن اسم قديم.
public class TrialBalanceTests(TestDatabase database) : IdentityTestBase(database)
{
    private static string Json(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);

    private static readonly string Rows = Json(nameof(TrialBalanceResponseDto.Rows));
    private static readonly string Totals = Json(nameof(TrialBalanceResponseDto.Totals));
    private static readonly string DebitBalance = Json(nameof(TrialBalanceRowDto.DebitBalance));
    private static readonly string CreditBalance = Json(nameof(TrialBalanceRowDto.CreditBalance));
    private static readonly string TotalDebit = Json(nameof(TrialBalanceTotalsDto.TotalDebit));
    private static readonly string TotalCredit = Json(nameof(TrialBalanceTotalsDto.TotalCredit));
    private static readonly string AmountBase = Json(nameof(BaseAmountDto.AmountBase));

    private async Task<(HttpStatusCode Status, JsonElement Data)> TrialBalanceAsync(string accessToken, Guid branchId)
    {
        var response = await AuthClient.GetAsync(Client, $"{AuthClient.TrialBalancePath}?branchId={branchId}", accessToken);

        return (response.StatusCode, await AccountingClient.ReadDataAsync(response));
    }

    private static List<JsonElement> RowsOf(JsonElement data) =>
        [.. data.GetProperty(Rows).EnumerateArray()];

    private static JsonElement RowOf(JsonElement data, Guid accountId) =>
        RowsOf(data).Single(row => row.GetProperty("accountId").GetGuid() == accountId);

    // R-API-02 وR-API-03: المبلغ كائن كامل بعملته، والقيمة **نصّ** لا عدد — فالفحص على
    // نوع الرمز قبل القيمة
    private static decimal Amount(JsonElement owner, string field, Guid expectedCurrencyId)
    {
        var money = owner.GetProperty(field);
        var amount = money.GetProperty(AmountBase);

        Assert.Equal(JsonValueKind.String, amount.ValueKind);
        Assert.Equal(expectedCurrencyId, money.GetProperty("currencyId").GetGuid());
        Assert.Equal("IQD", money.GetProperty("currencyCode").GetString());

        return decimal.Parse(amount.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private Task PostAsync(Scenario company, Guid branchId, params LineDraft[] lines)
    {
        var request = company.NewPost(lines);
        request.BranchId = branchId;

        return PostingClient.PostAsync(Database, request);
    }

    [Fact]
    public async Task R01_PostedEntry_ShowsBothAccountsWithBalancesAndEqualTotals()
    {
        var identity = await NewIdentityAsync();
        var company = identity.Company;

        await PostAsync(company, company.BranchId,
            company.Debit(company.CashAccountId, 1000m), company.Credit(company.RevenueAccountId, 1000m));

        var (accessToken, _) = await LoginAsync(identity.BranchUserName);
        var (status, data) = await TrialBalanceAsync(accessToken, company.BranchId);

        Assert.Equal(HttpStatusCode.OK, status);

        // الرأس (R-RPT-02): الأساس معلَن، وعملة الدفاتر من الشركة لا من معامل
        Assert.Equal("BaseCurrency", data.GetProperty("basis").GetString());
        Assert.Equal(company.BaseCurrencyId, data.GetProperty("baseCurrencyId").GetGuid());
        Assert.Equal("IQD", data.GetProperty("baseCurrencyCode").GetString());
        Assert.Equal(company.BranchId, data.GetProperty("branchId").GetGuid());

        var cash = RowOf(data, company.CashAccountId);
        var revenue = RowOf(data, company.RevenueAccountId);

        Assert.Equal("1101", cash.GetProperty("accountCode").GetString());
        Assert.Equal(1000m, Amount(cash, DebitBalance, company.BaseCurrencyId));
        Assert.Equal(0m, Amount(cash, CreditBalance, company.BaseCurrencyId));
        Assert.Equal(0m, Amount(revenue, DebitBalance, company.BaseCurrencyId));
        Assert.Equal(1000m, Amount(revenue, CreditBalance, company.BaseCurrencyId));

        var totals = data.GetProperty(Totals);

        Assert.Equal(1000m, Amount(totals, TotalDebit, company.BaseCurrencyId));
        Assert.Equal(1000m, Amount(totals, TotalCredit, company.BaseCurrencyId));

        // الترتيب برمز الحساب
        Assert.Equal(["1101", "4101"], RowsOf(data).Select(row => row.GetProperty("accountCode").GetString()));
    }

    // R-LIFE-06: المرشِّح على الكيان المرجعي يُسقط **السطور** لا المرجع وحده، فيخرج ميزان
    // متوازن ينقصه حساب بلا إعلان. والحساب يُحذف منطقياً **بعد** ترحيل حركته، بـSQL مباشر
    [Fact]
    public async Task R02_SoftDeletedAccountWithMovement_StillAppears()
    {
        var identity = await NewIdentityAsync();
        var company = identity.Company;

        await PostAsync(company, company.BranchId,
            company.Debit(company.CashAccountId, 500m), company.Credit(company.RevenueAccountId, 500m));

        await PostingClient.ExecuteAsync(Database,
            "UPDATE Accounts SET IsDeleted = 1 WHERE Id = @id", ("@id", company.CashAccountId));

        var (accessToken, _) = await LoginAsync(identity.BranchUserName);
        var (_, data) = await TrialBalanceAsync(accessToken, company.BranchId);

        Assert.Equal(500m, Amount(RowOf(data, company.CashAccountId), DebitBalance, company.BaseCurrencyId));
        Assert.Equal(500m, Amount(data.GetProperty(Totals), TotalDebit, company.BaseCurrencyId));
    }

    [Fact]
    public async Task R03_NoLinesFromAnotherBranchOrAnotherCompany()
    {
        var identity = await NewIdentityAsync();
        var company = identity.Company;
        var other = (await NewIdentityAsync()).Company;

        await PostAsync(company, company.BranchId,
            company.Debit(company.CashAccountId, 1000m), company.Credit(company.RevenueAccountId, 1000m));

        // فرع ثانٍ في الشركة نفسها وعلى **الحسابين أنفسهما**: تسرّبه يظهر في المبلغ لا في عدد الصفوف
        await PostAsync(company, company.SecondBranchId,
            company.Debit(company.CashAccountId, 700m), company.Credit(company.RevenueAccountId, 700m));

        // شركة أخرى: حساباتها بالرموز نفسها ومعرّفات غيرها — فتسرّبها صفوف زائدة
        await PostAsync(other, other.BranchId,
            other.Debit(other.CashAccountId, 300m), other.Credit(other.RevenueAccountId, 300m));

        var (accessToken, _) = await LoginAsync(identity.BranchUserName);
        var (_, data) = await TrialBalanceAsync(accessToken, company.BranchId);

        // الشرط الموجب أولاً: الفرع المطلوب حاضر بأرقامه — فلا يمرّ النفي على ميزان فارغ
        Assert.Equal(1000m, Amount(RowOf(data, company.CashAccountId), DebitBalance, company.BaseCurrencyId));

        Assert.Equal(
            [company.CashAccountId, company.RevenueAccountId],
            RowsOf(data).Select(row => row.GetProperty("accountId").GetGuid()));

        Assert.Equal(1000m, Amount(data.GetProperty(Totals), TotalDebit, company.BaseCurrencyId));
    }

    [Fact]
    public async Task R04_AccountWithZeroNetBalance_AppearsWithTwoZeroColumns()
    {
        var identity = await NewIdentityAsync();
        var company = identity.Company;

        await PostAsync(company, company.BranchId,
            company.Debit(company.CashAccountId, 500m), company.Credit(company.RevenueAccountId, 500m));
        await PostAsync(company, company.BranchId,
            company.Debit(company.RealizedFxLossAccountId, 500m), company.Credit(company.CashAccountId, 500m));

        var (accessToken, _) = await LoginAsync(identity.BranchUserName);
        var (_, data) = await TrialBalanceAsync(accessToken, company.BranchId);

        var cash = RowOf(data, company.CashAccountId);

        Assert.Equal(0m, Amount(cash, DebitBalance, company.BaseCurrencyId));
        Assert.Equal(0m, Amount(cash, CreditBalance, company.BaseCurrencyId));

        var totals = data.GetProperty(Totals);

        Assert.Equal(500m, Amount(totals, TotalDebit, company.BaseCurrencyId));
        Assert.Equal(500m, Amount(totals, TotalCredit, company.BaseCurrencyId));
    }

    // نظير G06 على حدّ **الشركة** لا الفرع: G06 يطلب فرعاً ثانياً من الشركة نفسها
    [Fact]
    public async Task R05_BranchOfAnotherCompany_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var other = (await NewIdentityAsync()).Company;

        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var (inScope, _) = await TrialBalanceAsync(accessToken, identity.Company.BranchId);
        var (outOfScope, _) = await TrialBalanceAsync(accessToken, other.BranchId);

        Assert.Equal(HttpStatusCode.OK, inScope);
        Assert.Equal(HttpStatusCode.Forbidden, outOfScope);
    }

    // لا اختبار لهذه الصلاحية قبل اليوم. والشرط الموجب: الطلب نفسه ينجح قبل السحب،
    // فالرفض بعده من الصلاحية وحدها لا من الفرع
    [Fact]
    public async Task R06_WithoutTrialBalanceRead_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var (before, _) = await TrialBalanceAsync(accessToken, identity.Company.BranchId);
        Assert.Equal(HttpStatusCode.OK, before);

        await PostingClient.ExecuteAsync(Database,
            "DELETE FROM RolePermissions WHERE RoleId = @role AND PermissionCode = @code",
            ("@role", identity.BranchRoleId), ("@code", Permissions.TrialBalanceRead));

        var (after, _) = await TrialBalanceAsync(accessToken, identity.Company.BranchId);
        Assert.Equal(HttpStatusCode.Forbidden, after);
    }
}
