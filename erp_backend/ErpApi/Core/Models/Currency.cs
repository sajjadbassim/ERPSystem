using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

public class Currency : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }

    // خانات العرض العشرية خاصية للعملة لا ثابت في الكود (R-AMT-06)
    public byte DecimalPlaces { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
