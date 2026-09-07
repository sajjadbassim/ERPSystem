using System.Net;
using System.Text.Json;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Accounting;

// المجموعة التي تُغلق G01 و G04 و H03 بمحتوى حقيقي لا برمز حالة.
// تلك الثلاثة خضراء اليوم على عقد التخويل وحده والجسم فارغ — «ديون مقنَّعة بالأخضر»
public class JournalReadApiTests(TestDatabase database) : IdentityTestBase(database)
{
    private async Task<Guid> PostOneAsync(IdentityScenario identity, decimal amount = 610m)
    {
        var request = identity.Company.NewPost(
            identity.Company.Debit(identity.Company.CashAccountId, amount),
            identity.Company.Credit(identity.Company.RevenueAccountId, amount));

        await PostingClient.PostAsync(Database, request);
        return request.JournalEntryId;
    }

    // L29 — القراءة تُرجع القيد المرحَّل فعلاً. G01 يكتفي بـ 200، وهذا يفحص المحتوى
    [Fact]
    public async Task L29_ReadEntries_ReturnsThePostedEntry_NotAnEmptyPage()
    {
        var identity = await NewIdentityAsync();
        var entryId = await PostOneAsync(identity);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client,
            $"{AccountingClient.JournalEntriesPath}?branchId={identity.Company.BranchId}", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await AccountingClient.ReadItemsAsync(response);

        Assert.Contains(items, e => AccountingClient.Field(e, "id") == entryId.ToString());
    }

    // L30 — قراءة قيد واحد تُرجع سطوره. رأس بلا سطور ليس قيداً (R-TRC-03-a)
    [Fact]
    public async Task L30_ReadEntryById_ReturnsItsLines()
    {
        var identity = await NewIdentityAsync();
        var entryId = await PostOneAsync(identity);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(
            Client, AccountingClient.EntryPath(entryId), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await AccountingClient.ReadLinesAsync(response)).Count);
    }

    // L31 — R-API-03: كل مبلغ وسعر يعبر كنصّ لا كعدد.
    // القيود تحمل مبالغ حقيقية بخلاف الدفعة (أ)، فهذا أول اختبار يفحص القاعدة على أرقام دفترية
    [Fact]
    public async Task L31_EveryMoneyFieldOnALine_CrossesAsJsonString()
    {
        var identity = await NewIdentityAsync();
        var entryId = await PostOneAsync(identity);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(
            Client, AccountingClient.EntryPath(entryId), accessToken);
        response.EnsureSuccessStatusCode();

        var lines = await AccountingClient.ReadLinesAsync(response);

        Assert.NotEmpty(lines);

        foreach (var line in lines)
        {
            foreach (var field in AccountingClient.LineMoneyFields)
            {
                Assert.True(line.TryGetProperty(field, out var value), $"الحقل الناقص: {field}");
                Assert.Equal(JsonValueKind.String, value.ValueKind);
            }
        }
    }

    // L32 — R-AMT-02 و R-API-02: لا رقم عارٍ بلا هويته الكاملة.
    // مبلغ بلا عملته وسعره وتاريخ سعره صحيح عددياً وخاطئ في معناه (R-RPT-06)
    [Fact]
    public async Task L32_EveryLine_CarriesTheCompleteMoneyIdentity()
    {
        var identity = await NewIdentityAsync();
        var entryId = await PostOneAsync(identity);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(
            Client, AccountingClient.EntryPath(entryId), accessToken);
        response.EnsureSuccessStatusCode();

        var lines = await AccountingClient.ReadLinesAsync(response);

        Assert.NotEmpty(lines);

        foreach (var line in lines)
        {
            foreach (var field in AccountingClient.LineRequiredFields)
            {
                Assert.True(line.TryGetProperty(field, out _), $"الحقل الناقص في السطر: {field}");
            }
        }
    }

    // L33 — المبلغ يعود بدقته المخزَّنة لا مقرَّباً. القطع في النقل يتضخم
    // عبر التحويلات المتتالية (R-INV-05)، وهنا يُفحص على قيمة دفترية فعلية
    [Fact]
    public async Task L33_LineAmount_PreservesStoredScaleExactly()
    {
        var identity = await NewIdentityAsync();
        var entryId = await PostOneAsync(identity, 610m);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(
            Client, AccountingClient.EntryPath(entryId), accessToken);
        response.EnsureSuccessStatusCode();

        var lines = await AccountingClient.ReadLinesAsync(response);
        var debitLine = lines.Single(l => AccountingClient.Field(l, "debitFC") != "0.0000");

        // العمود (19,4) فالقيمة تعود بأربع خانات لا بصيغة مختصرة
        Assert.Equal("610.0000", AccountingClient.Field(debitLine, "debitFC"));
        Assert.Equal("610.0000", AccountingClient.Field(debitLine, "debitBase"));
    }
}
