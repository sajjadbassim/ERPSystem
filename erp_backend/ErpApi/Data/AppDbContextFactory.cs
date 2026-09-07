using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ErpApi.Data;

// يُستخدم وقت توليد الترحيلات وتطبيقها. يقرأ نفس مصادر التطبيق حتى لا يختلف هدف
// الترحيل عن هدف التشغيل. سلسلة الاتصال من User Secrets أو متغير بيئة، لا من الملفات المرفوعة (بند 18).
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("ERP_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // الفشل الصريح مقصود: السقوط الصامت إلى قاعدة افتراضية يطبّق الترحيل على قاعدة
        // غير مقصودة ويُظهر النجاح، فيظن المطوّر أن قاعدته حُدّثت وهي لم تُمس
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "لا توجد سلسلة اتصال. اضبط ERP_CONNECTION أو ConnectionStrings:DefaultConnection في User Secrets.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
