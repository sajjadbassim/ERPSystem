using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<Account>> GetPagedAsync(Guid companyId, PaginationParams pagination, CancellationToken ct = default);

    Task<int> CountAsync(Guid companyId, CancellationToken ct = default);

    Task<bool> CodeExistsInCompanyAsync(Guid companyId, string code, CancellationToken ct = default);

    // الحارس البنيوي لـ R-LIFE-06 على الحساب، نظير HasJournalLinesAsync للعملة
    Task<bool> HasJournalLinesAsync(Guid accountId, CancellationToken ct = default);

    Task AddAsync(Account entity, CancellationToken ct = default);

    void Update(Account entity);
}
