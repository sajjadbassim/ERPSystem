using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ErpApi.Common.Validation;

// يُسند رسالة عربية افتراضية لكل `[Required]` لم يُصرَّح لها برسالة — سواء كتبها أحد
// أم أضافها MVC ضمناً لمرجع غير قابل للعدم حين يكون `<Nullable>enable</Nullable>`.
//
// **هذا هو الخطر المنهجي الذي سجّله L34، مسدوداً بنيوياً لا بالتذكّر.** كان مكتوباً
// في STATE: «أي خاصية مرجعية غير قابلة للعدم تُضاف مستقبلاً بلا
// ‏`[Required(ErrorMessage = ...)]` صريح ستُنتج رسالة إنجليزية بالآلية نفسها».
// والسمة على كل خاصية تفشل مفتوحة — نسيانُها صامت، ووقع مرة مع `Lines`.
//
// **وقد كُشف بالقياس موضع لم يُفحص قط:** `[Required]` الضمني يقع على **معامل الفعل**
// نفسه أيضاً، فكانت كل حمولة يتعذّر بناؤها تُرجع «The request field is required.»
// حاملةً اسم المعامل في C#. وفحص STATE السابق شمل الـDTOs وحدها.
//
// والتصريح الصريح يبقى مقدَّماً: هذا يملأ الفراغ ولا يستولي على ما كُتب.
public sealed class ArabicRequiredMetadataProvider : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>())
        {
            if (attribute.ErrorMessage is not null || attribute.ErrorMessageResourceName is not null)
            {
                continue;
            }

            attribute.ErrorMessage = DescribeRequirement(context);
        }
    }

    private static string DescribeRequirement(ValidationMetadataProviderContext context)
    {
        var name = context.Key.Name;

        if (context.Key.MetadataKind != ModelMetadataKind.Parameter)
        {
            return $"الحقل {ArabicValidationMessages.ToJsonName(name ?? string.Empty)} مطلوب.";
        }

        // معامل من نوع مرجعي معرَّف في هذا التجميع هو DTO يُبنى من الجسم بحكم
        // ‏[ApiController]. والتمييز بالنوع لا بالتخمين: معامل نصّي من الاستعلام يوماً
        // يأخذ الفرع الثاني بصدق بدل أن يُنسب إلى الجسم
        var isBodyDto = context.Key.ModelType.IsClass
                        && context.Key.ModelType.Assembly == typeof(ArabicRequiredMetadataProvider).Assembly;

        return isBodyDto
            ? ArabicValidationMessages.BodyRequired
            : $"القيمة {ArabicValidationMessages.ToJsonName(name ?? string.Empty)} مطلوبة.";
    }
}
