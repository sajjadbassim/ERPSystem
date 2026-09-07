using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Accounting;

// المجموعة التي تُغلق مسار نجاح G03. كل تأكيد هنا يقرأ **أثر الترحيل في القاعدة**
// لا رمز الاستجابة وحده: 201 على نقطة نهاية لا تُرحّل شيئاً تبدو نجاحاً وهي فراغ
public class JournalPostingApiTests(TestDatabase database) : IdentityTestBase(database)
{
    private static object Line(
        Guid accountId, Guid currencyId, string debitFc, string creditFc,
        string debitBase, string creditBase) =>
        new
        {
            accountId,
            description = "سطر اختبار",
            currencyId,
            exchangeRate = "1.000000000000",
            exchangeRateDate = "2026-06-15",
            debitFC = debitFc,
            creditFC = creditFc,
            debitBase,
            creditBase
        };

    private static object NewEntry(Scenario company, string amount = "400.0000") =>
        new
        {
            branchId = company.BranchId,
            postingDate = "2026-06-15",
            documentDate = "2026-06-15",
            description = "قيد عبر الـ API",
            sourceModule = (byte)1,
            lines = new[]
            {
                Line(company.CashAccountId, company.BaseCurrencyId, amount, "0.0000", amount, "0.0000"),
                Line(company.RevenueAccountId, company.BaseCurrencyId, "0.0000", amount, "0.0000", amount)
            }
        };

