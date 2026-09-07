using ErpApi.Core.Constants;
using Microsoft.AspNetCore.Identity;

namespace ErpApi.Tests.Infrastructure;

public sealed class IdentityScenario
{
    public const string Password = "P@ssw0rd!Test";

    public required Scenario Company { get; init; }

    public required Guid AdminUserId { get; init; }
    public required Guid BranchUserId { get; init; }
    public required Guid NoScopeUserId { get; init; }
    public required Guid InactiveUserId { get; init; }
    public required Guid NoRoleUserId { get; init; }

    public required string AdminUserName { get; init; }
    public required string BranchUserName { get; init; }
    public required string NoScopeUserName { get; init; }
    public required string InactiveUserName { get; init; }
    public required string NoRoleUserName { get; init; }

    public required Guid SystemRoleId { get; init; }
    public required Guid BranchRoleId { get; init; }
}

// البذر بـ SQL خام لا بكيانات EF: كيانات المستخدمين لم تُبنَ بعد (الخطوة ٦).
// الجداول غير موجودة الآن، فالبذر يفشل وكل الاختبارات ترسب — وهو المتوقع.
public static class IdentityScenarioBuilder
{
    private static int _sequence;

    // التجزئة تُحسب هنا بنفس الصنف الذي يجب أن يستعمله التنفيذ (بند 13).
    // هذا الاختيار يقيّد التنفيذ: أي خوارزمية أخرى تجعل E01 يرسب إلى الأبد.
    private static readonly PasswordHasher<object> Hasher = new();

    public static async Task<IdentityScenario> CreateAsync(TestDatabase database)
    {
        var company = await ScenarioBuilder.CreateAsync(database);
        var tag = Interlocked.Increment(ref _sequence);
        var hash = Hasher.HashPassword(new object(), IdentityScenario.Password);

        var adminId = Guid.CreateVersion7();
        var branchUserId = Guid.CreateVersion7();
        var noScopeId = Guid.CreateVersion7();
        var inactiveId = Guid.CreateVersion7();
        var noRoleId = Guid.CreateVersion7();
        var systemRoleId = Guid.CreateVersion7();
        var branchRoleId = Guid.CreateVersion7();

        var names = new
        {
            Admin = $"admin{tag:D4}",
            Branch = $"branch{tag:D4}",
            NoScope = $"noscope{tag:D4}",
            Inactive = $"inactive{tag:D4}",
            NoRole = $"norole{tag:D4}"
        };

        foreach (var (id, userName, isActive) in new[]
                 {
                     (adminId, names.Admin, true),
                     (branchUserId, names.Branch, true),
                     (noScopeId, names.NoScope, true),
                     (inactiveId, names.Inactive, false),
                     (noRoleId, names.NoRole, true)
                 })
        {
            await InsertUserAsync(database, id, company.CompanyId, userName, hash, isActive, company.UserId);
        }

        await InsertRoleAsync(database, systemRoleId, company.CompanyId, "SYSADMIN", "مدير النظام", true, company.UserId);
        await InsertRoleAsync(database, branchRoleId, company.CompanyId, "BRANCHACC", "محاسب فرع", false, company.UserId);

        await GrantAsync(database, systemRoleId, Permissions.All, company.UserId);
        await GrantAsync(database, branchRoleId,
            [Permissions.JournalEntryRead, Permissions.JournalEntryPost, Permissions.TrialBalanceRead],
            company.UserId);

        await AssignRoleAsync(database, adminId, systemRoleId, company.UserId);
        await AssignRoleAsync(database, branchUserId, branchRoleId, company.UserId);
        await AssignRoleAsync(database, noScopeId, branchRoleId, company.UserId);
        await AssignRoleAsync(database, inactiveId, branchRoleId, company.UserId);

        // مستخدم الفرع مقصور على الفرع الأول. NoScope بلا أي فرع وبلا AllBranches
        await AssignBranchAsync(database, branchUserId, company.BranchId, company.UserId);
        await AssignBranchAsync(database, inactiveId, company.BranchId, company.UserId);

        return new IdentityScenario
        {
            Company = company,
            AdminUserId = adminId,
            BranchUserId = branchUserId,
            NoScopeUserId = noScopeId,
            InactiveUserId = inactiveId,
            NoRoleUserId = noRoleId,
            AdminUserName = names.Admin,
            BranchUserName = names.Branch,
            NoScopeUserName = names.NoScope,
            InactiveUserName = names.Inactive,
            NoRoleUserName = names.NoRole,
            SystemRoleId = systemRoleId,
            BranchRoleId = branchRoleId
        };
    }

    public static Task InsertUserAsync(
        TestDatabase database, Guid id, Guid companyId, string userName, string passwordHash,
        bool isActive, Guid createdBy) =>
        PostingClient.ExecuteAsync(database,
            """
            INSERT INTO Users (Id, CompanyId, UserName, FullName, Email, PasswordHash, SecurityStamp,
                               IsActive, CreatedAt, CreatedByUserId)
            VALUES (@id, @company, @userName, @userName, NULL, @hash, NEWID(),
                    @isActive, SYSUTCDATETIME(), @createdBy);
            """,
            ("@id", id), ("@company", companyId), ("@userName", userName),
            ("@hash", passwordHash), ("@isActive", isActive), ("@createdBy", createdBy));

    public static Task InsertRoleAsync(
        TestDatabase database, Guid id, Guid companyId, string code, string name, bool isSystem, Guid createdBy) =>
        PostingClient.ExecuteAsync(database,
            """
            INSERT INTO Roles (Id, CompanyId, Code, Name, IsSystemRole, IsActive, IsDeleted,
                               CreatedAt, CreatedByUserId)
            VALUES (@id, @company, @code, @name, @isSystem, 1, 0, SYSUTCDATETIME(), @createdBy);
            """,
            ("@id", id), ("@company", companyId), ("@code", code),
            ("@name", name), ("@isSystem", isSystem), ("@createdBy", createdBy));

    public static async Task GrantAsync(
        TestDatabase database, Guid roleId, IEnumerable<string> permissionCodes, Guid createdBy)
    {
        foreach (var code in permissionCodes)
        {
            await PostingClient.ExecuteAsync(database,
                """
                INSERT INTO RolePermissions (RoleId, PermissionCode, CreatedAt, CreatedByUserId)
                VALUES (@role, @code, SYSUTCDATETIME(), @createdBy);
                """,
                ("@role", roleId), ("@code", code), ("@createdBy", createdBy));
        }
    }

    public static Task AssignRoleAsync(TestDatabase database, Guid userId, Guid roleId, Guid createdBy) =>
        PostingClient.ExecuteAsync(database,
            """
            INSERT INTO UserRoles (UserId, RoleId, CreatedAt, CreatedByUserId)
            VALUES (@user, @role, SYSUTCDATETIME(), @createdBy);
            """,
            ("@user", userId), ("@role", roleId), ("@createdBy", createdBy));

    public static Task AssignBranchAsync(TestDatabase database, Guid userId, Guid branchId, Guid createdBy) =>
        PostingClient.ExecuteAsync(database,
            """
            INSERT INTO UserBranches (UserId, BranchId, CreatedAt, CreatedByUserId)
            VALUES (@user, @branch, SYSUTCDATETIME(), @createdBy);
            """,
            ("@user", userId), ("@branch", branchId), ("@createdBy", createdBy));
}
