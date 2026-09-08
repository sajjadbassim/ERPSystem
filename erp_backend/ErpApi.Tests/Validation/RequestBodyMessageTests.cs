using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Validation;

// ‏N09 — الحارس على **كل صيغ فشل قراءة الجسم**، لا على الـenum وحده.
//
// القياس الذي أنتج هذه المجموعة: الحمولتان D و E كانتا تسرّبان
// ‏`ErpApi.Core.Constants.AccountType`، **وسرّب غيرهما `System.Guid` و
// ``System.Nullable`1[System.Guid]`` و`System.String`** ونصّ محلّل JSON ومواضعه.
// فالخرق لم يكن في الـenum بل في الطبقة، والحارس يجب أن يكون على الطبقة.
//
// **والبُعد الذي يتغيّر هنا هو نوع الفشل لا نقطة النهاية:** الإصلاح واقع في نقطة
// مركزية واحدة يمرّ بها كل تحقق (بند 6.3)، فلا تُضيف نقطةُ نهاية أخرى شهادةً جديدة.
// أما صيغة فشل جديدة فتُضيف، ولذلك عُدِّدت الصيغ لا المسارات.
public class RequestBodyMessageTests(TestDatabase database) : IdentityTestBase(database)
{
    [Theory]
    [InlineData("enum خارج مدى النوع الأساسي")]
    [InlineData("enum باسم العضو نصاً")]
    [InlineData("Guid تالف")]
    [InlineData("نصّ في موضع منطقي")]
    [InlineData("Guid قابل للعدم تالف")]
    [InlineData("JSON غير صالح شكلاً")]
    [InlineData("جسم فارغ")]
    public async Task N09_MalformedBody_NeverLeaksInternalDetail(string shape)
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);
        var companyId = identity.Company.CompanyId;

        var response = shape switch
        {
            "enum خارج مدى النوع الأساسي" => await PostBodyAsync(accessToken,
                new { companyId, code = "9101", name = "س", accountType = 300, normalBalance = 0 }),
            "enum باسم العضو نصاً" => await PostBodyAsync(accessToken,
                new { companyId, code = "9102", name = "س", accountType = "Asset", normalBalance = 0 }),
            "Guid تالف" => await PostBodyAsync(accessToken,
                new { companyId = "not-a-guid", code = "9103", name = "س", accountType = 1, normalBalance = 0 }),
            "نصّ في موضع منطقي" => await PostBodyAsync(accessToken,
                new { companyId, code = "9104", name = true, accountType = 1, normalBalance = 0 }),
            "Guid قابل للعدم تالف" => await PostBodyAsync(accessToken,
                new { companyId, code = "9105", name = "س", accountType = 1, normalBalance = 0, currencyId = 5 }),
            "JSON غير صالح شكلاً" => await PostRawAsync(accessToken, "{ this is not json"),
            "جسم فارغ" => await PostRawAsync(accessToken, string.Empty),
            _ => throw new InvalidOperationException($"صيغة غير معرَّفة: {shape}")
        };

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AccountingClient.AssertNoInternalLeakAsync(response);
    }

    private Task<HttpResponseMessage> PostBodyAsync(string accessToken, object body) =>
        AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken, body);

    private Task<HttpResponseMessage> PostRawAsync(string accessToken, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, AccountingClient.AccountsPath)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return Client.SendAsync(request);
    }
}
