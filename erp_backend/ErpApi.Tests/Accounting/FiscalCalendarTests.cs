using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Accounting;

public class FiscalCalendarTests(TestDatabase database) : IdentityTestBase(database)
{
    // L09
    [Fact]
    public async Task L09_CreateFiscalYear_ValidRange_ReturnsCreated()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.FiscalYearsPath, accessToken,
            new
            {
                companyId = identity.Company.CompanyId,
                code = "FY2028",
                startDate = "2028-01-01",
                endDate = "2028-12-31"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // L10 — سنتان متداخلتان تجعلان «في أي سنة يقع هذا التاريخ؟» بلا جواب واحد.
    // يحرسه TR_FiscalYear_PreventOverlap (51008)
    [Fact]
    public async Task L10_CreateFiscalYear_OverlappingExistingYear_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.FiscalYearsPath, accessToken,
            new
            {
                companyId = identity.Company.CompanyId,
                code = "FY2026B",
                startDate = "2026-06-01",
                endDate = "2027-05-31"
            });

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"المتوقَّع رفض التداخل، والوارد: {response.StatusCode}");
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L11 — فترة تخرج عن مدى سنتها. يحرسه TR_FiscalPeriod_WithinYearRange (51010)
    [Fact]
    public async Task L11_CreateFiscalPeriod_OutsideItsYearRange_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.FiscalPeriodsPath, accessToken,
            new
            {
                fiscalYearId = identity.Company.FiscalYearId,
                periodNumber = (byte)14,
                periodType = (byte)1,
                name = "فترة خارج السنة",
                startDate = "2027-03-01",
                endDate = "2027-03-31"
            });

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"المتوقَّع رفض الخروج عن المدى، والوارد: {response.StatusCode}");
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L12 — فترتان عاديتان متداخلتان. يحرسه TR_FiscalPeriod_PreventOverlap (51011).
    // فترة التسويات معفاة عمداً، ولذلك الاختبار على النوع العادي تحديداً
    [Fact]
    public async Task L12_CreateFiscalPeriod_OverlappingRegularPeriod_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.FiscalPeriodsPath, accessToken,
            new
            {
                fiscalYearId = identity.Company.FiscalYearId,
                periodNumber = (byte)14,
                periodType = (byte)1,
                name = "فترة متداخلة",
                startDate = "2026-06-10",
                endDate = "2026-06-20"
            });

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"المتوقَّع رفض التداخل، والوارد: {response.StatusCode}");
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L13 — سنة تُقفل وفيها فترة مفتوحة تترك باباً مفتوحاً في غرفة مغلقة.
    // يحرسه TR_FiscalYear_PreventCloseWithOpenPeriods (51009)
    [Fact]
    public async Task L13_CloseFiscalYear_WithOpenPeriods_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.CloseYearPath(identity.Company.FiscalYearId), accessToken, new { });

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"المتوقَّع رفض الإقفال، والوارد: {response.StatusCode}");
        await AccountingClient.AssertNoRawSqlLeakAsync(response);
    }

    // L14 — الإقفال يسري فعلاً: الفترة المقفلة تمنع الترحيل فيها (R-GL-04).
    // الفحص على أثر الإقفال في القاعدة لا على رمز الاستجابة وحده
    [Fact]
    public async Task L14_ClosePeriod_MarksItClosedInDatabase()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);
        var periodId = identity.Company.RegularPeriodIds[6];

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.ClosePeriodPath(periodId), accessToken, new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isClosed = await PostingClient.ScalarAsync<bool>(Database,
            "SELECT IsClosed FROM FiscalPeriods WHERE Id = @id", ("@id", periodId));

        Assert.True(isClosed);
    }

    // L15 — الإقفال تصرّف محاسبي لا إدارة بيانات، وصلاحيته منفصلة
    [Fact]
    public async Task L15_ClosePeriod_WithoutClosePermission_IsForbidden()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.ClosePeriodPath(identity.Company.RegularPeriodIds[7]), accessToken, new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
