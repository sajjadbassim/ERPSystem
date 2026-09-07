using Microsoft.Extensions.DependencyInjection;

namespace ErpApi.Tests.Infrastructure;

[Collection(TestDatabaseCollection.Name)]
public abstract class IdentityTestBase(TestDatabase database) : IDisposable
{
    private ApiFactory? _factory;

    protected TestDatabase Database { get; } = database;

    private ApiFactory Factory => _factory ??= new ApiFactory(Database.ConnectionString);

    protected HttpClient Client => Factory.CreateClient();

    // نطاق خدمات من التطبيق نفسه، فيه AppDbContext **بـ Interceptor التدقيق مسجَّلاً**.
    // لازم لاختبار تغييرات لا تملك نقطة نهاية بعد (منح دور، سحب فرع): تلك تُبنى في §4.3.
    // ولا HttpContext هنا، فالفاعل يسقط إلى المستخدم الجذر — وهو مسار مقصود يُختبر صراحةً
    protected AsyncServiceScope CreateAppScope() => Factory.Services.CreateAsyncScope();

    protected Task<IdentityScenario> NewIdentityAsync() => IdentityScenarioBuilder.CreateAsync(Database);

    protected async Task<(string AccessToken, string RefreshToken)> LoginAsync(string userName)
    {
        var response = await AuthClient.LoginAsync(Client, userName, IdentityScenario.Password);
        response.EnsureSuccessStatusCode();

        return (await AuthClient.ReadFieldAsync(response, "accessToken"),
                await AuthClient.ReadFieldAsync(response, "refreshToken"));
    }

    protected Task<int> RevokedTokenCountAsync(Guid userId) =>
        PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM RefreshTokens WHERE UserId = @user AND RevokedAt IS NOT NULL",
            ("@user", userId))!;

    protected Task<int> ActiveTokenCountAsync(Guid userId) =>
        PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM RefreshTokens WHERE UserId = @user AND RevokedAt IS NULL",
            ("@user", userId))!;

    protected Task<Guid> SecurityStampAsync(Guid userId) =>
        PostingClient.ScalarAsync<Guid>(Database,
            "SELECT SecurityStamp FROM Users WHERE Id = @user", ("@user", userId));

    protected Task<string?> PasswordHashAsync(Guid userId) =>
        PostingClient.ScalarAsync<string>(Database,
            "SELECT PasswordHash FROM Users WHERE Id = @user", ("@user", userId));

    // تسجيل استعمال النطاق الشامل. جدول التدقيق يُبنى في §4.2، والاختبار يفرض وجوده
    protected Task<int> AllBranchesAuditCountAsync(Guid userId) =>
        PostingClient.ScalarAsync<int>(Database,
            """
            SELECT COUNT(*) FROM AuditLogEntries
            WHERE UserId = @user AND Action = 'Query.AllBranches'
            """,
            ("@user", userId))!;

    public void Dispose()
    {
        _factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
