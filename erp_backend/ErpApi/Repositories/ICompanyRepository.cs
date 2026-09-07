using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<Company>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);

    Task AddAsync(Company entity, CancellationToken ct = default);

    void Update(Company entity);
}
