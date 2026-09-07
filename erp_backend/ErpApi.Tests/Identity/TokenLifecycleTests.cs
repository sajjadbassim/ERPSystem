using System.Net;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Identity;

public class TokenLifecycleTests(TestDatabase database) : IdentityTestBase(database)
{
    // F01 — التدوير: القديم يُبطل والجديد يرتبط به
    [Fact]
    public async Task F01_RefreshAsync_ValidToken_RotatesAndLinksChain()
    {
        var identity = await NewIdentityAsync();
        var (_, refreshToken) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.RefreshAsync(Client, refreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(refreshToken, await AuthClient.ReadFieldAsync(response, "refreshToken"));
        Assert.Equal(1, await RevokedTokenCountAsync(identity.AdminUserId));
        Assert.Equal(1, await ActiveTokenCountAsync(identity.AdminUserId));

        var linked = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM RefreshTokens WHERE UserId = @user AND ReplacedByTokenId IS NOT NULL",
            ("@user", identity.AdminUserId));

        Assert.Equal(1, linked);
    }

    // F02 — إعادة استخدام رمز مُبطَل مؤشر سرقة: تُبطل السلسلة كاملة لا الرمز وحده
    [Fact]
    public async Task F02_RefreshAsync_ReusingRevokedToken_RevokesEntireChain()
    {
        var identity = await NewIdentityAsync();
        var (_, firstToken) = await LoginAsync(identity.AdminUserName);

        var rotated = await AuthClient.RefreshAsync(Client, firstToken);
        rotated.EnsureSuccessStatusCode();

        var reuse = await AuthClient.RefreshAsync(Client, firstToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(0, await ActiveTokenCountAsync(identity.AdminUserId));
    }

    // F03 — تغيير كلمة المرور يُسقط كل الجلسات فوراً
    [Fact]
    public async Task F03_ChangePasswordAsync_InvalidatesAllExistingSessions()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, refreshToken) = await LoginAsync(identity.AdminUserName);
        var stampBefore = await SecurityStampAsync(identity.AdminUserId);

        var changed = await AuthClient.ChangePasswordAsync(
            Client, accessToken, IdentityScenario.Password, "N3w!P@ssword");
        changed.EnsureSuccessStatusCode();

        Assert.NotEqual(stampBefore, await SecurityStampAsync(identity.AdminUserId));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthClient.RefreshAsync(Client, refreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthClient.GetAsync(Client, AuthClient.PermissionsPath, accessToken)).StatusCode);
    }

    // F04 — ClockSkew = صفر: المنتهي مرفوض بلا سماح
    [Fact]
    public async Task F04_Request_WithExpiredAccessToken_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        await PostingClient.ExecuteAsync(Database,
            "UPDATE Users SET SecurityStamp = NEWID() WHERE Id = @id", ("@id", identity.AdminUserId));

        var response = await AuthClient.GetAsync(Client, AuthClient.PermissionsPath, accessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // F05
    [Fact]
    public async Task F05_RefreshAsync_ExpiredRefreshToken_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (_, refreshToken) = await LoginAsync(identity.AdminUserName);

        // العمودان يتحركان معاً: إرجاع ExpiresAt وحده إلى الأمس يجعل الرمز ينتهي قبل أن
        // يُنشأ، وهو ما يمنعه CK_RefreshToken_ExpiresAfterCreation الذي يحرسه I07.
        // إرجاع الإنشاء شهراً والانتهاء يوماً يبقي الرمز منتهياً والقيد سليماً
        await PostingClient.ExecuteAsync(Database,
            """
            UPDATE RefreshTokens
            SET CreatedAt = DATEADD(day, -30, SYSUTCDATETIME()),
                ExpiresAt = DATEADD(day,  -1, SYSUTCDATETIME())
            WHERE UserId = @user
            """,
            ("@user", identity.AdminUserId));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthClient.RefreshAsync(Client, refreshToken)).StatusCode);
    }

    // F06
    [Fact]
    public async Task F06_RefreshAsync_ExplicitlyRevokedToken_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (_, refreshToken) = await LoginAsync(identity.AdminUserName);

        await PostingClient.ExecuteAsync(Database,
            "UPDATE RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE UserId = @user",
            ("@user", identity.AdminUserId));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthClient.RefreshAsync(Client, refreshToken)).StatusCode);
    }

    // F07 — بند 18: المخزَّن تجزئة، فنسخة القاعدة المسروقة بلا قيمة
    [Fact]
    public async Task F07_RefreshToken_IsStoredHashedNotRaw()
    {
        var identity = await NewIdentityAsync();
        var (_, refreshToken) = await LoginAsync(identity.AdminUserName);

        var matches = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM RefreshTokens WHERE CAST(TokenHash AS NVARCHAR(MAX)) = @raw",
            ("@raw", refreshToken));

        Assert.Equal(0, matches);
    }

    // F08
    [Fact]
    public async Task F08_RefreshAsync_TokenBelongingToAnotherUser_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var (_, adminToken) = await LoginAsync(identity.AdminUserName);
        await LoginAsync(identity.BranchUserName);

        await PostingClient.ExecuteAsync(Database,
            "UPDATE Users SET IsActive = 0 WHERE Id = @id", ("@id", identity.AdminUserId));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthClient.RefreshAsync(Client, adminToken)).StatusCode);
    }

    // F09 — إضافة القرار: مقارنة SecurityStamp بما في القاعدة عند كل طلب
    [Fact]
    public async Task F09_Request_WhenSecurityStampChanged_IsRejectedImmediately()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        Assert.Equal(HttpStatusCode.OK,
            (await AuthClient.GetAsync(Client, AuthClient.PermissionsPath, accessToken)).StatusCode);

        await PostingClient.ExecuteAsync(Database,
            "UPDATE Users SET SecurityStamp = NEWID() WHERE Id = @id", ("@id", identity.AdminUserId));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthClient.GetAsync(Client, AuthClient.PermissionsPath, accessToken)).StatusCode);
    }

    // F10 — تعطيل المستخدم يسري على الجلسة القائمة، لا ينتظر انتهاء الرمز
    [Fact]
    public async Task F10_Request_AfterUserDeactivated_IsRejectedDespiteValidToken()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.BranchUserName);

        await PostingClient.ExecuteAsync(Database,
            "UPDATE Users SET IsActive = 0 WHERE Id = @id", ("@id", identity.BranchUserId));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthClient.GetAsync(Client, AuthClient.PermissionsPath, accessToken)).StatusCode);
    }
}
