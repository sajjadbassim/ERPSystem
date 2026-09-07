using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class FxRateRepository : IFxRateRepository
{
    private readonly AppDbContext _context;

    public FxRateRepository(AppDbContext context) => _context = context;

    public Task<FxRate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.FxRates
            .Include(r => r.FromCurrency)
            .Include(r => r.ToCurrency)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    // الأحدث أولاً: سعر اليوم هو المطلوب عملياً لا أقدم سعر مسجَّل
    public Task<List<FxRate>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default) =>
        _context.FxRates
            .AsNoTracking()
            .Include(r => r.FromCurrency)
            .Include(r => r.ToCurrency)
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.Id)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.FxRates.AsNoTracking().CountAsync(ct);

    public Task<bool> ScopeExistsAsync(
        Guid fromCurrencyId, Guid toCurrencyId, DateOnly rateDate, FxRateSource source,
        CancellationToken ct = default) =>
        _context.FxRates
            .AsNoTracking()
            .AnyAsync(r => r.FromCurrencyId == fromCurrencyId
                           && r.ToCurrencyId == toCurrencyId
                           && r.RateDate == rateDate
                           && r.RateSource == source, ct);

    public async Task AddAsync(FxRate entity, CancellationToken ct = default) =>
        await _context.FxRates.AddAsync(entity, ct);
}
