using ErpApi.Common;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class FiscalCalendarRepository : IFiscalCalendarRepository
{
    private readonly AppDbContext _context;

    public FiscalCalendarRepository(AppDbContext context) => _context = context;

    public Task<FiscalYear?> GetYearAsync(Guid id, CancellationToken ct = default) =>
        _context.FiscalYears.FirstOrDefaultAsync(y => y.Id == id, ct);

    public Task<List<FiscalYear>> GetYearsPagedAsync(
        PaginationParams pagination, CancellationToken ct = default) =>
        _context.FiscalYears
            .AsNoTracking()
            .OrderByDescending(y => y.StartDate)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountYearsAsync(CancellationToken ct = default) =>
        _context.FiscalYears.AsNoTracking().CountAsync(ct);

    public Task<bool> YearCodeExistsAsync(Guid companyId, string code, CancellationToken ct = default) =>
        _context.FiscalYears
            .AsNoTracking()
            .AnyAsync(y => y.CompanyId == companyId && y.Code == code, ct);

    public Task<FiscalPeriod?> GetPeriodAsync(Guid id, CancellationToken ct = default) =>
        _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> HasOpenPeriodsAsync(Guid fiscalYearId, CancellationToken ct = default) =>
        _context.FiscalPeriods
            .AsNoTracking()
            .AnyAsync(p => p.FiscalYearId == fiscalYearId && !p.IsClosed, ct);

    public async Task AddYearAsync(FiscalYear entity, CancellationToken ct = default) =>
        await _context.FiscalYears.AddAsync(entity, ct);

    public async Task AddPeriodAsync(FiscalPeriod entity, CancellationToken ct = default) =>
        await _context.FiscalPeriods.AddAsync(entity, ct);

    public void UpdateYear(FiscalYear entity) => _context.FiscalYears.Update(entity);

    public void UpdatePeriod(FiscalPeriod entity) => _context.FiscalPeriods.Update(entity);
}
