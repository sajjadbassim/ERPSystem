using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.MasterData;

public class CompanyTests(TestDatabase database) : IdentityTestBase(database)
{
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
