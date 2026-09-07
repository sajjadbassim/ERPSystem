using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Audit;

// المسار الثاني للكتابة: أحداث الوصول. لا تمرّ بالـ Interceptor لأنها ليست تغيير كيان —
// لا صف تغيّر، ولا «قبل» ولا «بعد». تُكتب باستدعاء صريح من UserService
public class AuditAccessEventTests(TestDatabase database) : IdentityTestBase(database)
{
    // J18 — شكل حدث الوصول. G05 يفحص وقوعه، وهذا يفحص أنه سُجِّل سليماً:
    // الفاعل حقيقي، وحقلا القيمة فارغان لأنه لا شيء تغيّر
    [Fact]
    public async Task J18_AllBranchesAccessEvent_RecordsActorWithNoBeforeOrAfter()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client, AuthClient.JournalEntriesPath, accessToken);
        response.EnsureSuccessStatusCode();

        var rows = await AuditClient.ReadAsync(Database, userId: identity.AdminUserId);
        var accessEvent = Assert.Single(rows, r => r.Action == AuditClient.QueryAllBranches);

        Assert.Equal(identity.AdminUserId, accessEvent.UserId);
        Assert.Null(accessEvent.BeforeJson);
        Assert.Null(accessEvent.AfterJson);

        // العمودان إلزاميان في المخطط، فحدث الوصول يملؤهما بالمورد ونطاقه لا بالفراغ
        Assert.Equal("Branch", accessEvent.EntityName);
        Assert.Equal("*", accessEvent.EntityKey);
    }
}
