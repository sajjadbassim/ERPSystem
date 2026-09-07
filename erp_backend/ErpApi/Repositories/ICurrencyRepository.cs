using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface ICurrencyRepository
{
    Task<Currency?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<Currency>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);

    // الحارس البنيوي لـ R-LIFE-06: هل لهذه العملة سطر مالي مرحَّل؟
    Task<bool> HasJournalLinesAsync(Guid currencyId, CancellationToken ct = default);

    Task AddAsync(Currency entity, CancellationToken ct = default);

    void Update(Currency entity);
}
