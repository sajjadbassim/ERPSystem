using System.Text.Json;
using ErpApi.Data;
using ErpApi.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ErpApi.Tests.Audit;

// بند 18 وتحذير ROADMAP §4.2: لا أسرار في السجل.
// الحارس البنيوي قائمة سماح — العمود غير المذكور فيها لا يُكتب، فالسهو لا يُسرّب
public class AuditSensitiveDataTests(TestDatabase database) : IdentityTestBase(database)
{
    // J14 — تغيير كلمة المرور يُسجَّل كحدث ولا تُسجَّل التجزئة.
    // وقوع الحدث أثر تدقيقي مطلوب؛ والسرّ ليس كذلك
    [Fact]
    public async Task J14_ChangingPassword_IsRecordedWithoutTheHash()
    {
        var identity = await NewIdentityAsync();
        var hashBefore = await PasswordHashAsync(identity.AdminUserId);
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var changed = await AuthClient.ChangePasswordAsync(
            Client, accessToken, IdentityScenario.Password, "N3w!P@ssword");
        changed.EnsureSuccessStatusCode();

        var hashAfter = await PasswordHashAsync(identity.AdminUserId);
        var rows = await AuditClient.ReadAsync(Database, "User", identity.AdminUserId.ToString());

        Assert.Contains(rows, r => r.Action == AuditClient.UserUpdated);

        var stored = await AuditClient.AllStoredTextAsync(Database);

        Assert.NotNull(stored);
        Assert.DoesNotContain(hashBefore!, stored);
        Assert.DoesNotContain(hashAfter!, stored);
        Assert.DoesNotContain("N3w!P@ssword", stored);
    }

    // J15 — لا اسم عمود حسّاس في السجل كله، لا في صفوف هذا الاختبار وحدها
    [Fact]
    public async Task J15_NoAuditRowEverMentionsAForbiddenColumn()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        await AuthClient.ChangePasswordAsync(Client, accessToken, IdentityScenario.Password, "N3w!P@ssword");

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstAsync(u => u.Id == identity.BranchUserId);
            user.IsActive = false;
            await context.SaveChangesAsync();
        }

        var stored = await AuditClient.AllStoredTextAsync(Database);

        Assert.NotNull(stored);

        foreach (var forbidden in AuditClient.ForbiddenColumnNames)
        {
            Assert.DoesNotContain(forbidden, stored, StringComparison.OrdinalIgnoreCase);
        }
    }

    // J16 — RefreshToken خارج الكيانات المدقَّقة أصلاً. الدخول والتجديد يكتبان فيه
    // صفوفاً كثيرة، ولو دخل نطاق التدقيق لامتلأ السجل بالتجزئات
    [Fact]
    public async Task J16_RefreshTokens_AreNeverAudited()
    {
        var identity = await NewIdentityAsync();
        var (_, refreshToken) = await LoginAsync(identity.AdminUserName);

        var rotated = await AuthClient.RefreshAsync(Client, refreshToken);
        rotated.EnsureSuccessStatusCode();

        var rows = await AuditClient.ReadAsync(Database, "RefreshToken");

        Assert.Empty(rows);
    }

    // J17 — قائمة السماح موجبة لا سالبة: الحقول المسجَّلة هي المذكورة بالضبط،
    // فأي عمود يُضاف إلى User لاحقاً يغيب عن السجل حتى يُدرَج صراحةً.
    // هذا ما يجعل السهو آمناً بدل أن يكون تسريباً
    [Fact]
    public async Task J17_UserAuditedProjection_ContainsExactlyTheAllowListedColumns()
    {
        var identity = await NewIdentityAsync();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstAsync(u => u.Id == identity.BranchUserId);
            user.FullName = "اسم آخر";
            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(Database, "User", identity.BranchUserId.ToString());
        var updated = Assert.Single(rows, r => r.Action == AuditClient.UserUpdated);

        using var after = JsonDocument.Parse(updated.AfterJson!);
        var keys = after.RootElement.EnumerateObject().Select(p => p.Name).Order().ToList();

        Assert.Equal([.. AuditClient.UserAuditedColumns.Order()], keys);
    }
}
