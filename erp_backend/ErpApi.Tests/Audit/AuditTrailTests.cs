using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using ErpApi.Data;
using ErpApi.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ErpApi.Tests.Audit;

// ما الذي يُسجَّل فعلاً. المحرّك هنا Interceptor على SaveChanges لكيانات الهوية الخمسة
public class AuditTrailTests(TestDatabase database) : IdentityTestBase(database)
{
    // J05 — الإنشاء: لا قيمة قبل، وقيمة بعد
    [Fact]
    public async Task J05_CreatingUser_WritesRowWithAfterOnly()
    {
        var identity = await NewIdentityAsync();
        var newUserId = Guid.CreateVersion7();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(new User
            {
                Id = newUserId,
                CompanyId = identity.Company.CompanyId,
                UserName = $"created{newUserId:N}"[..20],
                FullName = "مستخدم منشأ",
                PasswordHash = "irrelevant-for-this-case",
                CreatedByUserId = identity.Company.UserId
            });

            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(Database, "User", newUserId.ToString());

        Assert.Single(rows);
        Assert.Equal(AuditClient.UserCreated, rows[0].Action);
        Assert.Null(rows[0].BeforeJson);
        Assert.NotNull(rows[0].AfterJson);
    }

    // J06 — التعديل: القيمتان معاً، والقيمة القديمة مقروءة بعد أن استُبدلت
    [Fact]
    public async Task J06_UpdatingUser_WritesBeforeAndAfterWithChangedValue()
    {
        var identity = await NewIdentityAsync();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstAsync(u => u.Id == identity.BranchUserId);
            user.FullName = "اسم بعد التعديل";
            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(Database, "User", identity.BranchUserId.ToString());
        var updated = Assert.Single(rows, r => r.Action == AuditClient.UserUpdated);

        Assert.NotNull(updated.BeforeJson);
        Assert.NotNull(updated.AfterJson);
        Assert.Contains(identity.BranchUserName, updated.BeforeJson);
        Assert.Contains("اسم بعد التعديل", updated.AfterJson);
    }

    // J07 — منح دور: نصف السؤال الذي بُني السجل لأجله
    [Fact]
    public async Task J07_GrantingRole_IsRecorded()
    {
        var identity = await NewIdentityAsync();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.UserRoles.Add(new UserRole
            {
                UserId = identity.NoRoleUserId,
                RoleId = identity.BranchRoleId,
                CreatedByUserId = identity.Company.UserId
            });

            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(
            Database, "UserRole", $"{identity.NoRoleUserId}|{identity.BranchRoleId}");

        Assert.Single(rows);
        Assert.Equal(AuditClient.UserRoleCreated, rows[0].Action);
        Assert.NotNull(rows[0].AfterJson);
    }

    // J08 — سحب دور: نصفه الآخر. الحذف فعليّ، فالسجل هو الأثر الوحيد الباقي
    [Fact]
    public async Task J08_RevokingRole_IsRecordedWithBeforeOnly()
    {
        var identity = await NewIdentityAsync();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await context.UserRoles.FirstAsync(
                ur => ur.UserId == identity.BranchUserId && ur.RoleId == identity.BranchRoleId);

            context.UserRoles.Remove(link);
            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(
            Database, "UserRole", $"{identity.BranchUserId}|{identity.BranchRoleId}");

        Assert.Single(rows);
        Assert.Equal(AuditClient.UserRoleDeleted, rows[0].Action);
        Assert.NotNull(rows[0].BeforeJson);
        Assert.Null(rows[0].AfterJson);
    }

    // J09 — سحب فرع: نطاق الوصول يتغيّر بلا أثر في جدوله، فالأثر هنا
    [Fact]
    public async Task J09_RevokingBranchAccess_IsRecorded()
    {
        var identity = await NewIdentityAsync();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await context.UserBranches.FirstAsync(
                ub => ub.UserId == identity.BranchUserId && ub.BranchId == identity.Company.BranchId);

            context.UserBranches.Remove(link);
            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(
            Database, "UserBranch", $"{identity.BranchUserId}|{identity.Company.BranchId}");

        Assert.Single(rows);
        Assert.Equal(AuditClient.UserBranchDeleted, rows[0].Action);
    }

    // J10 — منح صلاحية لدور
    [Fact]
    public async Task J10_GrantingPermissionToRole_IsRecorded()
    {
        var identity = await NewIdentityAsync();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = identity.BranchRoleId,
                PermissionCode = Permissions.UserManage,
                CreatedByUserId = identity.Company.UserId
            });

            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(
            Database, "RolePermission", $"{identity.BranchRoleId}|{Permissions.UserManage}");

        Assert.Single(rows);
        Assert.Equal(AuditClient.RolePermissionCreated, rows[0].Action);
    }

    // J11 — السؤال الذي تنص عليه ROADMAP حرفياً: «من منح فلاناً حق الترحيل، ومتى، ومن سحبه».
    // لا يُجاب باستعلام واحد إن لم يكن الحدثان في الجدول نفسه بالترتيب
    [Fact]
    public async Task J11_GrantThenRevoke_YieldsBothEventsInOrder()
    {
        var identity = await NewIdentityAsync();
        var key = $"{identity.NoRoleUserId}|{identity.BranchRoleId}";

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.UserRoles.Add(new UserRole
            {
                UserId = identity.NoRoleUserId,
                RoleId = identity.BranchRoleId,
                CreatedByUserId = identity.Company.UserId
            });

            await context.SaveChangesAsync();
        }

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await context.UserRoles.FirstAsync(
                ur => ur.UserId == identity.NoRoleUserId && ur.RoleId == identity.BranchRoleId);

            context.UserRoles.Remove(link);
            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(Database, "UserRole", key);

        Assert.Equal(2, rows.Count);
        Assert.Equal(AuditClient.UserRoleCreated, rows[0].Action);
        Assert.Equal(AuditClient.UserRoleDeleted, rows[1].Action);
    }

    // J12 — الفاعل هو من نفّذ لا من وقع عليه الفعل. خلطهما يجعل السجل يتهم الضحية
    [Fact]
    public async Task J12_ActorIsTheAuthenticatedCaller_NotTheAffectedEntity()
    {
        var identity = await NewIdentityAsync();
        var (accessToken, _) = await LoginAsync(identity.AdminUserName);

        var response = await AuthClient.PostAsync(Client,
            $"{AuthClient.RolesPath}/{identity.BranchRoleId}/rename", accessToken,
            new { name = "اسم جديد للدور" });

        response.EnsureSuccessStatusCode();

        var rows = await AuditClient.ReadAsync(Database, "Role", identity.BranchRoleId.ToString());
        var renamed = Assert.Single(rows, r => r.Action == AuditClient.RoleUpdated);

        Assert.Equal(identity.AdminUserId, renamed.UserId);
    }

    // J13 — الفاعل حين لا يوجد طلب HTTP يسقط إلى الجذر بدل أن يبقى فارغاً.
    // Guid.Empty في عمود الفاعل يعني «لا نعرف»، وهو أسوأ من «النظام فعلها»
    [Fact]
    public async Task J13_ActorFallsBackToSystemUser_WhenNoHttpContext()
    {
        var identity = await NewIdentityAsync();

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.UserRoles.Add(new UserRole
            {
                UserId = identity.NoRoleUserId,
                RoleId = identity.SystemRoleId,
                CreatedByUserId = identity.Company.UserId
            });

            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(
            Database, "UserRole", $"{identity.NoRoleUserId}|{identity.SystemRoleId}");

        Assert.Single(rows);
        Assert.Equal(SystemUser.Id, rows[0].UserId);
    }
}
