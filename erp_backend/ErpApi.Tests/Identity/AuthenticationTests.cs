using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Identity;

public class AuthenticationTests(TestDatabase database) : IdentityTestBase(database)
{
    // E01
    [Fact]
    public async Task E01_LoginAsync_ValidCredentials_ReturnsBothTokens()
    {
        var identity = await NewIdentityAsync();

        var response = await AuthClient.LoginAsync(Client, identity.AdminUserName, IdentityScenario.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await AuthClient.ReadFieldAsync(response, "accessToken"));
        Assert.NotEmpty(await AuthClient.ReadFieldAsync(response, "refreshToken"));
    }

    // E02
    [Fact]
    public async Task E02_LoginAsync_WrongPassword_IsRejected()
    {
        var identity = await NewIdentityAsync();

        var response = await AuthClient.LoginAsync(Client, identity.AdminUserName, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // E03 — بند 13: الرسالة موحّدة ولا تكشف أي الحقلين خاطئ
    [Fact]
    public async Task E03_LoginAsync_UnknownUser_ReturnsIdenticalMessageToWrongPassword()
    {
        var identity = await NewIdentityAsync();

        var wrongPassword = await AuthClient.LoginAsync(Client, identity.AdminUserName, "wrong-password");
        var unknownUser = await AuthClient.LoginAsync(Client, "no-such-user-at-all", IdentityScenario.Password);

        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);
        Assert.Equal(await AuthClient.ReadFieldAsync(wrongPassword, "message"),
                     await AuthClient.ReadFieldAsync(unknownUser, "message"));
    }

    // E04
    [Fact]
    public async Task E04_LoginAsync_InactiveUser_IsRejected()
    {
        var identity = await NewIdentityAsync();

        var response = await AuthClient.LoginAsync(Client, identity.InactiveUserName, IdentityScenario.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // E05 — R-LIFE-06: المستخدم بلا IsDeleted. التعطيل وحده يمنع الدخول
    [Fact]
    public async Task E05_LoginAsync_UserDeactivatedAfterCreation_IsRejected()
    {
        var identity = await NewIdentityAsync();
        await PostingClient.ExecuteAsync(Database,
            "UPDATE Users SET IsActive = 0 WHERE Id = @id", ("@id", identity.BranchUserId));

        var response = await AuthClient.LoginAsync(Client, identity.BranchUserName, IdentityScenario.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // E06
    [Fact]
    public async Task E06_StoredPassword_IsHashedNotPlainText()
    {
        var identity = await NewIdentityAsync();

        var stored = await PasswordHashAsync(identity.AdminUserId);

        Assert.NotNull(stored);
        Assert.NotEqual(IdentityScenario.Password, stored);
        Assert.DoesNotContain(IdentityScenario.Password, stored);
    }

    // E07 — التفرّد على مستوى الشركة لا النظام
    [Fact]
    public async Task E07_InsertUser_SameUserNameInAnotherCompany_IsAccepted()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);

        await IdentityScenarioBuilder.InsertUserAsync(Database, Guid.CreateVersion7(),
            otherCompany.CompanyId, identity.AdminUserName, "hash", true, otherCompany.UserId);

        var count = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM Users WHERE UserName = @name", ("@name", identity.AdminUserName));

        Assert.Equal(2, count);
    }

    // E08
    [Fact]
    public async Task E08_InsertUser_DuplicateUserNameInSameCompany_IsRejected()
    {
        var identity = await NewIdentityAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => IdentityScenarioBuilder.InsertUserAsync(
            Database, Guid.CreateVersion7(), identity.Company.CompanyId,
            identity.AdminUserName, "hash", true, identity.Company.UserId));
    }

    // E09 — بند 9: لا حقول حساسة في أي استجابة
    [Fact]
    public async Task E09_LoginResponse_ContainsNoSensitiveFields()
    {
        var identity = await NewIdentityAsync();

        var response = await AuthClient.LoginAsync(Client, identity.AdminUserName, IdentityScenario.Password);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);
    }
}
