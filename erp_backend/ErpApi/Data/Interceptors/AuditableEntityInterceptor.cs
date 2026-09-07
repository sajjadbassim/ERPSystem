using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ErpApi.Data.Interceptors;

// مسؤوليتان في مكان واحد كما ينص ROADMAP §4.2: ضبط حقول التدقيق، وكتابة السجل الكامل.
// كلتاهما تعمل على نفس اللقطة من ChangeTracker وفي نفس المعاملة، ففصلهما كان سيقتضي
// المرور مرتين على التغييرات نفسها.
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    // النصوص العربية تُخزَّن كما هي لا كـ أ: السجل يُقرأ بعين بشرية عند التحقيق
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    // قائمة سماح موجبة لكل كيان. العمود غير المذكور هنا لا يدخل السجل أبداً.
    // PasswordHash و SecurityStamp و TokenHash غائبة لأنها غير مذكورة — لا لأن رمزاً
    // يستثنيها. الفرق أن قائمة الحظر تفشل مفتوحة عند إضافة عمود سرّي جديد، وهذه تفشل مغلقة
    private static readonly Dictionary<Type, string[]> AuditedEntities = new()
    {
        // الهوية والصلاحيات (§4.2)
        [typeof(User)] = ["UserName", "FullName", "Email", "CompanyId", "IsActive"],
        [typeof(Role)] = ["Code", "Name", "IsSystemRole", "IsActive", "IsDeleted", "CompanyId"],
        [typeof(UserRole)] = ["UserId", "RoleId"],
        [typeof(RolePermission)] = ["RoleId", "PermissionCode"],
        [typeof(UserBranch)] = ["UserId", "BranchId"],

        // البيانات الأساسية (§4.3 الدفعة أ). لا سرّ في أعمدة هذه الأربعة، ومع ذلك
        // القائمة موجبة كسابقتها: عمود يُضاف غداً لا يدخل السجل حتى يُدرَج صراحةً.
        // أعمدة التدقيق نفسها (CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId)
        // غائبة عمداً — صف السجل يحمل الفاعل والزمن، فتكرارها داخل اللقطة ضجيج
        [typeof(Currency)] = ["Code", "Name", "Symbol", "DecimalPlaces", "IsActive", "IsDeleted"],
        [typeof(Company)] =
        [
            "Code", "Name", "BaseCurrencyId", "IsBaseCurrencyLocked",
            "FxRoundingToleranceBase", "IsActive", "IsDeleted"
        ],
        [typeof(Branch)] = ["CompanyId", "Code", "Name", "IsActive", "IsDeleted"],
        [typeof(FxRate)] =
            ["FromCurrencyId", "ToCurrencyId", "Rate", "RateDate", "RateSource", "IsDeleted"]
    };

    private readonly ICurrentUserService _currentUser;

    public AuditableEntityInterceptor(ICurrentUserService currentUser) => _currentUser = currentUser;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext context)
    {
        var now = DateTime.UtcNow;

        // الفاعل من الجلسة، وفي غيابها المستخدم الجذر. Guid.Empty يعني «لا نعرف من فعلها»
        // وهو أسوأ من «النظام فعلها»، لأن الأول يمرّ كقيمة سليمة في كل استعلام
        var actorId = _currentUser.UserId ?? SystemUser.Id;

        foreach (var entry in context.ChangeTracker.Entries<ICreationAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByUserId = actorId;
            }
        }

        // اللقطة تُؤخذ قبل الإضافة: تعديل ChangeTracker أثناء المرور عليه يرمي
        var audited = context.ChangeTracker.Entries()
            .Where(e => AuditedEntities.ContainsKey(e.Entity.GetType())
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in audited)
        {
            context.Add(BuildAuditRow(entry, actorId, now));
        }
    }

    private static AuditLogEntry BuildAuditRow(EntityEntry entry, Guid actorId, DateTime now)
    {
        var type = entry.Entity.GetType();
        var allowed = AuditedEntities[type];

        // التعديل يكتب اللقطتين حتى لو لم يتغيّر عمود مدقَّق. تغيير كلمة المرور مثاله:
        // ما تغيّر خارج قائمة السماح، فتتطابق اللقطتان — والصف يشهد أن التعديل وقع
        // دون أن يشهد بما تغيّر. الحدث أثر مطلوب، والسرّ ليس كذلك
        var (action, before, after) = entry.State switch
        {
            EntityState.Added => ($"{type.Name}.Created", (string?)null, Project(entry.CurrentValues, allowed)),
            EntityState.Deleted => ($"{type.Name}.Deleted", Project(entry.OriginalValues, allowed), (string?)null),
            _ => ($"{type.Name}.Updated",
                  Project(entry.OriginalValues, allowed),
                  Project(entry.CurrentValues, allowed))
        };

        return new AuditLogEntry
        {
            UserId = actorId,
            Action = action,
            EntityName = type.Name,
            EntityKey = BuildKey(entry.Entity),
            BeforeJson = before,
            AfterJson = after,
            CreatedAt = now
        };
    }

    private static string Project(PropertyValues values, string[] allowed)
    {
        var projection = new Dictionary<string, object?>(allowed.Length);

        foreach (var name in allowed)
        {
            projection[name] = values[name];
        }

        return JsonSerializer.Serialize(projection, JsonOptions);
    }

    // نصّ مقروء يبقى بعد محو الصف. المركّب بفاصل | لجداول الربط
    private static string BuildKey(object entity) => entity switch
    {
        User u => u.Id.ToString(),
        Role r => r.Id.ToString(),
        UserRole ur => $"{ur.UserId}|{ur.RoleId}",
        RolePermission rp => $"{rp.RoleId}|{rp.PermissionCode}",
        UserBranch ub => $"{ub.UserId}|{ub.BranchId}",
        Currency c => c.Id.ToString(),
        Company co => co.Id.ToString(),
        Branch b => b.Id.ToString(),
        FxRate f => f.Id.ToString(),
        _ => string.Empty
    };
}
