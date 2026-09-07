using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.MasterData;

public class CurrencyTests(TestDatabase database) : IdentityTestBase(database)
{
    private static object NewCurrency(string code, string name, byte decimalPlaces) =>
        new { code, name, symbol = (string?)null, decimalPlaces };

    // K04
    [Fact]
    public async Task K04_CreateCurrency_ValidPayload_ReturnsCreated()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.CurrenciesPath, accessToken,
            NewCurrency("GBP", "جنيه إسترليني", 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // K05 — التكرار تعارض حالة لا خطأ مدخلات، فهو 409 لا 400 (بند 8.1)
    [Fact]
    public async Task K05_CreateCurrency_DuplicateCode_IsConflict()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.CurrenciesPath, accessToken,
            NewCurrency("IQD", "دينار مكرر", 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // K06 — R-AMT-06: الخانات العشرية خاصية للعملة، ومداها محكوم بـ CK_Currency_DecimalPlaces.
    // خطأ في شكل المدخل فهو 400 (بند 9: DataAnnotations على الـ DTO لا فحص في الخدمة)
    [Fact]
    public async Task K06_CreateCurrency_DecimalPlacesOutOfRange_IsBadRequest()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, MasterDataClient.CurrenciesPath, accessToken,
            NewCurrency("XXX", "عملة بدقة مستحيلة", 9));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // K07 — الاسم محدَّد سلفاً في ERP-CORE-RULES تحت R-LIFE-06.
    // تعطيل عملة لها حركة يجعل EF يولّد INNER JOIN بمرشِّح المبدأ عند التنقّل،
    // فتختفي سطورها من كل استعلام دفتري — ميزان ينقصه حساب دون أن يُعلن ذلك
    [Fact]
    public async Task K07_DeactivateAsync_WhenCurrencyHasJournalLines_ThrowsConflictException()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        // حركة فعلية بعملة الأساس، فتصير IQD عملة ذات سطور مرحَّلة
        var request = identity.Company.NewPost(
            identity.Company.Debit(identity.Company.CashAccountId, 500m),
            identity.Company.Credit(identity.Company.RevenueAccountId, 500m));
        await PostingClient.PostAsync(Database, request);

        var response = await AuthClient.PostAsync(Client,
            MasterDataClient.DeactivateCurrencyPath(identity.Company.BaseCurrencyId), accessToken, new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // K08 — الوجه الآخر: القاعدة تمنع تعطيل ما له حركة لا التعطيل مطلقاً
    [Fact]
    public async Task K08_DeactivateCurrency_WithoutJournalLines_Succeeds()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var created = await AuthClient.PostAsync(Client, MasterDataClient.CurrenciesPath, accessToken,
            NewCurrency("CHF", "فرنك سويسري", 2));
        created.EnsureSuccessStatusCode();

        var id = Guid.Parse(await MasterDataClient.RawStringAsync(created, "id") ?? Guid.Empty.ToString());

        var response = await AuthClient.PostAsync(Client,
            MasterDataClient.DeactivateCurrencyPath(id), accessToken, new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // K09 — الفشل مغلق: من بلا صلاحية القراءة لا يرى البيانات الأساسية
    [Fact]
    public async Task K09_ReadCurrencies_WithoutMasterDataRead_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.GetAsync(Client, MasterDataClient.CurrenciesPath, accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
