using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // ‏`companyId` حدّ لا يُتجاوز. و`scopedUserId` هو الحدّ الثاني المستقلّ عنه:
    // ‏`null` ⟵ كل فروع الشركة (حاملُ `AllBranches`)، وقيمةٌ ⟵ فروع ذلك المستخدم
    // المخصَّصة **داخل شركته**. فالحدّان يُطبَّقان معاً لا أحدهما بدل الآخر
    Task<List<Branch>> GetPagedAsync(
        Guid companyId, Guid? scopedUserId, PaginationParams pagination,
        CancellationToken ct = default);

    Task<int> CountAsync(Guid companyId, Guid? scopedUserId, CancellationToken ct = default);

    // التفرّد على مستوى الشركة لا النظام، مطابقةً لـ UQ_Branch_CompanyId_Code
    Task<bool> CodeExistsInCompanyAsync(Guid companyId, string code, CancellationToken ct = default);

    Task AddAsync(Branch entity, CancellationToken ct = default);

    void Update(Branch entity);
}
