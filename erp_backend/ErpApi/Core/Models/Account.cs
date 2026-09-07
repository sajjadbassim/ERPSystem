using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

public class Account : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Guid? ParentAccountId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public NormalBalance NormalBalance { get; set; }

    // الحساب التجميعي لا يُرحَّل عليه مباشرة
    public bool IsPostable { get; set; } = true;

    // فارغ = يقبل أي عملة معاملة، وقيمة = مقيَّد بعملة واحدة
    public Guid? CurrencyId { get; set; }

    // يجد المحرك حسابات فروقات الصرف والتقريب دون بحث بالاسم أو الكود (R-GL-07)
    public SystemAccountRole? SystemAccountRole { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Company Company { get; set; } = null!;
    public Account? ParentAccount { get; set; }
    public Currency? Currency { get; set; }
}
