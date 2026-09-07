using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.MasterData;

public class FxRateTests(TestDatabase database) : IdentityTestBase(database)
{
    // K19
    [Fact]
    public async Task K19_CreateFxRate_ValidPayload_ReturnsCreated()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.UsdCurrencyId,
                toCurrencyId = identity.Company.BaseCurrencyId,
                rate = "1320.000000000000",
                rateDate = "2026-07-01",
                rateSource = 1
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // K20 — R-FX-01/R-FX-02: السعر يجيب سؤالاً بين عملتين مختلفتين.
    // من عملة إلى نفسها ليس سعراً بل واحد، ويحرسه CK_FxRate_DifferentCurrencies
    [Fact]
    public async Task K20_CreateFxRate_SameFromAndToCurrency_IsBadRequest()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.BaseCurrencyId,
                toCurrencyId = identity.Company.BaseCurrencyId,
                rate = "1.000000000000",
                rateDate = "2026-07-02",
                rateSource = 1
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // K21 — سعر صفري أو سالب لا معنى اقتصادياً له، ويحرسه CK_FxRate_RatePositive
    [Fact]
    public async Task K21_CreateFxRate_NonPositiveRate_IsBadRequest()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.UsdCurrencyId,
                toCurrencyId = identity.Company.BaseCurrencyId,
                rate = "0.000000000000",
                rateDate = "2026-07-03",
                rateSource = 1
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // K22 — سعران لنفس الزوج والتاريخ والمصدر يجعلان السؤال «بأي سعر؟» بلا جواب.
    // يحرسه UQ_FxRate_From_To_Date_Source
    [Fact]
    public async Task K22_CreateFxRate_DuplicateScope_IsConflict()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var payload = new
        {
            fromCurrencyId = identity.Company.UsdCurrencyId,
            toCurrencyId = identity.Company.BaseCurrencyId,
            rate = "1320.000000000000",
            rateDate = "2026-07-04",
            rateSource = 1
        };

        var first = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken, payload);
        first.EnsureSuccessStatusCode();

        var second = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken, payload);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // K23 — R-FX-04: الدقة (28,12) لأن السعر الأقل من واحد يفقد معناه سريعاً بدقة أقل.
    // القطع في أي طبقة — تخزين أو نقل — يتضخم عبر التحويلات المتتالية
    [Fact]
    public async Task K23_CreateFxRate_TwelveDecimalPlaces_ArePreservedExactly()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);
        const string exact = "0.000000123456";

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.BaseCurrencyId,
                toCurrencyId = identity.Company.UsdCurrencyId,
                rate = exact,
                rateDate = "2026-07-05",
                rateSource = 1
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(exact, await MasterDataClient.RawStringAsync(response, "rate"));
    }

    // K24 — الفشل مغلق
    [Fact]
    public async Task K24_CreateFxRate_WithoutFxRateManage_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.UsdCurrencyId,
                toCurrencyId = identity.Company.BaseCurrencyId,
                rate = "1320.000000000000",
                rateDate = "2026-07-06",
                rateSource = 1
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
