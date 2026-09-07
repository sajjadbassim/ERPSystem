using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface IFxRateRepository
{
    Task<FxRate?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<FxRate>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    // النطاق الذي يحرسه UQ_FxRate_From_To_Date_Source: سعران له يجعلان
    // سؤال «بأي سعر؟» بلا جواب
    Task<bool> ScopeExistsAsync(
        Guid fromCurrencyId, Guid toCurrencyId, DateOnly rateDate, FxRateSource source,
        CancellationToken ct = default);

    Task AddAsync(FxRate entity, CancellationToken ct = default);
}
