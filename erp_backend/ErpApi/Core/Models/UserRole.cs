using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

// جدول ربط خالص: مفتاحه مركّب طبيعي من عموديه، بلا مفتاح بديل.
// يُحذف فعلياً عند سحب الدور، لأن صفاً مُعطَّلاً يستلزم فلترته في كل فحص صلاحية
// ونسيان الفلترة يفشل مفتوحاً. تاريخه يعيش في AuditLog (ROADMAP §4.2).
public class UserRole : ICreationAuditable
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
