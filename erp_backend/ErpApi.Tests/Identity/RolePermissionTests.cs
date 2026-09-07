using System.Net;
using ErpApi.Core.Constants;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Identity;

public class RolePermissionTests(TestDatabase database) : IdentityTestBase(database)
{
    // H01
    [Fact]
    public async Task H01_DeleteRole_SystemRole_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.DeleteAsync(Client,
            $"{AuthClient.RolesPath}/{identity.SystemRoleId}", accessToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // H02
    [Fact]
    public async Task H02_UpdateRole_RenamingSystemRole_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client,
            $"{AuthClient.RolesPath}/{identity.SystemRoleId}/rename", accessToken, new { name = "اسم آخر" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // H03
    [Fact]
    public async Task H03_Request_WithGrantedPermission_Succeeds()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={identity.Company.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // H04
    [Fact]
    public async Task H04_Request_WithoutRequiredPermission_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.DeleteAsync(Client,
            $"{AuthClient.RolesPath}/{identity.BranchRoleId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // H05 — سحب الدور يسري فوراً لأن جدول الربط يُحذف فعلياً لا منطقياً
    [Fact]
    public async Task H05_RevokingRole_TakesEffectImmediately()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        await PostingClient.ExecuteAsync(Database,
            "DELETE FROM UserRoles WHERE UserId = @user AND RoleId = @role",
            ("@user", identity.BranchUserId), ("@role", identity.BranchRoleId));

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={identity.Company.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // H06 — رمز غير معروف لا يمنح شيئاً: يفشل مغلقاً
    [Fact]
    public async Task H06_UnknownPermissionCode_GrantsNothing()
    {
        var identity = await NewIdentityAsync();
        await IdentityScenarioBuilder.GrantAsync(Database, identity.BranchRoleId,
            ["Totally.Unknown.Permission"], identity.Company.UserId);

        var (accessToken, _) = await LoginAsync(identity.BranchUserName);
        var response = await AuthClient.DeleteAsync(Client,
            $"{AuthClient.RolesPath}/{identity.BranchRoleId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // H07 — نقطة النهاية هي المصدر الوحيد، ويجب أن تطابق ثوابت الكود تماماً
    [Fact]
    public async Task H07_PermissionsEndpoint_MatchesConstantsExactly()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client, AuthClient.PermissionsPath, accessToken);
        var returned = await AuthClient.ReadStringArrayAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([.. Permissions.All.Order()], [.. returned.Order()]);
    }

    // H08
    [Fact]
    public async Task H08_UserWithNoRoles_HasNoPermissions()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.NoRoleUserName);

        var response = await AuthClient.GetAsync(Client,
            $"{AuthClient.JournalEntriesPath}?branchId={identity.Company.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
