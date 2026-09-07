using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ErpApi.Tests.Infrastructure;

// عقد نقاط النهاية الذي تفرضه الاختبارات على التنفيذ.
// الطلبات كائنات مجهولة والاستجابات تُقرأ بـ JsonDocument عمداً: تعريف DTOs هنا كان
// سينشئ نسخة ثانية من عقد الـ API تنحرف عن الأصل بصمت (R-API-04).
public static class AuthClient
{
    public const string LoginPath = "/api/auth/login";
    public const string RefreshPath = "/api/auth/refresh";
    public const string ChangePasswordPath = "/api/auth/change-password";
    public const string PermissionsPath = "/api/permissions";
    public const string JournalEntriesPath = "/api/journal-entries";
    public const string TrialBalancePath = "/api/reports/trial-balance";
    public const string RolesPath = "/api/roles";

    // companyCode يُحذف من الجسم كلياً حين لا يُمرَّر، فيبقى شكل الطلب في الـ46 المعتمدة
    // كما هو حرفياً ولا يتغير عقدها بإضافة حقل جديد
    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string userName, string password, string? companyCode = null) =>
        companyCode is null
            ? client.PostAsJsonAsync(LoginPath, new { userName, password })
            : client.PostAsJsonAsync(LoginPath, new { userName, password, companyCode });

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync(RefreshPath, new { refreshToken });

    public static Task<HttpResponseMessage> ChangePasswordAsync(
        HttpClient client, string accessToken, string currentPassword, string newPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ChangePasswordPath)
        {
            Content = JsonContent.Create(new { currentPassword, newPassword })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string accessToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    // فكّ الحمولة فقط بلا تحقق من التوقيع: أداة فحص في الاختبار لا بوابة أمان.
    // التحقق الحقيقي يقع في الخادم، ومحاكاته هنا كانت ستختبر المكتبة لا الكود
    public static IReadOnlyDictionary<string, string> DecodeJwtClaims(string accessToken)
    {
        var parts = accessToken.Split('.');

        if (parts.Length != 3)
        {
            return new Dictionary<string, string>();
        }

        using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));

        return document.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.ToString());
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (3 - ((padded.Length + 3) % 4)), '='));
    }

    public static async Task<string> ReadFieldAsync(HttpResponseMessage response, string field)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return ReadField(document.RootElement, field) ?? string.Empty;
    }

    public static async Task<IReadOnlyList<string>> ReadStringArrayAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            root = data;
        }

        return root.ValueKind == JsonValueKind.Array
            ? [.. root.EnumerateArray().Select(e => e.GetString() ?? string.Empty)]
            : [];
    }

    private static string? ReadField(JsonElement element, string field)
    {
        if (element.TryGetProperty(field, out var direct))
        {
            return direct.GetString();
        }

        // الاستجابات مغلَّفة بـ ApiResponse<T>، فالحقل قد يقع تحت data
        return element.TryGetProperty("data", out var data) && data.TryGetProperty(field, out var nested)
            ? nested.GetString()
            : null;
    }
}
