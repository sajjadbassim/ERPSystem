using System.Net;
using System.Text.Json;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.MasterData;

// R-API-02 و R-API-03 على حدود الشبكة. هذه أخطر مجموعة في الدفعة: خرقها لا يُنتج
// خطأً بل رقماً خاطئاً يصل الشاشة سليم المظهر
public class MoneySerializationTests(TestDatabase database) : IdentityTestBase(database)
{
    // K01 — الرقم يعبر كنصّ لا كعدد. Number في JavaScript هو double،
    // فسعر بدقة (28,12) يفقد خاناته الأخيرة قبل أن يُعرض
    [Fact]
    public async Task K01_FxRateResponse_CarriesRateAsJsonStringNotNumber()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.UsdCurrencyId,
                toCurrencyId = identity.Company.BaseCurrencyId,
                rate = "1320.000000000000",
                rateDate = "2026-06-15",
                rateSource = 1
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.String, await MasterDataClient.ValueKindAsync(response, "rate"));
    }

    // K02 — قيمة تتجاوز ما يمثّله double بدقة. لو عبرت كعدد لعادت مبتورة،
    // والاختبار على النصّ الحرفي لا على قيمة مقرَّبة
    [Fact]
    public async Task K02_RateBeyondDoublePrecision_SurvivesRoundTripExactly()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);
        const string exact = "1234567890.123456789012";

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.UsdCurrencyId,
                toCurrencyId = identity.Company.BaseCurrencyId,
                rate = exact,
                rateDate = "2026-06-16",
                rateSource = 1
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(exact, await MasterDataClient.RawStringAsync(response, "rate"));
    }

    // K03 — R-API-02: لا رقم عارٍ بلا سياق عملته. السعر بلا عملتيه ورمزيهما
    // وتاريخه صحيح عددياً وبلا معنى — وهو ما يمنعه R-RPT-06 على الشاشة
    [Fact]
    public async Task K03_FxRateResponse_CarriesCompleteCurrencyContext()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.FxRatesPath, accessToken,
            new
            {
                fromCurrencyId = identity.Company.UsdCurrencyId,
                toCurrencyId = identity.Company.BaseCurrencyId,
                rate = "1320.000000000000",
                rateDate = "2026-06-17",
                rateSource = 1
            });

        response.EnsureSuccessStatusCode();
        var data = await MasterDataClient.ReadDataAsync(response);

        foreach (var field in new[]
                 {
                     "fromCurrencyId", "fromCurrencyCode",
                     "toCurrencyId", "toCurrencyCode",
                     "rate", "rateDate", "rateSource"
                 })
        {
            Assert.True(data.TryGetProperty(field, out _), $"الحقل الناقص: {field}");
        }
    }
}
