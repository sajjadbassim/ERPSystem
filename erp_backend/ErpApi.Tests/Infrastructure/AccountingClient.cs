using System.Text.Json;

namespace ErpApi.Tests.Infrastructure;

// عقد نقاط نهاية الدفعة (ب). الاستجابات تُقرأ بـ JsonDocument عمداً (R-API-04)
public static class AccountingClient
{
    public const string AccountsPath = "/api/accounts";
    public const string FiscalYearsPath = "/api/fiscal-years";
    public const string FiscalPeriodsPath = "/api/fiscal-periods";
    public const string NumberSequencesPath = "/api/number-sequences";
    public const string JournalEntriesPath = "/api/journal-entries";

    public static string DeactivateAccountPath(Guid id) => $"{AccountsPath}/{id}/deactivate";

    public static string ClosePeriodPath(Guid id) => $"{FiscalPeriodsPath}/{id}/close";

    public static string CloseYearPath(Guid id) => $"{FiscalYearsPath}/{id}/close";

    public static string EntryPath(Guid id) => $"{JournalEntriesPath}/{id}";

    public static string ReversePath(Guid id) => $"{JournalEntriesPath}/{id}/reverse";

    // كل حقل مبلغ أو سعر في هذه الدفعة. R-API-03 يفرض عبورها نصوصاً لا أعداداً،
    // فالفحص على نوع الرمز لا على القيمة — القيمة تمرّ سليمة في الحالتين
    public static readonly IReadOnlyList<string> LineMoneyFields =
        ["exchangeRate", "debitFC", "creditFC", "debitBase", "creditBase"];

    // R-AMT-02: الحقول الخمسة الإلزامية لكل سطر مالي، ومعها سياق العملة المقروء
    public static readonly IReadOnlyList<string> LineRequiredFields =
    [
        "accountId", "accountCode",
        "currencyId", "currencyCode",
        "exchangeRate", "exchangeRateDate",
        "debitFC", "creditFC", "debitBase", "creditBase"
    ];

    public static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(payload);

        return document.RootElement.TryGetProperty("data", out var data)
            ? data.Clone()
            : document.RootElement.Clone();
    }

    // عناصر PagedResponse<T>.Data، أو المصفوفة نفسها إن لم تكن مرقَّمة
    public static async Task<List<JsonElement>> ReadItemsAsync(HttpResponseMessage response)
    {
        var data = await ReadDataAsync(response);

        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("data", out var inner))
        {
            data = inner;
        }

        return data.ValueKind == JsonValueKind.Array
            ? [.. data.EnumerateArray().Select(e => e.Clone())]
            : [];
    }

    public static async Task<List<JsonElement>> ReadLinesAsync(HttpResponseMessage response)
    {
        var data = await ReadDataAsync(response);

        return data.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array
            ? [.. lines.EnumerateArray().Select(e => e.Clone())]
            : [];
    }

    public static string? Field(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

    // آثار تدل على أن نصّ SQL خاماً عبر إلى العميل: أسماء أنواع الاستثناءات،
    // أسماء الإجراءات والقيود والفهارس، وترويسات رسائل SQL Server
    private static readonly string[] RawSqlMarkers =
    [
        "SqlException", "Microsoft.Data.SqlClient", "System.Data", "StackTrace",
        "usp_JournalEntry", "TR_", "CK_", "UQ_", "IX_", "FK_", "PK_",
        "Msg ", "Level 16", "Procedure", "sp_executesql", "THROW"
    ];

    // بند 8.4: يُمنع تسريب ex.Message الخام أو StackTrace للعميل.
    // الفحص على **جسم الاستجابة الفعلي** لا على رمز الحالة: 400 برسالة تحوي اسم قيد
    // يبدو معالَجاً وهو تسريب لبنية المخطط
    public static async Task AssertNoRawSqlLeakAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        foreach (var marker in RawSqlMarkers)
        {
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
        }

        // ولا رقم خطأ خام من نطاقَي الإجراءات (50001..50031) والتريجرات (51001..51014)
        foreach (var number in Enumerable.Range(50001, 31).Concat(Enumerable.Range(51001, 14)))
        {
            Assert.DoesNotContain(number.ToString(), body, StringComparison.Ordinal);
        }
    }

    // آثار تدل على أن بنية داخلية عبرت إلى العميل من **طبقة ربط النموذج** لا من القاعدة:
    // مسارات أنواع C#، ومواضع المحلّل، وأسماء تجميعات الإطار
    private static readonly string[] InternalLeakMarkers =
    [
        "ErpApi.", "System.", "Microsoft.", "Path: $", "LineNumber", "BytePositionInLine"
    ];

    // **الحارس الخَلَف لمسار لا يغطيه `AssertNoRawSqlLeakAsync`** — وعدم تغطيته مقيس
    // لا مفترض: أربع حمولات متسربة مُرِّرت على مرشِّحاته فمرّت كلها، لأن مؤشراته كلها
    // من عالم SQL ولا واحد منها يظهر في رسالة `System.Text.Json` أو `ModelState`.
    //
    // والشرط الموجب هنا هو الحارس الحقيقي: **الرسالة يجب أن تحمل حرفاً عربياً**.
    // وهي نفس القاعدة التي يفرضها `ArabicValidationMessages` في الإنتاج، فيفشل الاختبار
    // مغلقاً على أي رسالة إنجليزية جديدة من أي مصدر — بينما لائحة عبارات معروفة كانت
    // ستمرّرها لأنها ليست فيها
    public static async Task AssertNoInternalLeakAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var message = document.RootElement.TryGetProperty("message", out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;

        Assert.True(message.Any(character => character is >= '؀' and <= 'ۿ'),
            $"رسالة بلا حرف عربي — بند 8.4: «{message}»");

        foreach (var marker in InternalLeakMarkers)
        {
            Assert.DoesNotContain(marker, message, StringComparison.Ordinal);
        }
    }
}
