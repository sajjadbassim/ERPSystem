using System.Reflection;
using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.DevSeedTool;

// ‏البذر **إدراجيّ حصراً**: كل خطوة تبحث بمفتاحها الطبيعي وتُنشئ إن غاب، ولا تُعدّل
// صفاً قائماً ولا تحذف شيئاً أبداً. فتشغيل الأداة مرتين لا يُنتج تكراراً ولا يمسّ
// بيانات أُدخلت يدوياً — نفس منطق `WHERE NOT EXISTS` في بذر المستخدم الجذر.
//
// ‏و`CreatedByUserId` يُضبط **صراحةً** على كل صف. المعترِض يضبط `CreatedAt` عند
// الإضافة و`UpdatedByUserId` عند التعديل، **ولا يمسّ `CreatedByUserId`** — مقيس من
// ‏`AuditableEntityInterceptor.Apply` (الأسطر 85-100)، ومؤكَّد بأن `BranchService`
// يضبطه بنفسه. وتركه يعني `Guid.Empty` فيسقط الإدراج على
// ‏`FK_..._Users_CreatedByUserId` — وقد سقط فعلاً قبل التصحيح.
//
// والفاعل `SystemUser.Id`: المستخدم الجذر المبذور في الترحيل، وهو الفاعل نفسه الذي
// يسنده المعترِض للتدقيق حين لا جلسة.
public sealed class DevSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DevSeeder(AppDbContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<DevSeedReport> RunAsync(CancellationToken ct = default)
    {
        var report = new DevSeedReport();

        var currency = await EnsureCurrencyAsync(report, ct);
        var company = await EnsureCompanyAsync(currency, report, ct);
        var branch = await EnsureBranchAsync(company, report, ct);
        var role = await EnsureRoleAsync(company, report, ct);
        var user = await EnsureUserAsync(company, report, ct);

        await EnsureRolePermissionsAsync(role, report, ct);
        await EnsureUserRoleAsync(user, role, report, ct);
        await EnsureUserBranchAsync(user, branch, report, ct);

        await _context.SaveChangesAsync(ct);

        return report;
    }

    private async Task<Currency> EnsureCurrencyAsync(DevSeedReport report, CancellationToken ct)
    {
        var existing = await _context.Set<Currency>()
            .FirstOrDefaultAsync(c => c.Code == DevSeedData.CurrencyCode, ct);

        if (existing is not null)
        {
            report.Skipped.Add($"العملة {DevSeedData.CurrencyCode} موجودة");
            return existing;
        }

        var currency = new Currency
        {
            Code = DevSeedData.CurrencyCode,
            Name = DevSeedData.CurrencyName,
            Symbol = DevSeedData.CurrencySymbol,
            DecimalPlaces = DevSeedData.CurrencyDecimalPlaces,
            CreatedByUserId = SystemUser.Id
        };

        await _context.AddAsync(currency, ct);
        report.Created.Add($"العملة {DevSeedData.CurrencyCode}");

        return currency;
    }

    private async Task<Company> EnsureCompanyAsync(
        Currency currency, DevSeedReport report, CancellationToken ct)
    {
        var existing = await _context.Set<Company>()
            .FirstOrDefaultAsync(c => c.Code == DevSeedData.CompanyCode, ct);

        if (existing is not null)
        {
            report.Skipped.Add($"الشركة {DevSeedData.CompanyCode} موجودة");
            return existing;
        }

        var company = new Company
        {
            Code = DevSeedData.CompanyCode,
            Name = DevSeedData.CompanyName,
            BaseCurrencyId = currency.Id,

            // ‏الاشتقاق نفسه الذي يجريه `CompanyService`: 10^(−DecimalPlaces)
            // (R-AMT-07-a). نسخُه هنا لأن الخدمة تفرض صلاحية لا يملكها البذر
            FxRoundingToleranceBase = (decimal)Math.Pow(10, -DevSeedData.CurrencyDecimalPlaces),
            CreatedByUserId = SystemUser.Id
        };

        await _context.AddAsync(company, ct);
        report.Created.Add($"الشركة {DevSeedData.CompanyCode}");

        return company;
    }

    private async Task<Branch> EnsureBranchAsync(
        Company company, DevSeedReport report, CancellationToken ct)
    {
        var existing = await _context.Set<Branch>()
            .FirstOrDefaultAsync(b => b.CompanyId == company.Id && b.Code == DevSeedData.BranchCode, ct);

        if (existing is not null)
        {
            report.Skipped.Add($"الفرع {DevSeedData.BranchCode} موجود");
            return existing;
        }

        var branch = new Branch
        {
            CompanyId = company.Id,
            Code = DevSeedData.BranchCode,
            Name = DevSeedData.BranchName,
            CreatedByUserId = SystemUser.Id
        };

        await _context.AddAsync(branch, ct);

        // ‏يُحاكي أثر `BranchService.CreateAsync` الجانبيّ حرفياً: فرعٌ بلا عدّاده
        // حالة وسيطة لا يجوز أن تُرى، وأول ترحيل فيه كان سيُنشئ العدّاد داخل معاملته
        // فتنشأ فجوة تسلسل يمنعها R-NUM-01
        await _context.AddAsync(new NumberSequence
        {
            CompanyId = company.Id,
            BranchId = branch.Id,
            DocumentType = SequenceDocumentType.JournalEntry,
            FiscalYearId = null,
            Prefix = "JV-",
            CurrentValue = 0,
            PaddingLength = 6,
            CreatedByUserId = SystemUser.Id
        }, ct);

        report.Created.Add($"الفرع {DevSeedData.BranchCode} وعدّاد قيوده");

        return branch;
    }

    private async Task<Role> EnsureRoleAsync(
        Company company, DevSeedReport report, CancellationToken ct)
    {
        var existing = await _context.Set<Role>()
            .FirstOrDefaultAsync(r => r.CompanyId == company.Id && r.Code == DevSeedData.RoleCode, ct);

        if (existing is not null)
        {
            report.Skipped.Add($"الدور {DevSeedData.RoleCode} موجود");
            return existing;
        }

        var role = new Role
        {
            CompanyId = company.Id,
            Code = DevSeedData.RoleCode,
            Name = DevSeedData.RoleName,
            IsSystemRole = false,
            CreatedByUserId = SystemUser.Id
        };

        await _context.AddAsync(role, ct);
        report.Created.Add($"الدور {DevSeedData.RoleCode}");

        return role;
    }

    private async Task<User> EnsureUserAsync(
        Company company, DevSeedReport report, CancellationToken ct)
    {
        var existing = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.UserName == DevSeedData.UserName, ct);

        if (existing is not null)
        {
            report.Skipped.Add($"المستخدم {DevSeedData.UserName} موجود");
            return existing;
        }

        var user = new User
        {
            CompanyId = company.Id,
            UserName = DevSeedData.UserName,
            FullName = DevSeedData.FullName,
            CreatedByUserId = SystemUser.Id
        };

        // ‏نفس `IPasswordHasher<User>` المحقون في `AuthService` — لا تجزئة موازية.
        // تجزئة أخرى كانت ستنجح بالمصادفة لا بالعقد، وتنكسر متى غيّر الخادم صنفه
        user.PasswordHash = _passwordHasher.HashPassword(user, DevSeedData.Password);

        await _context.AddAsync(user, ct);
        report.Created.Add($"المستخدم {DevSeedData.UserName}");

        return user;
    }

    // ‏**كل الصلاحيات بالانعكاس لا بلائحة يدوية.** لائحة مكتوبة تنحرف صامتةً عند
    // إضافة صلاحية جديدة، فيعجز مستخدم التطوير عن شاشة بُنيت لتوّها ويبدو العطب
    // في الشاشة لا في البذرة.
    private async Task EnsureRolePermissionsAsync(Role role, DevSeedReport report, CancellationToken ct)
    {
        var all = ReadPermissionCatalog();

        var granted = await _context.Set<RolePermission>()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionCode)
            .ToListAsync(ct);

        var missing = all.Except(granted, StringComparer.Ordinal).ToList();

        foreach (var code in missing)
        {
            await _context.AddAsync(
                new RolePermission
                {
                    RoleId = role.Id,
                    PermissionCode = code,
                    CreatedByUserId = SystemUser.Id
                }, ct);
        }

        if (missing.Count == 0)
        {
            report.Skipped.Add($"صلاحيات الدور مكتملة ({all.Count})");
        }
        else
        {
            report.Created.Add($"{missing.Count} صلاحية للدور (من أصل {all.Count})");
        }
    }

    private static List<string> ReadPermissionCatalog() =>
        [.. typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(code => code, StringComparer.Ordinal)];

    private async Task EnsureUserRoleAsync(User user, Role role, DevSeedReport report, CancellationToken ct)
    {
        var exists = await _context.Set<UserRole>()
            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);

        if (exists)
        {
            report.Skipped.Add("ربط المستخدم بالدور موجود");
            return;
        }

        await _context.AddAsync(
            new UserRole { UserId = user.Id, RoleId = role.Id, CreatedByUserId = SystemUser.Id }, ct);
        report.Created.Add("ربط المستخدم بالدور");
    }

    private async Task EnsureUserBranchAsync(User user, Branch branch, DevSeedReport report, CancellationToken ct)
    {
        var exists = await _context.Set<UserBranch>()
            .AnyAsync(ub => ub.UserId == user.Id && ub.BranchId == branch.Id, ct);

        if (exists)
        {
            report.Skipped.Add("نطاق الفرع موجود");
            return;
        }

        await _context.AddAsync(
            new UserBranch { UserId = user.Id, BranchId = branch.Id, CreatedByUserId = SystemUser.Id }, ct);
        report.Created.Add("نطاق الفرع");
    }
}

public sealed class DevSeedReport
{
    public List<string> Created { get; } = [];

    public List<string> Skipped { get; } = [];
}
