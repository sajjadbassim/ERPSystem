using ErpApi.Core.Models;
using ErpApi.Data;
using ErpApi.DevSeedTool;
using ErpApi.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// ‏أداة تنفيذية مستقلة تُستدعى **بيد إنسان**، على غرار `ErpApi.ContractTool`.
// والحاجز بنيويّ لا إجرائي: لا مشغّل اختبارات يبلغها، ولا إقلاع خادم يستدعيها،
// فلا تكتب صفاً إلا حين يُشغّلها أحد قاصداً. وبذرٌ عند الإقلاع مشروط بالبيئة كان
// سيكتب عند كل تشغيل، ويكفي متغيّر بيئة منسيّ على خادم لينشط — وهو من عائلة
// ‏`Database.Migrate()` التلقائي الذي يمنعه بند 16.

try
{
    DevSeedGuards.EnsureDevelopmentEnvironment();

    var configuration = new ConfigurationBuilder()
        // ‏أسرار ErpApi نفسها (المعرّف مشترك في csproj): سلسلة واحدة لا نسختان.
        // والإرساء على `DevSeedGuards` لا على `Program`: الأخير مُصدَّر من `ErpApi`
        // أيضاً (لأجل `WebApplicationFactory`) فيلتبس الاسمان
        .AddUserSecrets(typeof(DevSeedGuards).Assembly)
        .AddEnvironmentVariables()
        .Build();

    var connectionString = configuration.GetConnectionString("DefaultConnection");

    DevSeedGuards.EnsureLocalConnection(connectionString);

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<IConfiguration>(configuration);

    // ‏تسجيل التطبيق نفسه لا نسخة مبسَّطة منه: `IPasswordHasher<User>` الذي يُحقن
    // هنا هو عين الذي يحقنه `AuthService` في مسار الدخول الحقيقي
    services.AddAppPersistence(configuration);
    services.AddAppServices(configuration);

    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();

    var seeder = new DevSeeder(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());

    var report = await seeder.RunAsync();

    Console.WriteLine("بذرة التطوير المحلية — تمّت.");

    foreach (var line in report.Created)
    {
        Console.WriteLine($"  + أُنشئ: {line}");
    }

    foreach (var line in report.Skipped)
    {
        Console.WriteLine($"  = قائم: {line}");
    }

    Console.WriteLine();
    Console.WriteLine($"  الدخول: {DevSeedData.UserName} / {DevSeedData.Password}");
    Console.WriteLine("  ⚠ بيانات دخول DEV محلية — لا تُستخدم في الإنتاج.");

    return 0;
}
catch (DevSeedRefusedException refusal)
{
    // ‏رمز خروج مميَّز: الرفض قرار حارس لا عطب. من يستدعي الأداة في سكربت يفرّق
    // بين «مُنعت» و«فشلت»
    Console.Error.WriteLine($"رُفض البذر. {refusal.Message}");
    return 2;
}
catch (Exception failure)
{
    // ‏السلسلة الداخلية كاملة: خطأ EF الخارجي («حدث خطأ أثناء حفظ التغييرات») لا
    // يقول شيئاً، والسبب الحقيقي — قيد أو تريجر أو عمود — يقع في الداخلي. ورسالةٌ
    // تخفي سببها تُطيل التشخيص بلا مقابل
    Console.Error.WriteLine("فشل البذر:");

    for (Exception? current = failure; current is not null; current = current.InnerException)
    {
        Console.Error.WriteLine($"  {current.GetType().Name}: {current.Message}");
    }

    return 1;
}
