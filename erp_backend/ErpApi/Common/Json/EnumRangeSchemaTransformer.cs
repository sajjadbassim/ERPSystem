using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ErpApi.Common.Json;

// النصف الثاني من قرار «مدى الـenum يُفرض عند حدّ الـAPI»، ولا يقوم أحدهما بلا الآخر:
// ‏`EnumRangeValidationFilter` يفرض المدى **على السلك**، وهذا يعلنه **في العقد**.
//
// وفرض المدى بلا تصحيح العقد يقلب الصدق كذباً في الاتجاه المضاد — عقدٌ يَعِد بقبول
// ما يرفضه الخادم. فالمولِّد يصف كل enum بـ`{"type":"integer"}` مجرّدة، أي مدى النوع
// الأساسي (0..255) لا مدى الـenum (1..5). وهو **الوضع نفسه** الذي وُجد
// ‏`DecimalAsStringSchemaTransformer` لأجله: وصفٌ أوسع من الحقيقي بالتساهل.
//
// **الكنس على كل نوع enum لا على لائحة أنواع بالاسم**: الجديد مشمول بحكم البناء،
// واللائحة كانت ستفشل مفتوحة. الحارس `M03` يفرض ذلك بالصياغة نفسها.
//
// يُوضع بجوار نظيره عمداً: من يضيف أحدهما يرى الآخر، فلا ينحرف طرفا العقد.
public sealed class EnumRangeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (!underlying.IsEnum)
        {
            return Task.CompletedTask;
        }

        // القيم تُعلَن **بأرقامها** لأنها ما يعبر السلك: لا `JsonStringEnumConverter`
        // مسجَّل، فالحمولة تحمل عدداً والاسم مرفوض (مقيس على `"Asset"`).
        // وإعلان الأسماء هنا كان سيصف سطحاً لا يقبله الخادم — الكذبة نفسها بالمقلوب
        schema.Enum = [.. Enum.GetValues(underlying)
            .Cast<object>()
            .Select(value => (JsonNode)JsonValue.Create(Convert.ToInt64(value)))];

        return Task.CompletedTask;
    }
}
