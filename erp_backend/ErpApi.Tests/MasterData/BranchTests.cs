using System.Net;
using System.Text.Json;
using ErpApi.Core.Constants;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.MasterData;

public class BranchTests(TestDatabase database) : IdentityTestBase(database)
{
    // البذر يضع ثلاثة فروع في كل شركة (BR1..BR3). هذه تُكمّلها إلى عشرة، فيصير
    // «فرعان من عشرة» في K25 نسبةً حقيقية لا فرعين من ثلاثة
    private async Task<List<Guid>> SeedExtraBranchesAsync(Scenario company, int count)
    {
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var id = Guid.CreateVersion7();

            await PostingClient.ExecuteAsync(Database,
                """
                INSERT INTO Branches (Id, CompanyId, Code, Name, IsActive, IsDeleted,
                                      CreatedAt, CreatedByUserId)
                VALUES (@id, @company, @code, @name, 1, 0, SYSUTCDATETIME(), @createdBy);
                """,
                ("@id", id), ("@company", company.CompanyId),
                ("@code", $"EX{i:D2}"), ("@name", $"فرع إضافي {i}"),
                ("@createdBy", company.UserId));

            ids.Add(id);
        }

        return ids;
    }

    private static List<string?> BranchIdsOf(JsonElement page) =>
        [.. page.GetProperty("data").EnumerateArray().Select(b => b.GetProperty("id").GetString())];

    // K25 — الحدّ الثاني: من لا يحمل AllBranches يرى فروعه المخصَّصة وحدها.
    //
    // ‏**وفحص الصلاحية الساكن لا يقول هذا**: `MasterData.Read` يأذن بالقراءة ولا
    // يحدّ المقروء، فبلا مرشِّح على `UserBranches` تعود فروع الشركة العشرة كلها
    [Fact]
    public async Task K25_ListBranches_WithoutAllBranches_ReturnsOnlyAssignedBranches()
    {
        var identity = await NewIdentityAsync();

        await SeedExtraBranchesAsync(identity.Company, 7);

        // دور الفرع بلا MasterData.Read في البذر، فبدونها يرسب الاختبار بـ403
        // لسبب غير المقصود. والمنح هنا **لا يمنح AllBranches** — وهو بيت القصيد
        await IdentityScenarioBuilder.GrantAsync(
            Database, identity.BranchRoleId, [Permissions.MasterDataRead], identity.Company.UserId);

        // البذر خصّص له BR1 وحده، فيصير فرعان من عشرة
        await IdentityScenarioBuilder.AssignBranchAsync(
            Database, identity.BranchUserId, identity.Company.SecondBranchId, identity.Company.UserId);

        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.GetAsync(Client, MasterDataClient.BranchesPath, accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ids = BranchIdsOf(await MasterDataClient.ReadDataAsync(response));

        Assert.Equal(2, ids.Count);
        Assert.Contains(identity.Company.BranchId.ToString(), ids);
        Assert.Contains(identity.Company.SecondBranchId.ToString(), ids);
        Assert.DoesNotContain(identity.Company.BranchWithoutSequenceId.ToString(), ids);
    }

    // K26 — الحدّ الأول: حاملُ AllBranches يرى كل فروع **شركته** ولا يعبرها.
    // نظير G10 على البيانات الأساسية: الصلاحية الشاملة تتسع داخل الشركة لا فوقها
    [Fact]
    public async Task K26_ListBranches_WithAllBranches_SeesOwnCompanyOnly()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);

        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client, MasterDataClient.BranchesPath, accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ids = BranchIdsOf(await MasterDataClient.ReadDataAsync(response));

        // فروع شركته الثلاثة حاضرة — والإدارية لا تضيق عن فرع لم يُخصَّص لها
        Assert.Contains(identity.Company.BranchId.ToString(), ids);
        Assert.Contains(identity.Company.SecondBranchId.ToString(), ids);
        Assert.Contains(identity.Company.BranchWithoutSequenceId.ToString(), ids);

        // ولا فرع واحد من الشركة الأخرى
        Assert.DoesNotContain(otherCompany.BranchId.ToString(), ids);
        Assert.DoesNotContain(otherCompany.SecondBranchId.ToString(), ids);
        Assert.DoesNotContain(otherCompany.BranchWithoutSequenceId.ToString(), ids);
    }

    // K27 — العدّاد يخضع للمرشِّح نفسه الذي تخضع له الصفحة.
    //
    // ‏**لولاه لمرّ نصف إصلاح صامتاً**: صفحةٌ مقيَّدة بثلاثة وعدّادٌ يقول «ثلاثون»
    // يُنتج ترقيماً كاذباً وصفحات فارغة — والواجهة تصدّقه. نظير ما يحرسه G10 بجعل
    // `Filter` مصدراً واحداً للقائمة والعدّ معاً
    [Fact]
    public async Task K27_ListBranches_TotalCount_MatchesScopedCountNotSystemWide()
    {
        var identity = await NewIdentityAsync();
        await ScenarioBuilder.CreateAsync(Database);

        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client, MasterDataClient.BranchesPath, accessToken);
        response.EnsureSuccessStatusCode();

        var page = await MasterDataClient.ReadDataAsync(response);

        // ‏**العدّاد عدد على السلك لا نصّ — مقيس لا مفترض.** محوّل R-API-03 يخصّ
        // ‏`decimal` وحده، و`TotalCount` من نوع `int`. (العقد يصفه `["integer","string"]`
        // فيقبل الاثنين، والمقيس `Number`.) والفرض المعاكس أرسب هذا الاختبار أول مرة
        // بـ`InvalidOperationException` — رسوبٌ لسبب غير المقصود لا يُعدّ برهاناً
        var totalCount = page.GetProperty("totalCount").GetInt32();

        Assert.Equal(3, totalCount);
        Assert.Equal(totalCount, BranchIdsOf(page).Count);
    }

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
