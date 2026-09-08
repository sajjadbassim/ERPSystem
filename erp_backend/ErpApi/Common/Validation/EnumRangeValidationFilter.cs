using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpApi.Common.Validation;

// فارض مدى الـenum عند حدّ الـAPI (قرار 2026-09-08).
//
// **لماذا فلتر عام لا `[EnumDataType]` على كل خاصية:** السمة تفشل **مفتوحة** —
// خاصية enum جديدة تُضاف بلا السمة تمرّ صامتة، وهو فخّ L34 نفسه بالاسم وقد وقعنا فيه
// مرة مع `[Required(ErrorMessage = ...)]`. الكنس بالانعكاس يفشل **مغلقاً** بلا أن
// يتذكّر أحد شيئاً، وهو منطق M02 نفسه («صفر number في العقد كله» بدل لائحة أسماء).
//
// **ما يفرضه ليس سلامة البيانات بل صدق الرسالة والعقد.** النظام كان يرفض القيم
// الخارجة عن المدى أصلاً ويستقرّ بصفر صفوف — لكن الرفض كان يقع في موضعين خاطئين:
// في القاعدة برسالة قيد لا تسمّي الحقل، أو في الخدمة برسالة **واثقة وخاطئة** تتهم
// حقلاً سليماً. `System.Text.Json` يقبل أي قيمة تسع في النوع الأساسي (0..255 لا 1..5).
//
// وموضعه على مسار التحقق من الشكل — أي **قبل** فحص التخويل في الخدمة (بند 13.1)،
// كما يسبقه `[Required]` اليوم. حمولة مخالفة الشكل تُردّ بـ400 ولو كان مُرسِلها
// لا يملك الصلاحية أصلاً.
//
// **افتراض صريح لا سهو — `Enum.IsDefined` وحده بلا فرع لـ[Flags]:**
// ‏`IsDefined` يرفض أي تركيب بتّي لا يطابق قيمة مسمّاة، فلو حمل enum سمة `[Flags]`
// يوماً لرفض هذا الفارض تركيباً مشروعاً. **وهذا مقبول عن قصد**: enums السبعة كلها
// قوائم مغلقة لا رايات، وبناء فرع لحالة غير قائمة يخالف بند 2.2. والأثر لو وقع
// **فشل مغلق ظاهر** — طلب سليم يُردّ بـ400 فيُرى في أول اختبار — لا تسرّب صامت.
// **من يضيف enum بـ[Flags] فعليه تعديل هذا الموضع بقصد، لا أن يجده «عطباً» فيصلحه.**
public sealed class EnumRangeValidationFilter : IActionFilter
{
    // عمق يكفي كل DTO في المشروع أضعافاً، ويقطع أي دورة مرجعية غير متوقَّعة قبل
    // أن تستنزف المكدس. الكشف بالمرجع أدناه يمنع الدورة، وهذا حزام ثانٍ
    private const int MaxDepth = 8;

    private static readonly Assembly OwnAssembly = typeof(EnumRangeValidationFilter).Assembly;

    // الانعكاس على كل طلب بلا ذاكرة وسيطة ثمنٌ يُدفع في كل نداء. الخريطة تُبنى
    // مرة واحدة لكل نوع وتبقى
    private static readonly ConcurrentDictionary<Type, WalkableProperty[]> PropertyCache = new();

    private sealed record WalkableProperty(PropertyInfo Property, string JsonName);

    public void OnActionExecuting(ActionExecutingContext context)
    {
        List<string>? offenders = null;

        foreach (var argument in context.ActionArguments)
        {
            // اسم المعامل يظهر في الرسالة فقط حين يكون المعامل نفسه enum (استعلام أو مسار).
            // أما جسم الطلب فجذره بلا اسم عند العميل، فتُسمّى خصائصه مباشرة
            var root = argument.Value is not null && argument.Value.GetType().IsEnum
                ? ToJsonName(argument.Key)
                : string.Empty;

            Walk(argument.Value, root, 0, null, ref offenders);
        }

        if (offenders is null)
        {
            return;
        }

        // الفاصل نفسه الذي يستعمله InvalidModelStateResponseFactory (بند 6.3)،
        // والشكل نفسه: هذا تحقق من شكل المدخلات لا قاعدة عمل، فلا يمرّ باستثناء
        context.Result = new BadRequestObjectResult(
            ApiResponse<object>.Fail(string.Join(" | ", offenders), context.HttpContext.TraceIdentifier));
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static void Walk(
        object? value, string path, int depth, HashSet<object>? visited, ref List<string>? offenders)
    {
        if (value is null || depth > MaxDepth)
        {
            return;
        }

        var type = value.GetType();

        if (type.IsEnum)
        {
            // القيمة القابلة للعدم تصل هنا غير مغلَّفة: العدم عاد أعلاه، وما بقي قيمة
            // من النوع الأساسي. وغياب فرع [Flags] قرار مقصود موثَّق فوق الصنف
            if (!Enum.IsDefined(type, value))
            {
                (offenders ??= []).Add(Describe(type, value, path));
            }

            return;
        }

        if (value is string || type.IsPrimitive || !type.IsClass && !IsWalkableStruct(type))
        {
            return;
        }

        if (value is IEnumerable sequence)
        {
            var index = 0;

            foreach (var item in sequence)
            {
                Walk(item, $"{path}[{index}]", depth + 1, visited, ref offenders);
                index++;
            }

            return;
        }

        // الكنس مقصور على أنواع هذا التجميع: كل DTO فيه بحكم البناء، فالخاصية الجديدة
        // مشمولة تلقائياً. والتوغّل في أنواع الإطار يجرّ رسوماً بلا مقابل
        if (type.Assembly != OwnAssembly)
        {
            return;
        }

        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);

        if (!visited.Add(value))
        {
            return;
        }

        foreach (var property in PropertiesOf(type))
        {
            var child = path.Length == 0 ? property.JsonName : $"{path}.{property.JsonName}";
            Walk(property.Property.GetValue(value), child, depth + 1, visited, ref offenders);
        }
    }

    // الأنواع القيمية المركّبة (struct) تُكنس مثل الأصناف، فـ DTO قد يحمل واحداً منها.
    // وتُستثنى الأنواع القيمية البسيطة قبل ذلك بـ IsPrimitive وما يلي
    private static bool IsWalkableStruct(Type type) =>
        type.IsValueType && !type.IsPrimitive && type.Assembly == OwnAssembly;

    private static WalkableProperty[] PropertiesOf(Type type) =>
        PropertyCache.GetOrAdd(type, static t =>
        [
            .. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Select(p => new WalkableProperty(p, ResolveJsonName(p)))
        ]);

    // الرسالة تسمّي الحقل كما أرسله العميل لا كما سُمّي في C#. والسمة تُقرأ إن وُجدت
    // كي لا تنحرف التسمية عن الحمولة يوم تُستعمل
    private static string ResolveJsonName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? ToJsonName(property.Name);

    private static string ToJsonName(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);

    // القيم المقبولة تُعرَض بأرقامها لأنها ما يعبر السلك وما يصفه العقد — والعقد
    // يعلنها أصلاً في `enum: [...]`، فلا يكشف السرد ما هو مستور
    private static string Describe(Type enumType, object value, string path)
    {
        var allowed = string.Join("، ", Enum.GetValues(enumType).Cast<object>().Select(v => Convert.ToInt64(v)));

        return $"القيمة {Convert.ToInt64(value)} خارج المدى المسموح للحقل {path}. القيم المقبولة: {allowed}.";
    }
}
