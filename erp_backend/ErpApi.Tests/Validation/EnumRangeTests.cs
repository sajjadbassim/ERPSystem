using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Validation;

// المجموعة N — مدى الـenum يُفرض عند حدّ الـAPI (قرار 2026-09-08).
//
// المادة هنا هي الحمولات الستّ المقيسة في جلسة القياس. أربع منها تبلغ ربط النموذج
// فعلاً (القيمة تسع في byte)، وهذه المجموعة تحرسها. أما `300` و`"Asset"` فتُرفضان
// قبل ذلك في `System.Text.Json` — وهما خرق بند 8.4 المنفصل، وحارسه في مكانه.
//
// **ما تشهد به هذه الاختبارات ليس أن الرفض وقع** — فالنظام كان يرفض الأربع أصلاً
// ويستقرّ بصفر صفوف. تشهد بأن الرفض وقع **في الموضع الصحيح وبالقول الصحيح**:
// الحالتان B وC كانتا تُرفَضان برسالة خدمة **واثقة وخاطئة** تتهم الطبيعة المحاسبية
// بينما الخلل في النوع، والحالتان A وF برسالة قيد قاعدة بيانات لا تسمّي الحقل.
public class EnumRangeTests(TestDatabase database) : IdentityTestBase(database)
{
    private const string ServiceNormalBalanceMessage = "الطبيعة المحاسبية تناقض نوع الحساب";
    private const string DatabaseConstraintMessage = "قيداً على مستوى قاعدة البيانات";

    private static object NewAccount(
        Guid companyId, string code, string name,
        int accountType = 1, int normalBalance = 0, int? systemAccountRole = null) =>
        new
        {
            companyId,
            code,
            name,
            accountType,
            normalBalance,
            isPostable = true,
            systemAccountRole
        };

    private static object NewEntry(Scenario company, int sourceModule) =>
        new
        {
            branchId = company.BranchId,
            postingDate = "2026-06-15",
            documentDate = "2026-06-15",
            description = "قيد بنوع مصدر خارج المدى",
            sourceModule,
            lines = new[]
            {
                new
                {
                    accountId = company.CashAccountId,
                    currencyId = company.BaseCurrencyId,
                    exchangeRate = "1.000000000000",
                    exchangeRateDate = "2026-06-15",
                    debitFC = "400.0000",
                    creditFC = "0.0000",
                    debitBase = "400.0000",
                    creditBase = "0.0000"
                },
                new
                {
                    accountId = company.RevenueAccountId,
                    currencyId = company.BaseCurrencyId,
                    exchangeRate = "1.000000000000",
                    exchangeRateDate = "2026-06-15",
                    debitFC = "0.0000",
                    creditFC = "400.0000",
                    debitBase = "0.0000",
                    creditBase = "400.0000"
                }
            }
        };

