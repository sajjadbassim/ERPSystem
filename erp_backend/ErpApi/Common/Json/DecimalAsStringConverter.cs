using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErpApi.Common.Json;

// R-API-03 [صارم]: المبالغ وأسعار الصرف تُنقل نصوصاً لا أعداداً.
// السبب أن Number في JavaScript هو double: يفقد الدقة فوق 2^53، ومليار دينار رقم يومي
// في هذا النظام. سعر صرف بدقة (28,12) يفقد خاناته الأخيرة صامتاً قبل أن يصل الشاشة.
//
// يُسجَّل مرة واحدة على مستوى MVC لا في كل DTO: التسجيل الموضعي يعني أن أي DTO جديد
// يُكتب لاحقاً بلا انتباه يمرّ بلا حماية — والخلل حينها رقم خاطئ لا خطأ ظاهر.
public sealed class DecimalAsStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    // ToString بلا تنسيق يحفظ خانات الكسر كما هي في القيمة، فسعر (28,12) يعبر بكامل دقته
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

public sealed class NullableDecimalAsStringConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
            : reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
