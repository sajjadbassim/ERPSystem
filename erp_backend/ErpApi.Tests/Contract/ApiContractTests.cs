using System.Text.Json.Nodes;
using ErpApi.ContractTool;

namespace ErpApi.Tests.Contract;

// ‏R-API-04 [صارم]: أنواع TypeScript مولَّدة آلياً من OpenAPI، ويُمنع أي نموذج يدوي.
// فالملف المعتمَد `ErpApi/openapi.json` مصدرُ تلك الأنواع، وقِدَمُه يعني أن الواجهة
// تُبنى على عقد لم يعد قائماً.
//
// هذان الاختباران يقرآن ولا يكتبان. الكتابة في ErpApi.ContractTool وحده — تنفيذيّ
// مستقل لا يبلغه مشغّل الاختبارات، فيستحيل أن تُمحى الحقيقة في التشغيلة التي
// يُفترض أن تقارن بها.
//
// ولا تحتاج المجموعة قاعدة بيانات: توليد الوثيقة انعكاس على نقاط النهاية والـ DTOs،
// و AddDbContext كسول فلا يُحلّ AppDbContext أصلاً.
public class ApiContractTests
{
    private const int MaxReportedDifferences = 12;

    private static async Task<JsonNode> GeneratedDocumentAsync()
    {
        var json = await OpenApiDocumentProducer.ProduceAsync();

        return JsonNode.Parse(json)
               ?? throw new InvalidOperationException("الوثيقة المولَّدة ليست JSON صالحاً.");
    }

    // M01
    [Fact]
    public async Task M01_CommittedDocument_MatchesGeneratedDocument()
    {
        var path = ContractPaths.ResolveDocumentPath();

        Assert.True(File.Exists(path),
            $"الملف المعتمَد غير موجود: {path}. شغّل ErpApi.ContractTool لإخراجه.");

        var committed = JsonNode.Parse(await File.ReadAllTextAsync(path))
                        ?? throw new InvalidOperationException("الملف المعتمَد ليس JSON صالحاً.");

        var generated = await GeneratedDocumentAsync();

        var differences = new List<string>();
        CollectDifferences(committed, generated, "$", differences);

        Assert.True(differences.Count == 0,
            "العقد المولَّد يخالف الملف المعتمَد — أعد تشغيل ErpApi.ContractTool واعتمد الفرق:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, differences.Take(MaxReportedDifferences))
            + (differences.Count > MaxReportedDifferences
                ? $"{Environment.NewLine}... و{differences.Count - MaxReportedDifferences} اختلافاً آخر"
                : string.Empty));
    }

