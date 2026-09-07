using ErpApi.Core.Models;
using ErpApi.Data;
using ErpApi.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ErpApi.Tests.Audit;

// سلامة السجل نفسه: يُكتب ولا يُمسّ. تُفحص بـ SQL مباشر لأن الحارس قيد أو تريجر
public class AuditLogIntegrityTests(TestDatabase database) : IdentityTestBase(database)
{
    // J01 — سجل يُحذف منطقياً ليس سجلاً. غياب العمود يُسقط المشكلة بدل إدارتها
    [Fact]
    public async Task J01_AuditLogEntries_HasNoIsDeletedColumn()
    {
        var tableExists = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'AuditLogEntries'");

        Assert.Equal(1, tableExists);

        var hasIsDeleted = await PostingClient.ScalarAsync<int>(Database,
            """
            SELECT COUNT(*) FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.AuditLogEntries') AND name = 'IsDeleted'
            """);

        Assert.Equal(0, hasIsDeleted);
    }

    // J02 — التعديل ممنوع. سجل قابل للتحرير يوثّق ما يريده المحرِّر لا ما جرى
    [Fact]
    public async Task J02_UpdateOnAuditRow_IsRejectedByTrigger()
    {
        await SeedOneRowAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(
            Database, "UPDATE AuditLogEntries SET Action = N'tampered'"));

        Assert.Equal(PostingErrors.TriggerAuditLogImmutable, exception.Number);
    }

    // J03 — الحذف ممنوع كذلك، وإلا مُحي الأثر بدل تعديله
    [Fact]
    public async Task J03_DeleteOnAuditRow_IsRejectedByTrigger()
    {
        await SeedOneRowAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(
            Database, "DELETE FROM AuditLogEntries"));

        Assert.Equal(PostingErrors.TriggerAuditLogImmutable, exception.Number);
    }

    // J04 — جوهر قرار EntityKey النصّي: صف الربط يُحذف فعلياً، والسجل يبقى مقروءاً بعده.
    // مفتاح أجنبي هنا كان سيمنع الحذف أو يجرّ السجل معه، وكلاهما يُفرغ السجل من معناه
    [Fact]
    public async Task J04_EntityKeySurvivesHardDeleteOfReferencedRow_AndHasNoForeignKey()
    {
        var identity = await NewIdentityAsync();
        var expectedKey = $"{identity.BranchUserId}|{identity.BranchRoleId}";

        await using (var scope = CreateAppScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await context.UserRoles.FirstAsync(
                ur => ur.UserId == identity.BranchUserId && ur.RoleId == identity.BranchRoleId);

            context.UserRoles.Remove(link);
            await context.SaveChangesAsync();
        }

        var rows = await AuditClient.ReadAsync(Database, "UserRole", expectedKey);

        Assert.Single(rows);
        Assert.Equal(AuditClient.UserRoleDeleted, rows[0].Action);

        var linkRemains = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM UserRoles WHERE UserId = @u AND RoleId = @r",
            ("@u", identity.BranchUserId), ("@r", identity.BranchRoleId));

        Assert.Equal(0, linkRemains);

        // المفتاح الوحيد المسموح هو الفاعل إلى Users. أي مفتاح آخر يربط السجل
        // بصفوف تُحذف فعلاً، فيُمنع الحذف أو يُجرّ السجل معه
        var otherForeignKeys = await PostingClient.ScalarAsync<int>(Database,
            """
            SELECT COUNT(*) FROM sys.foreign_keys
            WHERE parent_object_id = OBJECT_ID('dbo.AuditLogEntries')
              AND referenced_object_id <> OBJECT_ID('dbo.Users')
            """);

        Assert.Equal(0, otherForeignKeys);
    }

    private async Task SeedOneRowAsync()
    {
        var identity = await NewIdentityAsync();

        await using var scope = CreateAppScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.UserBranches.Add(new UserBranch
        {
            UserId = identity.NoScopeUserId,
            BranchId = identity.Company.BranchId,
            CreatedByUserId = identity.Company.UserId
        });

        await context.SaveChangesAsync();
    }
}
