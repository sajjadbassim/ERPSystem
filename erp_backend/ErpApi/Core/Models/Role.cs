using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

// لا يشير إليه أي سطر مالي مرحَّل، فيبقى على حكم R-LIFE-01: حذف منطقي مسموح
public class Role : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // الأدوار النظامية لا تُحذف ولا يُعاد تسميتها
    public bool IsSystemRole { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Company Company { get; set; } = null!;
}
