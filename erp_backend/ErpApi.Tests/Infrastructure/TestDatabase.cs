using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Tests.Infrastructure;

// قاعدة اختبار حقيقية على SQL Server. ممنوع InMemory (بند 17).
// تُهدَم وتُبنى بالترحيلات مرة واحدة لكل تشغيلة، ولا تُحذف بعدها ليمكن فحصها عند الفشل.
public class TestDatabase : IAsyncLifetime
{
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("ERP_TEST_CONNECTION")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=ErpApi_Tests;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public Guid IqdCurrencyId { get; } = Guid.CreateVersion7();
    public Guid UsdCurrencyId { get; } = Guid.CreateVersion7();
    public Guid EurCurrencyId { get; } = Guid.CreateVersion7();

    // المستخدم الجذر الذي يبذره الترحيل. صار معرّفاً حقيقياً بعد أن أصبح CreatedByUserId
    // مفتاحاً أجنبياً: معرّف عشوائي هنا كان سيُسقط بذر كل سيناريو بانتهاك FK
    public Guid TestUserId { get; } = SystemUser.Id;

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        context.Currencies.AddRange(
            new Currency { Id = IqdCurrencyId, Code = "IQD", Name = "دينار عراقي", DecimalPlaces = 0, CreatedByUserId = TestUserId },
            new Currency { Id = UsdCurrencyId, Code = "USD", Name = "دولار أمريكي", DecimalPlaces = 2, CreatedByUserId = TestUserId },
            new Currency { Id = EurCurrencyId, Code = "EUR", Name = "يورو", DecimalPlaces = 2, CreatedByUserId = TestUserId });

        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public class TestDatabaseCollection : ICollectionFixture<TestDatabase>
{
    public const string Name = "TestDatabase";
}
