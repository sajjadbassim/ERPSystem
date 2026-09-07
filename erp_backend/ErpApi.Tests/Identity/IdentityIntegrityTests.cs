using ErpApi.Core.Constants;
using ErpApi.Tests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace ErpApi.Tests.Identity;

// سلامة على مستوى القاعدة: تُفحص بـ SQL مباشر لا عبر HTTP، لأن الحارس هنا قيد أو تريجر
public class IdentityIntegrityTests(TestDatabase database) : IdentityTestBase(database)
{
    // I01
    [Fact]
    public async Task I01_UserBranch_BranchFromAnotherCompany_IsRejectedByTrigger()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);

        await Assert.ThrowsAsync<SqlException>(() => IdentityScenarioBuilder.AssignBranchAsync(
            Database, identity.BranchUserId, otherCompany.BranchId, identity.Company.UserId));
    }

    // I02
    [Fact]
    public async Task I02_UserRole_DuplicatePair_IsRejectedByCompositeKey()
    {
        var identity = await NewIdentityAsync();

        await Assert.ThrowsAsync<SqlException>(() => IdentityScenarioBuilder.AssignRoleAsync(
            Database, identity.BranchUserId, identity.BranchRoleId, identity.Company.UserId));
    }

    // I03
    [Fact]
    public async Task I03_RolePermission_DuplicatePair_IsRejectedByCompositeKey()
    {
        var identity = await NewIdentityAsync();

        await Assert.ThrowsAsync<SqlException>(() => IdentityScenarioBuilder.GrantAsync(
            Database, identity.BranchRoleId, [Permissions.JournalEntryRead], identity.Company.UserId));
    }

    // I04
    [Fact]
    public async Task I04_UserBranch_DuplicatePair_IsRejectedByCompositeKey()
    {
        var identity = await NewIdentityAsync();

        await Assert.ThrowsAsync<SqlException>(() => IdentityScenarioBuilder.AssignBranchAsync(
            Database, identity.BranchUserId, identity.Company.BranchId, identity.Company.UserId));
    }

    // I05 — المستخدم الجذر يحل حلقة CreatedByUserId المفرغة عند أول صف
    [Fact]
    public async Task I05_SystemUser_ExistsWithFixedIdAfterMigration()
    {
        var count = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM Users WHERE Id = @id",
            ("@id", Guid.Parse("00000000-0000-0000-0000-000000000001")));

        Assert.Equal(1, count);
    }

    // I06 — المفتاح الأجنبي يمنع محو فاعل تدقيقي له أثر مالي
    [Fact]
    public async Task I06_DeleteUser_WithPostedJournalEntry_IsRejectedByForeignKey()
    {
        var identity = await NewIdentityAsync();
        var request = identity.Company.NewPost(
            identity.Company.Debit(identity.Company.CashAccountId, 100m),
            identity.Company.Credit(identity.Company.RevenueAccountId, 100m));
        request.CreatedByUserId = identity.AdminUserId;
        await PostingClient.PostAsync(Database, request);

        await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            "DELETE FROM Users WHERE Id = @id", ("@id", identity.AdminUserId)));
    }

    // I07
    [Fact]
    public async Task I07_RefreshToken_ExpiryNotAfterCreation_IsRejectedByCheck()
    {
        var identity = await NewIdentityAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() => PostingClient.ExecuteAsync(Database,
            """
            INSERT INTO RefreshTokens (Id, UserId, TokenHash, ExpiresAt, CreatedAt)
            VALUES (NEWID(), @user, 0x00, '2026-01-01', '2026-06-01');
            """,
            ("@user", identity.AdminUserId)));

        // 547 رقم SQL Server القياسي لمخالفة CHECK. بدونه يمرّ الاختبار على أي خطأ SQL آخر —
        // عمود إلزامي ناقص مثلاً — فيبدو حارساً وهو لا يحرس القيد المقصود
        Assert.Equal(547, exception.Number);
    }

    // I08
    [Fact]
    public async Task I08_UserRole_RoleFromAnotherCompany_IsRejected()
    {
        var identity = await NewIdentityAsync();
        var otherCompany = await ScenarioBuilder.CreateAsync(Database);
        var foreignRoleId = Guid.CreateVersion7();
        await IdentityScenarioBuilder.InsertRoleAsync(Database, foreignRoleId,
            otherCompany.CompanyId, "FOREIGN", "دور شركة أخرى", false, otherCompany.UserId);

        await Assert.ThrowsAsync<SqlException>(() => IdentityScenarioBuilder.AssignRoleAsync(
            Database, identity.BranchUserId, foreignRoleId, identity.Company.UserId));
    }

    // I09 — حقول التدقيق في جداول الدفعة الأولى تصبح مفاتيح أجنبية حقيقية
    [Fact]
    public async Task I09_BatchOneAuditColumns_HaveRealForeignKeysToUsers()
    {
        var count = await PostingClient.ScalarAsync<int>(Database,
            """
            SELECT COUNT(DISTINCT fk.parent_object_id)
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id
                                     AND c.column_id = fkc.parent_column_id
            WHERE fk.referenced_object_id = OBJECT_ID('dbo.Users')
              AND c.name = 'CreatedByUserId'
            """);

        Assert.True(count >= 10, $"عدد الجداول المرتبطة بـ Users عبر CreatedByUserId: {count}");
    }

    // I10 — R-LIFE-06: تعطيل المستخدم لا يُخفي قيوده من الدفاتر
    [Fact]
    public async Task I10_DeactivatingUser_KeepsPostedEntriesFullyVisible()
    {
        var identity = await NewIdentityAsync();
        var request = identity.Company.NewPost(
            identity.Company.Debit(identity.Company.CashAccountId, 250m),
            identity.Company.Credit(identity.Company.RevenueAccountId, 250m));
        request.CreatedByUserId = identity.AdminUserId;
        await PostingClient.PostAsync(Database, request);

        await PostingClient.ExecuteAsync(Database,
            "UPDATE Users SET IsActive = 0 WHERE Id = @id", ("@id", identity.AdminUserId));

        var visible = await PostingClient.ScalarAsync<int>(Database,
            """
            SELECT COUNT(*)
            FROM JournalEntries e
            INNER JOIN Users u ON u.Id = e.CreatedByUserId
            WHERE e.Id = @entry
            """,
            ("@entry", request.JournalEntryId));

        Assert.Equal(1, visible);

        var hasIsDeleted = await PostingClient.ScalarAsync<int>(Database,
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'IsDeleted'");

        Assert.Equal(0, hasIsDeleted);
    }
}
