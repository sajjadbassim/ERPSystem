using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Accounting;

// ‏§4.3 ينصّ صراحةً: «تهيئة صفوف NumberSequence عند إنشاء السنة المالية والفرع،
// لا عند أول ترحيل». الفرق ليس تجميلياً: التهيئة الكسولة تجعل أول ترحيل في فرع
// جديد يُنشئ العدّاد داخل معاملته، فيتسابق ترحيلان متزامنان على إنشائه — والخطأ
// حينها فجوة في تسلسل مستندات محاسبية، وهو ما يمنعه R-NUM-01 من أصله.
//
// لذلك تفحص هذه المجموعة **التوقيت** لا وجود الصف: قبل الترحيل وبعده.
public class NumberSequenceInitializationTests(TestDatabase database) : IdentityTestBase(database)
{
    private Task<int> SequenceCountForBranchAsync(Guid branchId) =>
        PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM NumberSequences WHERE BranchId = @branch", ("@branch", branchId))!;

    // L16 — الحالة التي نصّ عليها §4.3 حرفياً: فرع جديد في سنة مالية قائمة
    // يُنتج صفوف العدّاد **فوراً**، بلا أي عملية ترحيل بعده
    [Fact]
    public async Task L16_CreatingBranch_InitializesNumberSequencesImmediately_WithoutAnyPosting()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var created = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = identity.Company.CompanyId, code = "BRN", name = "فرع بعدّاد فوري" });
        created.EnsureSuccessStatusCode();

        var branchId = Guid.Parse(await MasterDataClient.RawStringAsync(created, "id") ?? string.Empty);

        // لم يقع أي ترحيل بين إنشاء الفرع وهذا الفحص
        var postedEntries = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM JournalEntries WHERE BranchId = @branch", ("@branch", branchId));

        Assert.Equal(0, postedEntries);
        Assert.True(await SequenceCountForBranchAsync(branchId) > 0,
            "الفرع الجديد بلا صف NumberSequence — التهيئة كسولة لا فورية");
    }

    // L17 — العدّاد المهيَّأ يبدأ من الصفر وبإعدادات صالحة، لا صفاً فارغاً بلا معنى
    [Fact]
    public async Task L17_InitializedSequence_StartsAtZeroWithUsablePrefixAndPadding()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var created = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = identity.Company.CompanyId, code = "BRP", name = "فرع لفحص الإعدادات" });
        created.EnsureSuccessStatusCode();

        var branchId = Guid.Parse(await MasterDataClient.RawStringAsync(created, "id") ?? string.Empty);

        var currentValue = await PostingClient.ScalarAsync<long>(Database,
            "SELECT CurrentValue FROM NumberSequences WHERE BranchId = @branch AND DocumentType = 1",
            ("@branch", branchId));

        var padding = await PostingClient.ScalarAsync<byte>(Database,
            "SELECT PaddingLength FROM NumberSequences WHERE BranchId = @branch AND DocumentType = 1",
            ("@branch", branchId));

        Assert.Equal(0L, currentValue);
        Assert.True(padding >= 1, "طول الحشو صفر يجعل الأرقام بلا تنسيق ثابت");
    }

    // L18 — النصف السالب من دعوى التوقيت: الترحيل **يسحب** من عدّاد قائم
    // ولا يُنشئ صفاً جديداً. لو أنشأه، لكانت التهيئة كسولة رغم وجودها
    [Fact]
    public async Task L18_Posting_DoesNotCreateNewSequenceRow_ItOnlyDrawsFromExisting()
    {
        var identity = await NewIdentityAsync();
        var before = await SequenceCountForBranchAsync(identity.Company.BranchId);

        var request = identity.Company.NewPost(
            identity.Company.Debit(identity.Company.CashAccountId, 320m),
            identity.Company.Credit(identity.Company.RevenueAccountId, 320m));
        await PostingClient.PostAsync(Database, request);

        var after = await SequenceCountForBranchAsync(identity.Company.BranchId);

        Assert.Equal(before, after);
    }

    // L19 — ترقيم القيد على مستوى الفرع (BranchId إلزامي لنوع 1)، يحرسه
    // CK_NumberSequence_JournalScope. فرع بلا عدّاد يعني ترحيلاً يفشل بالخطأ 50021
    [Fact]
    public async Task L19_EveryBranchOfCompany_HasJournalSequenceAfterCreation()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var created = await AuthClient.PostAsync(Client, MasterDataClient.BranchesPath, accessToken,
            new { companyId = identity.Company.CompanyId, code = "BRQ", name = "فرع ثالث" });
        created.EnsureSuccessStatusCode();

        var branchId = Guid.Parse(await MasterDataClient.RawStringAsync(created, "id") ?? string.Empty);

        var journalSequences = await PostingClient.ScalarAsync<int>(Database,
            """
            SELECT COUNT(*) FROM NumberSequences
            WHERE BranchId = @branch AND DocumentType = 1
            """,
            ("@branch", branchId));

        Assert.Equal(1, journalSequences);
    }
}
