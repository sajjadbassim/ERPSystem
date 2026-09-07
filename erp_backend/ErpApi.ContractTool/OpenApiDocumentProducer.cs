using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace ErpApi.ContractTool;

// المنتِج الوحيد لوثيقة العقد. يستدعيه الكاتب في هذا المشروع، ويستدعيه اختبارا
// الانحراف في ErpApi.Tests. مسار إنتاج واحد لا اثنان: لو ولّد كلٌّ منهما الوثيقة
// بطريقته لصار خط الأساس والمقارَن به من مصدرين، فيفشل الاختبار بلا انحراف أو
// ينجح مع انحراف حقيقي.
public static class OpenApiDocumentProducer
{
    // اسم الوثيقة الافتراضي الذي يسجّله AddOpenApi() بلا وسيط في Program.cs
    public const string DocumentName = "v1";

    // مفتاح رمي ثابت ومكشوف عمداً، على غرار TestSigningKey في ApiFactory.
    // كونه في الكود ليس تسريباً: لا خادم يعمل، ولا رمز يُصدَر، ولا طلب يُوقَّع —
    // وجوده الوحيد لتجاوز الحارس الصريح في AddAppAuthentication الذي يرفض الإقلاع
    // بمفتاح أقصر من 32 محرفاً. الحارس يبقى كما هو ولا يُضعَّف.
    private const string InertSigningKey = "contract-tool-inert-key-not-a-secret-0000";

    // لا اتصال يقع: توليد الوثيقة انعكاس على نقاط النهاية والـ DTOs، و AddDbContext
    // كسول فلا يُحلّ AppDbContext أصلاً. القيمة موجودة لتكون السلسلة غير فارغة لا أكثر.
    private const string UnusedConnectionString = "Server=(contract-tool-unused);Database=(none)";

    public static async Task<string> ProduceAsync(CancellationToken ct = default)
    {
        // WebApplicationFactory يحدّد جذر المحتوى من MvcTestingAppManifest.json الذي
        // تولّده أهداف حزمة Mvc.Testing في مجلد المخرجات — لكنه يبحث عنه بمسار **نسبي
        // لمجلد العمل**. مشغّل الاختبارات يجعل مجلد العمل هو مجلد المخرجات فيجده،
        // و dotnet run يجعله مجلد المشروع فلا يجده، فيسقط إلى البحث عن ملف حلّ
        // ويفشل: «Solution root could not be located».
        // التثبيت هنا يجعل السلوك واحداً في المسارين، ويستعمل الآلية المقصودة نفسها.
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        try
        {
            return await ProduceCoreAsync(ct);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private static async Task<string> ProduceCoreAsync(CancellationToken ct)
    {
        await using var factory = new DocumentHostFactory();

        var services = factory.Services;

        // AddOpenApi يسجّل المزوّد مفتوحاً باسم الوثيقة. والسقوط إلى التسجيل غير
        // المفتاحي احتياط لا اعتماد: إن تغيّر شكل التسجيل في نسخة لاحقة نريد رسالة
        // صريحة لا NullReferenceException.
        var provider = services.GetKeyedService<IOpenApiDocumentProvider>(DocumentName)
                       ?? services.GetService<IOpenApiDocumentProvider>()
                       ?? throw new InvalidOperationException(
                           $"تعذّر الحصول على IOpenApiDocumentProvider للوثيقة '{DocumentName}'. "
                           + "تحقّق من استدعاء AddOpenApi() في Program.cs ومن اسم الوثيقة.");

        var document = await provider.GetOpenApiDocumentAsync(ct);

        // نسخة المواصفة تُقرأ من إعدادات التطبيق لا تُثبَّت هنا: تثبيتها يجعل الملف
        // المكتوب يخالف ما يخدمه MapOpenApi لو غُيّرت الإعدادات يوماً
        var version = services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>()
            .Get(DocumentName)
            .OpenApiVersion;

        return await document.SerializeAsJsonAsync(version, ct);
    }

    private sealed class DocumentHostFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", UnusedConnectionString);
            builder.UseSetting("Jwt:Key", InertSigningKey);

            // الوثيقة يجب أن تصف سطح الإنتاج لا سطح التطوير. وقد ثبت بكنس شامل على
            // المشروع أن المشروط الوحيد بالبيئة هو MapOpenApi() نفسه — أي قناة تسليم
            // الوثيقة، لا محتواها. فالتوليد تحت Production أمين ولا ينقص شيئاً.
            builder.UseEnvironment("Production");
        }
    }
}
