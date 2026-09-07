using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

public class FiscalPeriod : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FiscalYearId { get; set; }
    public byte PeriodNumber { get; set; }
    public FiscalPeriodType PeriodType { get; set; } = FiscalPeriodType.Regular;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // تمثيل الفترة المقفلة — يفحصه فرض منع الترحيل في القاعدة (R-GL-04)
    public bool IsClosed { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public FiscalYear FiscalYear { get; set; } = null!;
}
