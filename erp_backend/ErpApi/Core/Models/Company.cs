using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

public class Company : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // عملة الدفاتر — المصدر الوحيد لعملة الأساس في كامل النظام (R-CUR-03)
    public Guid BaseCurrencyId { get; set; }

    // يُرفع عند أول عملية مالية ولا يُخفَض أبداً (R-BASE-02)
    public bool IsBaseCurrencyLocked { get; set; }

    // حد امتصاص باقي التقريب، يُشتق من DecimalPlaces لعملة الأساس عند إنشاء الشركة
    public decimal FxRoundingToleranceBase { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Currency BaseCurrency { get; set; } = null!;
}
