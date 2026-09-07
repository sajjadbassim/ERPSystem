using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Accounting;

public class AccountTests(TestDatabase database) : IdentityTestBase(database)
{
    private static object NewAccount(
        Guid companyId, string code, string name,
        byte accountType = 1, byte normalBalance = 0, bool isPostable = true,
        Guid? parentAccountId = null, Guid? currencyId = null) =>
        new
        {
            companyId,
            code,
            name,
            accountType,
            normalBalance,
            isPostable,
            parentAccountId,
            currencyId
        };

    // L01
    [Fact]
    public async Task L01_CreateAccount_ValidPayload_ReturnsCreated()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1150", "صندوق فرعي"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // L02 — التفرّد داخل الشركة، يحرسه UQ_Account_CompanyId_Code
    [Fact]
    public async Task L02_CreateAccount_DuplicateCodeInSameCompany_IsConflict()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1101", "صندوق مكرر"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L03 — الأصول والمصروفات طبيعتها مدينة، والخصوم وحقوق الملكية والإيرادات دائنة.
    // يحرسه CK_Account_NormalBalanceMatchesType، والفحص هنا يمنع الرحلة إلى القاعدة
    [Fact]
    public async Task L03_CreateAccount_NormalBalanceContradictsType_IsBadRequest()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        // أصل (1) بطبيعة دائنة (1) — تناقض
        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1151", "أصل بطبيعة دائنة",
                accountType: 1, normalBalance: 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // L04 — حساب دور نظامي غير قابل للترحيل يعطّل كل قيد يظهر فيه باقي تقريب.
    // يحرسه CK_Account_SystemRolePostable
    [Fact]
    public async Task L04_CreateAccount_SystemRoleButNotPostable_IsBadRequest()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            new
            {
                companyId = identity.Company.CompanyId,
                code = "5910",
                name = "دور نظامي غير قابل للترحيل",
                accountType = (byte)5,
                normalBalance = (byte)0,
                isPostable = false,
                systemAccountRole = (byte)5
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // L05 — الاسم محدَّد سلفاً في ERP-CORE-RULES تحت R-LIFE-06، نظير K07 للعملة.
    // تعطيل حساب له حركة يجعل EF يولّد INNER JOIN بمرشِّح المبدأ عند التنقّل،
    // فتختفي سطوره من كل استعلام دفتري — ميزان متوازن ينقصه حساب دون أن يُعلن ذلك
    [Fact]
    public async Task L05_DeactivateAsync_WhenAccountHasJournalLines_ThrowsConflictException()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var request = identity.Company.NewPost(
            identity.Company.Debit(identity.Company.CashAccountId, 750m),
            identity.Company.Credit(identity.Company.RevenueAccountId, 750m));
        await PostingClient.PostAsync(Database, request);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.DeactivateAccountPath(identity.Company.CashAccountId), accessToken, new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L06 — القاعدة تمنع تعطيل ما له حركة لا التعطيل مطلقاً
    [Fact]
    public async Task L06_DeactivateAccount_WithoutJournalLines_Succeeds()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var created = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1152", "حساب بلا حركة"));
        created.EnsureSuccessStatusCode();

        var data = await AccountingClient.ReadDataAsync(created);
        var id = Guid.Parse(AccountingClient.Field(data, "id")!);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.DeactivateAccountPath(id), accessToken, new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // L07 — أب من شركة أخرى يكسر شجرة الحسابات. يحرسه TR_Account_ValidateConfiguration (51007)
    [Fact]
    public async Task L07_CreateAccount_ParentFromAnotherCompany_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1153", "حساب بأب أجنبي",
                parentAccountId: otherCompany.HeaderAccountId));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"المتوقَّع رفض، والوارد: {response.StatusCode}");

        // التريجر 51007 يرمي رسالة عربية — والفحص أنها وصلت بلا رقمها ولا اسم التريجر
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L08 — الفشل مغلق
    [Fact]
    public async Task L08_CreateAccount_WithoutAccountManage_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1154", "حساب ممنوع"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
