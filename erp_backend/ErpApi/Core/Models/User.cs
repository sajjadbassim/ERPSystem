using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

// بلا IsDeleted وبلا Query Filter (R-LIFE-06): المستخدم فاعل تدقيقي دائم يشير إليه
// JournalEntry.CreatedByUserId، وإخفاؤه يُسقط قيوده من كل استعلام يمرّ بالتنقّل.
// IsActive وحده يمنع الاستخدام الجديد ولا يمسّ التاريخ.
public class User : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // NULL للمستخدم الجذر وحده، ويفرض ذلك CK_User_SystemUserHasNoCompany
    public Guid? CompanyId { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // اختياري بقرار عمل: معرّف الدخول هو UserName، والبريد بيانات تواصل لا هوية
    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    // يتغيّر عند كل حدث يجب أن يُسقط الجلسات القائمة فوراً (تغيير كلمة المرور، الإبطال)
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Company? Company { get; set; }
}
