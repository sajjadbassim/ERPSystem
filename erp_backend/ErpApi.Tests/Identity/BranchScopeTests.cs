using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Identity;

public class BranchScopeTests(TestDatabase database) : IdentityTestBase(database)
{
    // G01
    [Fact]
    public async Task G01_ReadEntries_BranchInsideScope_Succeeds()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={identity.Company.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // G02
    [Fact]
    public async Task G02_ReadEntries_BranchOutsideScope_IsDenied()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={identity.Company.SecondBranchId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // G03 — الكتابة أخطر من القراءة. يعتمد على نقطة نهاية الترحيل (§4.3)
    [Fact]
    public async Task G03_PostEntry_BranchOutsideScope_IsDenied()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.PostAsync(Client, AuthClient.JournalEntriesPath, accessToken,
            new { branchId = identity.Company.SecondBranchId, postingDate = "2026-06-15" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // G04
    [Fact]
    public async Task G04_ReadEntries_WithAllBranchesPermission_SeesEveryBranch()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        foreach (var branchId in new[] { identity.Company.BranchId, identity.Company.SecondBranchId })
        {
            var response = await AuthClient.GetAsync(Client,
                $"{AuthClient.JournalEntriesPath}?branchId={branchId}", accessToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // G05 — النطاق الشامل يترك أثراً في كل مرة (يعتمد على AuditLog في §4.2)
    [Fact]
    public async Task G05_QueryWithAllBranchesScope_IsRecordedInAuditLog()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        await AuthClient.GetAsync(Client, AuthClient.JournalEntriesPath, accessToken);

        Assert.True(await AllBranchesAuditCountAsync(identity.AdminUserId) >= 1);
    }

    // G06 — الحالة التي بُني عليها قرار المعامل الإلزامي:
    // التقرير المالي يمرّ باستعلام مباشر خارج EF (بند 10.1)، فلا يحميه أي Query Filter
    [Fact]
    public async Task G06_TrialBalanceReadService_RespectsBranchScopeOutsideEf()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var inScope = await AuthClient.GetAsync(Client,
            $"{AuthClient.TrialBalancePath}?branchId={identity.Company.BranchId}", accessToken);
        var outOfScope = await AuthClient.GetAsync(Client,
            $"{AuthClient.TrialBalancePath}?branchId={identity.Company.SecondBranchId}", accessToken);

        Assert.Equal(HttpStatusCode.OK, inScope.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outOfScope.StatusCode);
    }

    // G07 — نطاق فارغ يفشل مغلقاً: لا شيء، لا كل شيء
    [Fact]
    public async Task G07_ReadEntries_UserWithNoBranchesAndNoAllBranches_SeesNothing()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.NoScopeUserName);

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={identity.Company.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // G08 — النطاق الشامل لا يتجاوز حدود الشركة
    [Fact]
    public async Task G08_AllBranchesScope_DoesNotCrossCompanyBoundary()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={otherCompany.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // G09 — سحب الفرع يسري فوراً بلا انتظار انتهاء الرمز
    [Fact]
    public async Task G09_RemovingBranchFromScope_TakesEffectImmediately()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        await PostingClient.ExecuteAsync(Database,
            "DELETE FROM UserBranches WHERE UserId = @user AND BranchId = @branch",
            ("@user", identity.BranchUserId), ("@branch", identity.Company.BranchId));

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={identity.Company.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