    // L22 — الأثر الفعلي: رأس وسطران في القاعدة، لا مجرد 201
    [Fact]
    public async Task L22_PostEntry_PersistsHeaderAndLinesInDatabase()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.JournalEntriesPath, accessToken, NewEntry(identity.Company));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await AccountingClient.ReadDataAsync(response);
        var entryId = Guid.Parse(AccountingClient.Field(data, "id")!);

        var headers = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalEntries WHERE Id = @id", ("@id", entryId));

        var lines = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalLines WHERE JournalEntryId = @id", ("@id", entryId));

        Assert.Equal(1, headers);
        Assert.Equal(2, lines);
    }

    // L23 — الدليل على المرور بالإجراء لا حوله: رقم المستند يُسحب من العدّاد ذرياً
    // داخل معاملة الترحيل (R-NUM-01). إدراج مباشر بـ EF لا ينتجه، ولا يحرّك العدّاد
    [Fact]
    public async Task L23_PostEntry_DrawsDocumentNumberFromSequence_ProvingItWentThroughTheProcedure()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var before = await PostingClient.ScalarAsync<long>(Database,
            "SELECT CurrentValue FROM NumberSequences WHERE BranchId = @b AND DocumentType = 1",
            ("@b", identity.Company.BranchId));

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.JournalEntriesPath, accessToken, NewEntry(identity.Company));
        response.EnsureSuccessStatusCode();

        var after = await PostingClient.ScalarAsync<long>(Database,
            "SELECT CurrentValue FROM NumberSequences WHERE BranchId = @b AND DocumentType = 1",
            ("@b", identity.Company.BranchId));

        var data = await AccountingClient.ReadDataAsync(response);
        var documentNumber = AccountingClient.Field(data, "documentNumber");

        Assert.Equal(before + 1, after);
        Assert.False(string.IsNullOrWhiteSpace(documentNumber),
            "رقم مستند فارغ يعني أن الصف لم يمرّ بالإجراء");
    }

    // L24 — قيد غير متوازن يُرفض، ولا يترك رأساً يتيماً خلفه (الخطأ 50001)
    [Fact]
    public async Task L24_PostEntry_Unbalanced_IsRejectedAndLeavesNothingBehind()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var before = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalEntries WHERE BranchId = @b", ("@b", identity.Company.BranchId));

        var response = await AuthClient.PostAsync(Client, AccountingClient.JournalEntriesPath, accessToken,
            new
            {
                branchId = identity.Company.BranchId,
                postingDate = "2026-06-15",
                documentDate = "2026-06-15",
                description = "قيد غير متوازن",
                sourceModule = (byte)1,
                lines = new[]
                {
                    Line(identity.Company.CashAccountId, identity.Company.BaseCurrencyId,
                        "500.0000", "0.0000", "500.0000", "0.0000"),
                    Line(identity.Company.RevenueAccountId, identity.Company.BaseCurrencyId,
                        "0.0000", "400.0000", "0.0000", "400.0000")
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // الخطأ 50001 من الإجراء — يصل مترجَماً برسالته العربية، بلا رقمه ولا اسم الإجراء
        await AccountingClient.AssertNoRawSqlLeakAsync(response);

        var after = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalEntries WHERE BranchId = @b", ("@b", identity.Company.BranchId));

        Assert.Equal(before, after);
    }

    // L25 — الترحيل في فترة مقفلة مرفوض (R-GL-04، الخطأ 50004)
    [Fact]
    public async Task L25_PostEntry_IntoClosedPeriod_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.JournalEntriesPath, accessToken,
            new
            {
                branchId = identity.Company.BranchId,
                postingDate = "2026-01-15",
                documentDate = "2026-01-15",
                description = "قيد في فترة مقفلة",
                sourceModule = (byte)1,
                lines = new[]
                {
                    Line(identity.Company.CashAccountId, identity.Company.BaseCurrencyId,
                        "100.0000", "0.0000", "100.0000", "0.0000"),
                    Line(identity.Company.RevenueAccountId, identity.Company.BaseCurrencyId,
                        "0.0000", "100.0000", "0.0000", "100.0000")
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // الخطأ 50004 من الإجراء
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L26 — العكس يُنشئ قيداً مرتبطاً بالأصل، ويُبنى من الأصل نفسه (R-GL-03-a).
    // الفحص على الرابط في القاعدة لا على رمز الاستجابة
    [Fact]
    public async Task L26_ReverseEntry_CreatesLinkedReversalWithMirroredLines()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var posted = await AuthClient.PostAsync(Client,
            AccountingClient.JournalEntriesPath, accessToken, NewEntry(identity.Company));
        posted.EnsureSuccessStatusCode();

        var originalId = Guid.Parse(
            AccountingClient.Field(await AccountingClient.ReadDataAsync(posted), "id")!);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.ReversePath(originalId), accessToken,
            // الوصف خمسة أحرف فأكثر: القيد اليدوي يتطلبه (الخطأ 50025)
            new { postingDate = "2026-06-20", documentDate = "2026-06-20", description = "عكس قيد اختباري" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var reversalId = Guid.Parse(
            AccountingClient.Field(await AccountingClient.ReadDataAsync(response), "id")!);

        var linked = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalEntries WHERE Id = @rev AND ReversalOfJournalEntryId = @orig",
            ("@rev", reversalId), ("@orig", originalId));

        var reversalLines = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalLines WHERE JournalEntryId = @rev", ("@rev", reversalId));

        Assert.Equal(1, linked);
        Assert.Equal(2, reversalLines);
    }

    // L27 — العكس صلاحية منفصلة عن الترحيل: من يُرحّل ليس بالضرورة من يُلغي
    [Fact]
    public async Task L27_ReverseEntry_WithoutReversePermission_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (adminToken, _) = await LoginAsync(identity.AdminUserName);

        var posted = await AuthClient.PostAsync(Client,
            AccountingClient.JournalEntriesPath, adminToken, NewEntry(identity.Company));
        posted.EnsureSuccessStatusCode();

        var originalId = Guid.Parse(
            AccountingClient.Field(await AccountingClient.ReadDataAsync(posted), "id")!);

        var (branchToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.ReversePath(originalId), branchToken,
            new { postingDate = "2026-06-21", documentDate = "2026-06-21", description = "عكس ممنوع" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // L34 — «lines»: null صراحةً في الجسم. المُهيِّئ `= []` في الـ DTO يغطي **غياب**
    // الحقل وحده: System.Text.Json يستدعي الواضع بـ null حين يحضر الحقل بقيمة null،
    // فيمحو المُهيِّئ. بلا حارس يقع NullReferenceException **خارج مسار الفحص المصمَّم**
    // وينتهي 500 بدل رفض نظيف — وهي عين فئة المشكلة التي أُغلقت في G03
    [Fact]
    public async Task L34_PostEntry_WithExplicitNullLines_IsRejectedCleanlyNotWithServerError()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.JournalEntriesPath, accessToken,
            new
            {
                branchId = identity.Company.BranchId,
                postingDate = "2026-06-15",
                documentDate = "2026-06-15",
                description = "قيد بلا سطور إطلاقاً",
                sourceModule = (byte)1,
                lines = (object?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AccountingClient.AssertNoRawSqlLeakAsync(response);

        // بند 8.4: رسائل المستخدم بالعربية. الرفض الضمني الذي يولّده MVC لمرجع غير
        // قابل للعدم رسالته إنجليزية افتراضاً، فيلزم ErrorMessage صريح ليُستبدل بها
        var message = await AuthClient.ReadFieldAsync(response, "message");

        Assert.DoesNotContain("field is required", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("سطور", message);
    }

    // L28 — الفشل مغلق على الترحيل نفسه
    [Fact]
    public async Task L28_PostEntry_WithoutPostPermission_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.NoRoleUserName);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.JournalEntriesPath, accessToken, NewEntry(identity.Company));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
