using ErpApi.Common;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class CurrencyRepository : ICurrencyRepository
{
    private readonly AppDbContext _context;

    public CurrencyRepository(AppDbContext context) => _context = context;

    public Task<Currency?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Currencies.FirstOrDefaultAsync(c => c.Id == id, ct);

    // OrderBy إلزامي قبل Skip/Take وإلا فالنتائج غير مضمونة (بند 10)
    public Task<List<Currency>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default) =>
        _context.Currencies
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Currencies.AsNoTracking().CountAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        _context.Currencies.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    // JournalLines بلا Query Filter، فالفحص يرى كل السطور المرحَّلة بلا استثناء
    public Task<bool> HasJournalLinesAsync(Guid currencyId, CancellationToken ct = default) =>
        _context.JournalLines.AsNoTracking().AnyAsync(l => l.CurrencyId == currencyId, ct);

    public async Task AddAsync(Currency entity, CancellationToken ct = default) =>
        await _context.Currencies.AddAsync(entity, ct);

    public void Update(Currency entity) => _context.Currencies.Update(entity);
}