    // N01 — الحمولة A: نوع خارج المدى وطبيعة سليمة الشكل.
    // كانت تصل القاعدة ويلتقطها CK_Account_NormalBalanceMatchesType، فتعود برسالة
    // «قيد على مستوى قاعدة البيانات» لا تسمّي الحقل المخالف. وأيّ قيد يُبلَّغ قرارُ
    // SQL Server لا قرارُنا — فالعقد الذي يصفه الخادم عن نفسه ليس عقداً
    [Fact]
    public async Task N01_CreateAccount_AccountTypeOutOfRange_IsRejectedAtApiBoundary()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1160", "نوع خارج المدى",
                accountType: 99, normalBalance: 1));

        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("accountType", message, StringComparison.Ordinal);
        Assert.DoesNotContain(DatabaseConstraintMessage, message, StringComparison.Ordinal);
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // N02 — الحمولة B: النوع نفسه خارج المدى مع طبيعة مدينة.
    // هذه أخطر الأربع: `IsNormalBalanceConsistent` ثنائية، فنوع خارج المدى يسقط في
    // الفرع else ويُطالَب بطبيعة دائنة. النتيجة رسالة **تقول شيئاً محدَّداً غير صحيح**
    // لمن أرسل نوعاً غير موجود. والرسالة الواثقة الخاطئة أسوأ من العامة: العامة تقول «لا أعرف»
    [Fact]
    public async Task N02_CreateAccount_AccountTypeOutOfRange_DoesNotBlameNormalBalance()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1161", "نوع خارج المدى بطبيعة مدينة",
                accountType: 99, normalBalance: 0));

        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("accountType", message, StringComparison.Ordinal);
        Assert.DoesNotContain(ServiceNormalBalanceMessage, message, StringComparison.Ordinal);
    }

    // N03 — الحمولة C: النوع سليم والطبيعة خارج المدى. نظيرة B من الطرف المقابل:
    // الرسالة نفسها تصدر، و CK_Account_NormalBalance لا يُبلَّغ عنه أبداً
    [Fact]
    public async Task N03_CreateAccount_NormalBalanceOutOfRange_NamesNormalBalanceNotTheType()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1162", "طبيعة خارج المدى",
                accountType: 1, normalBalance: 7));

        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("normalBalance", message, StringComparison.Ordinal);
        Assert.DoesNotContain(ServiceNormalBalanceMessage, message, StringComparison.Ordinal);
    }

    // N04 — الحمولة F: enum **قابل للعدم** خارج المدى. الفرض يسري على `SystemAccountRole?`
    // كما يسري على غير القابل للعدم: العدم قيمة مقبولة، وقيمة خارج المدى ليست كذلك
    [Fact]
    public async Task N04_CreateAccount_NullableSystemAccountRoleOutOfRange_IsRejectedAtApiBoundary()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1163", "دور نظامي خارج المدى",
                accountType: 5, normalBalance: 0, systemAccountRole: 99));

        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("systemAccountRole", message, StringComparison.Ordinal);
        Assert.DoesNotContain(DatabaseConstraintMessage, message, StringComparison.Ordinal);
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // N05 — **حارس العمومية**: نقطة نهاية أخرى، DTO آخر، enum آخر لم تُذكر واحدة منها
    // في الفارض. سمة على كل خاصية كانت ستمرّ هذه الحمولة صامتة يوم تُنسى السمة —
    // وهو فخّ L34 نفسه بالاسم. سقوط هذا الاختبار يعني أن الفرض صار لائحة أسماء
    [Fact]
    public async Task N05_PostEntry_SourceModuleOutOfRange_IsRejectedAtApiBoundary()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.JournalEntriesPath, accessToken, NewEntry(identity.Company, sourceModule: 99));

        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("sourceModule", message, StringComparison.Ordinal);
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // N07 — الحمولة D: قيمة لا تسع في النوع الأساسي أصلاً (`byte`).
    // هذه لا يبلغها الفارض: `System.Text.Json` يرفضها في إلغاء التسلسل قبل ربط النموذج،
    // فتخرج رسالته الإنجليزية حاملةً **اسم النوع الداخلي بمساره الكامل** — خرق بند 8.4
    [Fact]
    public async Task N07_CreateAccount_AccountTypeBeyondUnderlyingType_LeaksNoInternalTypeName()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "1164", "قيمة لا تسع في byte", accountType: 300));

        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("accountType", message, StringComparison.Ordinal);
        await AccountingClient.AssertNoInternalLeakAsync(response);
    }

    // N08 — الحمولة E: اسم العضو نصاً. مرفوض عمداً — لا `JsonStringEnumConverter` مسجَّل،
    // والعقد يعلن أرقاماً لا أسماء. والمقصود هنا **صيغة الرفض** لا الرفض نفسه
    [Fact]
    public async Task N08_CreateAccount_AccountTypeAsMemberName_LeaksNoInternalTypeName()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            new
            {
                companyId = identity.Company.CompanyId,
                code = "1165",
                name = "نوع بالاسم لا بالرقم",
                accountType = "Asset",
                normalBalance = 0,
                isPostable = true
            });

        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("accountType", message, StringComparison.Ordinal);
        await AccountingClient.AssertNoInternalLeakAsync(response);
    }

    // N06 — النصف السالب: الفارض يمنع ما هو خارج المدى ولا يمنع ما بداخله.
    // حارسٌ يرفض كل شيء ليس حارساً بل عطباً، وقيمته في بقائه أخضر بعد بناء الفارض
    [Fact]
    public async Task N06_CreateAccount_EnumValuesWithinRange_StillSucceeds()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.AccountsPath, accessToken,
            NewAccount(identity.Company.CompanyId, "5911", "مصروف بطبيعة مدينة",
                accountType: 5, normalBalance: 0));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
