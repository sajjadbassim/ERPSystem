using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ErpApi.Common.Validation;

// بند 8.4: رسائل المستخدم بالعربية، ولا تسريب لبنية داخلية.
//
// وقد كان مخروقاً على مسار **ربط النموذج** كله، لا على الـenum وحده. المقيس:
// ‏`ErpApi.Core.Constants.AccountType` و`System.Guid` و``System.Nullable`1[System.Guid]``
// تعبر إلى العميل، ومعها `Path` و`LineNumber` و`BytePositionInLine`، ونصّ محلّل JSON
// ‏(«'t' is an invalid start of a property name»)، و«A non-empty request body is required».
//
// **ولهذا لم يُبنَ محوّل enum:** كان سيصلح حالتين من سبع ويترك خمساً تسرّب. الإصلاح
// عند النقطة التي تلتقي عندها المسارات كلها — وهي واحدة بحكم بند 6.3.
//
// **قائمة سماح لا قائمة حظر — والتمييز بالكتابة لا بالعبارة.** المحاولة الأولى ميّزت
// خطأ إلغاء التسلسل بحمله استثناءً (`ModelError.Exception`)، فسقطت بالقياس: MVC يحوّل
// ‏`InputFormatterException` إلى **نصّ** ويُسقط الاستثناء، فلا يبقى ما يُميَّز به بنيوياً.
//
// والمعتمَد أن **كل رسالة خالية من الحرف العربي ليست من تأليفنا**، فتُستبدل بلا استثناء.
// وهذا هو منطق `M02` («صفر number في العقد كله») ومنطق قائمة السماح في §4.2: صفة على
// المجموع تفشل مغلقة، بينما لائحة عبارات معروفة تفشل مفتوحة — الرسالة الجديدة من نسخة
// إطار لاحقة ببساطة ليست فيها.
public static class ArabicValidationMessages
{
    private const string BodyUnreadable = "تعذّرت قراءة جسم الطلب";

    // يُشير إليه `ArabicRequiredMetadataProvider` ليبقى النصّ معرَّفاً مرة واحدة:
    // ‏`Describe` يُسقطه عند اجتماعه بغيره، والمطابقة على نصّ مكرَّر تنحرف بصمت
    internal const string BodyRequired = "جسم الطلب مطلوب.";

    public static string Describe(ModelStateDictionary modelState)
    {
        var messages = modelState
            .Where(entry => entry.Value is not null && entry.Value.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => (entry.Key, error.ErrorMessage)))
            .Select(entry => IsOurs(entry.ErrorMessage)
                ? entry.ErrorMessage
                : DescribeUnreadableBody(entry.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // «جسم الطلب مطلوب» حين يجتمع بغيره **نتيجة لا واقعة مستقلة**: تعذُّر بناء الجسم
        // يترك المعامل عدماً، فيضيف MVC خطأ «مطلوب» فوق السبب الحقيقي. وذكرهما معاً
        // يضاعف الرسالة بلا معلومة
        if (messages.Count > 1)
        {
            messages.RemoveAll(message => string.Equals(message, BodyRequired, StringComparison.Ordinal));
        }

        return messages.Count > 0
            ? string.Join(" | ", messages)
            : $"{BodyUnreadable}.";
    }

    // نطاقات العربية في يونيكود: الأساسي، والملحق، والصيغ العرضية. رسالة إطار إنجليزية
    // لا تحوي واحداً منها، ورسالتنا تحويه ولو حملت اسم حقل لاتينياً بجواره
    private static bool IsOurs(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && message.Any(character => character is >= '؀' and <= 'ۿ'
                                        or >= 'ݐ' and <= 'ݿ'
                                        or >= 'ﭐ' and <= '﷿'
                                        or >= 'ﹰ' and <= '﻿');

    // مفتاح الخطأ مسار JSON: `$.accountType` أو `$` للجذر. اسم الحقل كما أرسله العميل
    // ليس تسريباً — هو عقد الـAPI المعلَن؛ والمُسرَّب هو اسم النوع في C# ونصّ المكتبة
    private static string DescribeUnreadableBody(string key)
    {
        var field = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : null;

        return string.IsNullOrEmpty(field)
            ? $"{BodyUnreadable}: النصّ المُرسَل ليس JSON صالحاً."
            : $"{BodyUnreadable}: قيمة الحقل {field} لا تطابق النوع المتوقَّع.";
    }

    // نقطة الامتداد التي صمّمها الإطار لرسائل الرابط. استبدالها **كلها** يفشل مغلقاً:
    // لا تبقى رسالة إنجليزية واحدة يمكن أن تظهر من هذا المصدر
    public static void Apply(DefaultModelBindingMessageProvider provider)
    {
        provider.SetMissingBindRequiredValueAccessor(
            field => $"القيمة المطلوبة للحقل {ToJsonName(field)} غير موجودة في الطلب.");

        provider.SetMissingKeyOrValueAccessor(
            () => "المفتاح أو القيمة مفقودة.");

        provider.SetMissingRequestBodyRequiredValueAccessor(
            () => "جسم الطلب مطلوب ولا يجوز أن يكون فارغاً.");

        provider.SetValueMustNotBeNullAccessor(
            _ => "القيمة المُرسَلة لا يجوز أن تكون عدماً.");

        // القيمة المُرسَلة **لا تُعاد** إلى العميل في أي من هذه الرسائل: إعادتها تعكس
        // مدخلاً عشوائياً في جسم الاستجابة بلا فائدة تشخيصية تُذكر
        provider.SetAttemptedValueIsInvalidAccessor(
            (_, field) => $"القيمة المُرسَلة للحقل {ToJsonName(field)} غير صالحة.");

        provider.SetNonPropertyAttemptedValueIsInvalidAccessor(
            _ => "القيمة المُرسَلة غير صالحة.");

        provider.SetUnknownValueIsInvalidAccessor(
            field => $"القيمة المُرسَلة للحقل {ToJsonName(field)} غير صالحة.");

        provider.SetNonPropertyUnknownValueIsInvalidAccessor(
            () => "القيمة المُرسَلة غير صالحة.");

        provider.SetValueIsInvalidAccessor(
            _ => "القيمة المُرسَلة غير صالحة.");

        provider.SetValueMustBeANumberAccessor(
            field => $"الحقل {ToJsonName(field)} يجب أن يكون عدداً.");

        provider.SetNonPropertyValueMustBeANumberAccessor(
            () => "القيمة المُرسَلة يجب أن تكون عدداً.");
    }

    // الحقل يُسمّى كما يراه العميل في الحمولة، لا كما سُمّي في C#
    internal static string ToJsonName(string name) =>
        string.IsNullOrEmpty(name) ? name : JsonNamingPolicy.CamelCase.ConvertName(name);
}
