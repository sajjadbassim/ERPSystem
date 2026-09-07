using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ErpApi.Common.Json;

// النصف الثاني من عقد R-API-03، ولا يقوم أحدهما بلا الآخر:
// ‏`DecimalAsStringConverter` يجعل المبلغ نصاً **على السلك**، وهذا يجعله نصاً **في العقد**.
//
// ولزومه ليس نظرياً بل مقيس: مولّد المخطط يقرأ `Http.Json.JsonOptions` — كما يقوله
// توقيع `OpenApiSchemaService..ctor` — بينما المحوّل مسجَّل على `Mvc.JsonOptions` في
// ‏`AddControllers().AddJsonOptions(...)`. طبقتان منفصلتان، فلا يرى المولِّدُ المحوّلَ
// أصلاً، ويصف كل decimal بـ `["number","string"]` بصيغة `double`.
//
// وذلك وصف كاذب في اتجاهين: الاستجابة تحمل نصاً **دائماً** فلا تكون عدداً قط،
// و `double` ممنوع منعاً باتاً في أي حقل مالي (R-AMT-04) — وسعر صرف بدقة (28,12)
// يفقد خاناته في نوع لا يتسع لها.
//
// يُوضع بجوار المحوّل عمداً: من يضيف أحدهما يرى الآخر، فلا ينحرف طرفا العقد.
public sealed class DecimalAsStringSchemaTransformer : IOpenApiSchemaTransformer
{
    // نمط العدد العشري نصاً. يبقيه المولِّد على المخطط أصلاً، ونعيد تثبيته صراحةً
    // كي لا يعتمد العقد على تفصيل داخلي قد يتغيّر في نسخة لاحقة
    private const string DecimalPattern = @"^-?(?:0|[1-9]\d*)(?:\.\d+)?$";

    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (Nullable.GetUnderlyingType(type) != typeof(decimal) && type != typeof(decimal))
        {
            return Task.CompletedTask;
        }

        // العدم يُحفَظ إن كان قائماً: `decimal?` يبقى قابلاً للعدم بعد التحويل إلى نصّ
        var preservesNull = schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null);

        schema.Type = preservesNull
            ? JsonSchemaType.String | JsonSchemaType.Null
            : JsonSchemaType.String;

        // إسقاط `double` لا استبداله: لا صيغة OpenApi قياسية تصف عشرياً بدقة (28,12)،
        // وأي صيغة عائمة تكرّر الكذبة نفسها بلفظ آخر
        schema.Format = null;
        schema.Pattern = DecimalPattern;

        return Task.CompletedTask;
    }
}
