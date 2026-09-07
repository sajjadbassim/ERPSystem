using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

public class NumberSequence : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }

    // فارغ = تسلسل على مستوى الشركة. إلزامي لنوع القيد المحاسبي
    public Guid? BranchId { get; set; }

    public SequenceDocumentType DocumentType { get; set; }

    // فارغ = لا تصفير سنوي. قيمة = صف مستقل لكل سنة مالية
    public Guid? FiscalYearId { get; set; }

    public string Prefix { get; set; } = string.Empty;

    // آخر رقم صادر فعلاً. تزيده معاملة الترحيل ذرياً ولا يُشتق من MAX على جدول المستندات
    public long CurrentValue { get; set; }

    public byte PaddingLength { get; set; } = 6;

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Company Company { get; set; } = null!;
    public Branch? Branch { get; set; }
    public FiscalYear? FiscalYear { get; set; }
}
