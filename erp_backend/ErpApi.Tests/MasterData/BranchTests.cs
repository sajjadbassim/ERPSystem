using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.MasterData;

public class BranchTests(TestDatabase database) : IdentityTestBase(database)
{
    // K15
    [Fact]
    public async Task K15_CreateBranch_ValidPayload_ReturnsCreated()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = identity.Company.CompanyId, code = "BR9", name = "فرع جديد" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // K16 — التفرّد داخل الشركة، يحرسه UQ_Branch_CompanyId_Code
    [Fact]
    public async Task K16_CreateBranch_DuplicateCodeInSameCompany_IsConflict()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = identity.Company.CompanyId, code = "BR1", name = "فرع مكرر" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // K17 — التفرّد على مستوى الشركة لا النظام: نفس الرمز مقبول في شركة أخرى.
    // الرمز يُنشأ هنا في الشركتين تباعاً لا يُؤخذ من البذر: البذر يضع BR1 في **كل**
    // شركة يبنيها، فاختباره كان سيصطدم بتكرار داخل الشركة الثانية لا بحدّ الشركة
    [Fact]
    public async Task K17_CreateBranch_SameCodeInAnotherCompany_IsAccepted()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var first = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = identity.Company.CompanyId, code = "BRX", name = "فرع في الشركة الأولى" });
        first.EnsureSuccessStatusCode();

        var second = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = otherCompany.CompanyId, code = "BRX", name = "فرع في شركة أخرى" });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    // K18 — الفشل مغلق: بلا صلاحية إدارة الفروع لا إنشاء
    [Fact]
    public async Task K18_CreateBranch_WithoutBranchManage_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = identity.Company.CompanyId, code = "BR8", name = "فرع ممنوع" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
