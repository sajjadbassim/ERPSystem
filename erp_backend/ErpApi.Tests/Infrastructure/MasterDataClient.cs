using System.Text.Json;

namespace ErpApi.Tests.Infrastructure;

// عقد نقاط نهاية البيانات الأساسية. الاستجابات تُقرأ بـ JsonDocument عمداً:
// تعريف DTOs هنا كان سينشئ نسخة ثانية من العقد تنحرف عن الأصل بصمت (R-API-04)
public static class MasterDataClient
{
    public const string CurrenciesPath = "/api/currencies";
    public const string CompaniesPath = "/api/companies";
    public const string BranchesPath = "/api/branches";
    public const string FxRatesPath = "/api/fx-rates";

    public static string DeactivateCurrencyPath(Guid id) => $"{CurrenciesPath}/{id}/deactivate";

    // يُرجع عنصر data من ApiResponse<T>، أو الجذر إن لم يكن مغلَّفاً
    public static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(payload);

        return document.RootElement.TryGetProperty("data", out var data)
            ? data.Clone()
            : document.RootElement.Clone();
    }

    // نوع الرمز لا قيمته: الفحص هنا على أن المبلغ عبر كنصّ لا كعدد (R-API-03).
    // قراءة القيمة وحدها كانت ستمرّ على 1320 سواء أُرسل "1320" أو 1320
    public static async Task<JsonValueKind> ValueKindAsync(HttpResponseMessage response, string field)
    {
        var data = await ReadDataAsync(response);
        return data.TryGetProperty(field, out var value) ? value.ValueKind : JsonValueKind.Undefined;
    }

    public static async Task<string?> RawStringAsync(HttpResponseMessage response, string field)
    {
        var data = await ReadDataAsync(response);
        return data.TryGetProperty(field, out var value) ? value.GetString() : null;
    }
}