    // M02
    [Fact]
    public async Task M02_NoSchemaNode_IsTypedAsNumber()
    {
        var generated = await GeneratedDocumentAsync();

        var offenders = new List<string>();
        CollectNumberTypedNodes(generated, "$", offenders);

        // كل عدد صحيح في هذا النظام يخرج integer. فما بقي number هو مبلغ أو سعر صرف
        // بالضرورة، وكلاهما يعبر نصاً (R-API-03). والصياغة الشاملة تفشل **مغلقة**:
        // خاصية decimal جديدة لا يغطيها المحوّل المخططي تُولّد number من العدم فتسقط هنا،
        // بينما لائحة أسماء معروفة كانت ستفشل مفتوحة — الحقل الجديد ببساطة ليس فيها.
        Assert.True(offenders.Count == 0,
            $"وُجد {offenders.Count} موضعاً بنوع number في العقد — كل مبلغ وسعر صرف يعبر نصاً "
            + "(R-API-03)، و double ممنوع في أي حقل مالي (R-AMT-04):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    // M03
    [Fact]
    public async Task M03_EveryEnumSchema_DeclaresItsAllowedValues()
    {
        var generated = await GeneratedDocumentAsync();

        var schemas = generated["components"]?["schemas"] as JsonObject
                      ?? throw new InvalidOperationException("لا components.schemas في الوثيقة المولَّدة.");

        // الكنس على **كل** enum في التجميع لا على لائحة أسماء: النوع الجديد يظهر في
        // العقد باسمه فيُشمل تلقائياً. ولائحة معروفة كانت ستفشل مفتوحة — الجديد
        // ببساطة ليس فيها، وهو فخّ L34 نفسه على جانب العقد
        var enumTypes = typeof(ErpApi.Core.Constants.AccountType).Assembly
            .GetTypes()
            .Where(t => t.IsEnum && schemas.ContainsKey(t.Name))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        // حارس ضد النجاح الفارغ: لو انقطع الربط بين اسم النوع واسم المخطط يوماً،
        // لصار الاختبار يجتاز بلا أن يفحص شيئاً
        Assert.True(enumTypes.Count > 0,
            "لم يُعثر على أي نوع enum في العقد — الربط بين اسم النوع واسم المخطط منقطع، "
            + "والاختبار كان سيجتاز بلا فحص.");

        var offenders = new List<string>();

        foreach (var enumType in enumTypes)
        {
            var expected = Enum.GetValues(enumType).Cast<object>()
                .Select(value => Convert.ToInt64(value).ToString())
                .ToList();

            var declared = (schemas[enumType.Name]?["enum"] as JsonArray)?
                .Select(entry => entry?.ToJsonString() ?? "null")
                .ToList();

            if (declared is null)
            {
                offenders.Add($"  {enumType.Name}: بلا enum — العقد يصف مدى النوع الأساسي لا مدى الـenum");
                continue;
            }

            if (!declared.SequenceEqual(expected))
            {
                offenders.Add($"  {enumType.Name}: العقد يعلن [{string.Join(", ", declared)}] "
                              + $"والنوع يقول [{string.Join(", ", expected)}]");
            }
        }

        // ‏`{"type":"integer"}` مجرّدة تصف سطحاً **أوسع** من الحقيقي، وهو عين ما وُجد
        // `DecimalAsStringSchemaTransformer` لأجله: لا تُترك القاعدة تحمل الحقيقة وحدها
        // بينما العقد يكذب بالسكوت. والفارض العام يرفض ما يقبله العقد اليوم، فعقد
        // غير مصحَّح يَعِد بقبول ما يرفضه الخادم
        Assert.True(offenders.Count == 0,
            $"وُجد {offenders.Count} نوع enum لا يعلن العقد مداه بدقة:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    // مقارنة مُطبَّعة لا نصّية: ترتيب المفاتيح والمسافات ونهايات الأسطر لا تُنتج فشلاً.
    // الفشل على التفاهات يُدرَّب على تجاهله، والاختبار المُتجاهَل أسوأ من غيابه.
    private static void CollectDifferences(JsonNode? committed, JsonNode? generated, string path, List<string> differences)
    {
        if (committed is null || generated is null)
        {
            if (committed is not null || generated is not null)
            {
                differences.Add($"  {path}: قيمة null في أحد الطرفين دون الآخر");
            }

            return;
        }

        if (committed is JsonObject committedObject && generated is JsonObject generatedObject)
        {
            foreach (var property in committedObject)
            {
                if (!generatedObject.ContainsKey(property.Key))
                {
                    differences.Add($"  {path}.{property.Key}: مفقود من الوثيقة المولَّدة");
                }
            }

            foreach (var property in generatedObject)
            {
                if (!committedObject.ContainsKey(property.Key))
                {
                    differences.Add($"  {path}.{property.Key}: زائد في الوثيقة المولَّدة");
                }
                else
                {
                    CollectDifferences(committedObject[property.Key], property.Value, $"{path}.{property.Key}", differences);
                }
            }

            return;
        }

        if (committed is JsonArray committedArray && generated is JsonArray generatedArray)
        {
            if (committedArray.Count != generatedArray.Count)
            {
                differences.Add($"  {path}: طول المصفوفة {committedArray.Count} في الملف مقابل {generatedArray.Count} في المولَّدة");
                return;
            }

            for (var i = 0; i < committedArray.Count; i++)
            {
                CollectDifferences(committedArray[i], generatedArray[i], $"{path}[{i}]", differences);
            }

            return;
        }

        var committedValue = committed.ToJsonString();
        var generatedValue = generated.ToJsonString();

        if (!string.Equals(committedValue, generatedValue, StringComparison.Ordinal))
        {
            differences.Add($"  {path}: {committedValue} في الملف مقابل {generatedValue} في المولَّدة");
        }
    }

    private static void CollectNumberTypedNodes(JsonNode? node, string path, List<string> offenders)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                if (DeclaresNumberType(jsonObject["type"]))
                {
                    var format = jsonObject["format"]?.ToJsonString() ?? "بلا format";
                    offenders.Add($"  {path} -> type={jsonObject["type"]!.ToJsonString()} format={format}");
                }

                foreach (var property in jsonObject)
                {
                    CollectNumberTypedNodes(property.Value, $"{path}.{property.Key}", offenders);
                }

                break;

            case JsonArray jsonArray:
                for (var i = 0; i < jsonArray.Count; i++)
                {
                    CollectNumberTypedNodes(jsonArray[i], $"{path}[{i}]", offenders);
                }

                break;
        }
    }

    // النوع قد يكون نصاً مفرداً أو مصفوفة أنواع (OpenAPI 3.1 يسمح بالاتحاد)
    private static bool DeclaresNumberType(JsonNode? type) => type switch
    {
        JsonArray array => array.Any(entry => entry?.GetValue<string>() == "number"),
        JsonValue value => value.GetValue<string>() == "number",
        _ => false
    };
}
