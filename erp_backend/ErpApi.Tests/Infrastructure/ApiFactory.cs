using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ErpApi.Tests.Infrastructure;

// يُقلع التطبيق مع توجيهه إلى قاعدة الاختبار.
// تجاوز سلسلة الاتصال إلزامي: بدونه يقرأ التطبيق User Secrets ويكتب في قاعدة العمل الفعلية.
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    // مفتاح اختبار ثابت ومكشوف عمداً، منفصل تماماً عن مفتاح User Secrets الحقيقي.
    // كونه في الكود ليس تسريباً: لا يوقّع إلا رموزاً داخل قاعدة تُهدَم كل تشغيلة،
    // وتثبيته يجعل فشل التوقيع خطأ منطق لا اختلاف بيئة
    private const string TestSigningKey =
        "test-only-signing-key-not-a-production-secret-0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);

        builder.UseSetting("Jwt:Key", TestSigningKey);
        builder.UseSetting("Jwt:Issuer", "ErpApi.Tests");
        builder.UseSetting("Jwt:Audience", "ErpApi.Tests");

        builder.UseEnvironment("Testing");
    }
}
