using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

// نطاق الفرع. غيابه لا يعني «كل الفروع» بل «لا فرع» — الفشل المغلق.
public class UserBranch : ICreationAuditable
{
    public Guid UserId { get; set; }
    public Guid BranchId { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public User User { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}
