using System.Net;
using System.Text.Json;
using ErpApi.Core.Constants;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Accounting;

public class FiscalCalendarTests(TestDatabase database) : IdentityTestBase(database)
{
    private static string YearPath(Guid id) => $"{AccountingClient.FiscalYearsPath}/{id}";

    private static string PeriodPath(Guid id) => $"{AccountingClient.FiscalPeriodsPath}/{id}";

    private static async Task<string?> MessageAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("message").GetString();
    }

    // ‏‏══ G17–G22: حدّ الشركة على السنوات والفترات المالية ══════════════════════════

    // G17
    [Fact]
    public async Task G17_ListFiscalYears_ReturnsOnlyTheActorsCompany()
    {
        var identity = await NewIdentityAsync();
        var other = (await NewIdentityAsync()).Company;
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var ids = (await AccountingClient.ReadItemsAsync(await AuthClient.GetAsync(Client,
                $"{AccountingClient.FiscalYearsPath}?PageNumber=1&PageSize=100", accessToken)))
            .Select(year => year.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(identity.Company.FiscalYearId, ids);
        Assert.DoesNotContain(other.FiscalYearId, ids);
    }

    // G18 — نظير K27
    [Fact]
    public async Task G18_ListFiscalYears_TotalCountMatchesTheScopedList()
    {
        var identity = await NewIdentityAsync();
        _ = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.GetAsync(Client, $"{AccountingClient.FiscalYearsPath}?PageNumber=1&PageSize=100", accessToken);
        var totalCount = (await AccountingClient.ReadDataAsync(response)).GetProperty("totalCount").GetInt32();

        var expected = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM FiscalYears WHERE CompanyId = @company AND IsDeleted = 0",
            ("@company", identity.Company.CompanyId));

        Assert.Equal(expected, totalCount);
        Assert.Equal(totalCount, (await AccountingClient.ReadItemsAsync(response)).Count);
    }

    // G19 — السنة: شركتها 200، وغير الموجودة 404، وسنة شركة أخرى 403
    [Fact]
    public async Task G19_GetFiscalYear_OwnSucceeds_MissingIs404_AnotherCompanysIs403()
    {
        var identity = await NewIdentityAsync();
        var other = (await NewIdentityAsync()).Company;
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        Assert.Equal(HttpStatusCode.OK,
            (await AuthClient.GetAsync(Client, YearPath(identity.Company.FiscalYearId), accessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await AuthClient.GetAsync(Client, YearPath(Guid.CreateVersion7()), accessToken)).StatusCode);

        var foreign = await AuthClient.GetAsync(Client, YearPath(other.FiscalYearId), accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(AuthMessages.CompanyOutOfScope, await MessageAsync(foreign));
    }

    // G20 — الفترة عبر سنتها: لا `CompanyId` على الفترة، فالحدّ يمرّ بعلاقتين
    [Fact]
    public async Task G20_GetFiscalPeriod_ThroughItsYear_OwnSucceeds_MissingIs404_AnotherCompanysIs403()
    {
        var identity = await NewIdentityAsync();
        var other = (await NewIdentityAsync()).Company;
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        Assert.Equal(HttpStatusCode.OK,
            (await AuthClient.GetAsync(Client, PeriodPath(identity.Company.OpenPeriodId), accessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await AuthClient.GetAsync(Client, PeriodPath(Guid.CreateVersion7()), accessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await AuthClient.GetAsync(Client, PeriodPath(other.OpenPeriodId), accessToken)).StatusCode);
    }

    // G21 — كتابة عبر الشركات
    [Fact]
    public async Task G21_CreateFiscalYear_InAnotherCompany_IsForbiddenAndWritesNothing()
    {
        var identity = await NewIdentityAsync();
        var other = (await NewIdentityAsync()).Company;
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client, AccountingClient.FiscalYearsPath, accessToken,
            new { companyId = other.CompanyId, code = "FY2029", startDate = "2029-01-01", endDate = "2029-12-31" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM FiscalYears WHERE CompanyId = @company AND Code = 'FY2029'", ("@company", other.CompanyId)));
    }

    // G22 — إقفال فترة شركة أخرى: كان يقفلها
    [Fact]
    public async Task G22_ClosePeriod_OfAnotherCompany_IsForbiddenAndLeavesItOpen()
    {
        var identity = await NewIdentityAsync();
        var other = (await NewIdentityAsync()).Company;
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client,
            AccountingClient.ClosePeriodPath(other.RegularPeriodIds[8]), accessToken, new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await PostingClient.ScalarAsync<bool>(Database,
            "SELECT IsClosed FROM FiscalPeriods WHERE Id = @id", ("@id", other.RegularPeriodIds[8])));
    }

    // ‏‏══ G29–G30: الدين 10 — مسارا الكتابة الباقيان على التقويم ══════════════════════
    // ‏الهدف سنة **فارغة** يُنشئها مدير شركتها: سنة البذر فيها فترات مفتوحة وتقويم مكتمل،
    // ‏فيُرفض الإقفال (51009) والإنشاء (51011) عليها لسبب غير الشركة، فلا يثبت الأحمر شيئاً

    private async Task<Guid> CreateEmptyYearAsync(string accessToken, Guid companyId, string code, int year)
    {
        var response = await AuthClient.PostAsync(Client, AccountingClient.FiscalYearsPath, accessToken,
            new { companyId, code, startDate = $"{year}-01-01", endDate = $"{year}-12-31" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AccountingClient.ReadDataAsync(response)).GetProperty("id").GetGuid();
    }

    private static object PeriodRequest(Guid fiscalYearId, int year) => new
    {
        fiscalYearId,
        periodNumber = (byte)1,
        periodType = (byte)1,
        name = "يناير",
        startDate = $"{year}-01-01",
        endDate = $"{year}-01-31"
    };

    // G29 — إقفال سنة شركة أخرى: كان يقفلها، ولا مسار لإعادة فتحها
    [Fact]
    public async Task G29_CloseYear_OwnSucceeds_AnotherCompanysIsForbiddenAndLeavesItOpen()
    {
        var identity = await NewIdentityAsync();
        var other = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);
        var (otherToken, _) = await LoginAsync(other.AdminUserName);

        var ownYearId = await CreateEmptyYearAsync(accessToken, identity.Company.CompanyId, "FY2030", 2030);
        var foreignYearId = await CreateEmptyYearAsync(otherToken, other.Company.CompanyId, "FY2030", 2030);

        Assert.Equal(HttpStatusCode.OK,
            (await AuthClient.PostAsync(Client, AccountingClient.CloseYearPath(ownYearId), accessToken, new { })).StatusCode);

        var foreign = await AuthClient.PostAsync(Client, AccountingClient.CloseYearPath(foreignYearId), accessToken, new { });

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(AuthMessages.CompanyOutOfScope, await MessageAsync(foreign));
        Assert.False(await PostingClient.ScalarAsync<bool>(Database,
            "SELECT IsClosed FROM FiscalYears WHERE Id = @id", ("@id", foreignYearId)));
    }

    // G30 — فترة في سنة شركة أخرى: كانت تُنشأ وتحجز رقمها ومداها، ولا مسار لحذفها
    [Fact]
    public async Task G30_CreatePeriod_InOwnYearSucceeds_InAnotherCompanysYearIsForbiddenAndWritesNothing()
    {
        var identity = await NewIdentityAsync();
        var other = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);
        var (otherToken, _) = await LoginAsync(other.AdminUserName);

        var ownYearId = await CreateEmptyYearAsync(accessToken, identity.Company.CompanyId, "FY2031", 2031);
        var foreignYearId = await CreateEmptyYearAsync(otherToken, other.Company.CompanyId, "FY2031", 2031);

        Assert.Equal(HttpStatusCode.Created,
            (await AuthClient.PostAsync(Client, AccountingClient.FiscalPeriodsPath, accessToken,
                PeriodRequest(ownYearId, 2031))).StatusCode);

        var foreign = await AuthClient.PostAsync(Client, AccountingClient.FiscalPeriodsPath, accessToken,
            PeriodRequest(foreignYearId, 2031));

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(AuthMessages.CompanyOutOfScope, await MessageAsync(foreign));
        Assert.Equal(0, await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM FiscalPeriods WHERE FiscalYearId = @year", ("@year", foreignYearId)));
    }

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
