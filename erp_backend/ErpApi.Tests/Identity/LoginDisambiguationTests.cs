using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Identity;

// ══════════════════════════════════════════════════════════════════════════
//  إضافة خارج الـ46 المعتمدة — مجموعة X
//
//  المصفوفة المعتمدة تركت ثغرة: E07 يثبت أن نفس اسم المستخدم مسموح في شركتين،
//  وE01 يسجّل الدخول باسم وكلمة مرور بلا معرّف شركة. اجتماع الحالتين يجعل الهوية
//  ملتبسة، ولا اختبار من الـ46 يغطيه — لأن E07 يُنشئ الاسم المكرر ولا يسجّل به دخولاً.
//
//  هذا الملف يوثّق سلوك الحقل الاختياري companyCode. لا يُحتسب ضمن الـ46،
//  ويُعتمد أو يُرفض على حدة.
// ══════════════════════════════════════════════════════════════════════════
public class LoginDisambiguationTests(TestDatabase database) : IdentityTestBase(database)
{
    // X01 — الاسم مكرر بين شركتين، ورمز الشركة حاضر: الالتباس مرفوع فينجح الدخول
    [Fact]
    public async Task X01_LoginAsync_DuplicateUserNameAcrossCompanies_WithCompanyCode_Succeeds()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);

        // نفس الاسم في شركة أخرى — مقبول بنيوياً، وهو ما يثبته E07
        await IdentityScenarioBuilder.InsertUserAsync(Database, Guid.CreateVersion7(),
            otherCompany.CompanyId, identity.AdminUserName, "hash", true, otherCompany.UserId);

        var response = await AuthClient.LoginAsync(
            Client, identity.AdminUserName, IdentityScenario.Password, identity.Company.CompanyCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var accessToken = await AuthClient.ReadFieldAsync(response, "accessToken");
        Assert.NotEmpty(accessToken);
        Assert.NotEmpty(await AuthClient.ReadFieldAsync(response, "refreshToken"));

        // 200 ورمزان لا يكفيان: مع اسمين متطابقين في شركتين، الاستجابة الناجحة
        // للهوية الخاطئة تبدو مطابقة تماماً. المطالبة وحدها تفصل بينهما
        var claims = AuthClient.DecodeJwtClaims(accessToken);

        Assert.Equal(identity.Company.CompanyId.ToString(), claims["erp:cid"]);
        Assert.Equal(identity.AdminUserId.ToString(), claims["erp:uid"]);
    }
}
