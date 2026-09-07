using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<Branch>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    // التفرّد على مستوى الشركة لا النظام، مطابقةً لـ UQ_Branch_CompanyId_Code
    Task<bool> CodeExistsInCompanyAsync(Guid companyId, string code, CancellationToken ct = default);

    Task AddAsync(Branch entity, CancellationToken ct = default);

    void Update(Branch entity);
}
