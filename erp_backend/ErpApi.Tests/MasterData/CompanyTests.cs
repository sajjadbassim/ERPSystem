using System.Net;
using System.Text.Json;
using ErpApi.Core.Constants;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.MasterData;

public class CompanyTests(TestDatabase database) : IdentityTestBase(database)
{
    private static string CompanyPath(Guid id) => $"{MasterDataClient.CompaniesPath}/{id}";

    // ‏‏══ G23–G25: الشركة بحدّ شركتها لنفسها (`Company.Id == scopedCompanyId`) ═══════

    // G23
    [Fact]
    public async Task G23_ListCompanies_ReturnsOnlyTheActorsOwnCompany()
    {
        var identity = await NewIdentityAsync();
        _ = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var items = await AccountingClient.ReadItemsAsync(
            await AuthClient.GetAsync(Client, $"{MasterDataClient.CompaniesPath}?PageNumber=1&PageSize=100", accessToken));

        Assert.Equal([identity.Company.CompanyId], items.Select(company => company.GetProperty("id").GetGuid()));
    }

    // G24 — نظير K27: العدّاد من المرشّح نفسه
    [Fact]
    public async Task G24_ListCompanies_TotalCountIsOne()
    {
        var identity = await NewIdentityAsync();
        _ = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client, $"{MasterDataClient.CompaniesPath}?PageNumber=1&PageSize=100", accessToken);

        Assert.Equal(1, (await AccountingClient.ReadDataAsync(response)).GetProperty("totalCount").GetInt32());
    }

    // G25
    [Fact]
    public async Task G25_GetCompany_OwnSucceeds_MissingIs404_AnotherIs403()
    {
        var identity = await NewIdentityAsync();
        var other = (await NewIdentityAsync()).Company;
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        Assert.Equal(HttpStatusCode.OK,
            (await AuthClient.GetAsync(Client, CompanyPath(identity.Company.CompanyId), accessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await AuthClient.GetAsync(Client, CompanyPath(Guid.CreateVersion7()), accessToken)).StatusCode);

        var foreign = await AuthClient.GetAsync(Client, CompanyPath(other.CompanyId), accessToken);
        using var body = JsonDocument.Parse(await foreign.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(AuthMessages.CompanyOutOfScope, body.RootElement.GetProperty("message").GetString());
    }

    // R-AMT-07-a: الحد = 10^(−DecimalPlaces) لعملة الأساس — أصغر وحدة قابلة للعرض.
    // باقٍ أصغر منها لا يظهر للمستخدم أصلاً، وأكبر منها رقم حقيقي يستحق تفسيراً لا ابتلاعاً.
    // الاشتقاق يُفحص لا يُفترض: ثابت واحد لكل العملات خاطئ في اتجاهين معاً
    // الرموز مختلقة لا حقيقية: IQD و USD مبذورتان في قاعدة الاختبار، فإعادة إنشائهما
    // تُرفض بـ 409 قبل أن يبلغ الاختبار ما جاء يفحصه. الخانات هي المدخل ذو المعنى
    [Theory]
    // K10 — بدقة الدينار: صفر خانات
    [InlineData("TA0", (byte)0, "1.0000")]
    // K11 — بدقة الدولار: خانتان
    [InlineData("TA2", (byte)2, "0.0100")]
    // K12 — بدقة الدينار الكويتي: ثلاث خانات، ضمن دقة العمود (19,4)
    [InlineData("TA3", (byte)3, "0.0010")]
    public async Task K10_K12_CreateCompany_DerivesFxRoundingToleranceFromBaseCurrency(
        string code, byte decimalPlaces, string expectedTolerance)
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var currency = await AuthClient.PostAsync(Client, MasterDataClient.CurrenciesPath, accessToken,
            new { code, name = $"عملة {code}", symbol = (string?)null, decimalPlaces });
        currency.EnsureSuccessStatusCode();
        var currencyId = await MasterDataClient.RawStringAsync(currency, "id");

        var response = await AuthClient.PostAsync(Client, MasterDataClient.CompaniesPath, accessToken,
            new { code = $"CO-{code}", name = $"شركة {code}", baseCurrencyId = currencyId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(expectedTolerance,
            await MasterDataClient.RawStringAsync(response, "fxRoundingToleranceBase"));
    }

    // K13
    [Fact]
    public async Task K13_CreateCompany_DuplicateCode_IsConflict()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.CompaniesPath, accessToken,
            new
            {
                code = identity.Company.CompanyCode,
                name = "شركة بنفس الرمز",
                baseCurrencyId = identity.Company.BaseCurrencyId
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // K14 — إنشاء شركة يثبّت عملة دفاترها، وهو قرار لا يُتراجع عنه (R-BASE-01).
    // فصلح صلاحيته عن صلاحية إدخال سعر صرف يومي
    [Fact]
    public async Task K14_CreateCompany_WithoutCompanyManage_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.CompaniesPath, accessToken,
            new { code = "CO-DENY", name = "شركة ممنوعة", baseCurrencyId = identity.Company.BaseCurrencyId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
