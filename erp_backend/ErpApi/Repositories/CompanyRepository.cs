using ErpApi.Common;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _context;

    public CompanyRepository(AppDbContext context) => _context = context;

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<Company>> GetPagedAsync(Guid companyId, PaginationParams pagination, CancellationToken ct = default) =>
        Filter(companyId)
            .OrderBy(c => c.Code)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(Guid companyId, CancellationToken ct = default) =>
        Filter(companyId).CountAsync(ct);

    // ‏الشركة بحدّ شركتها **لنفسها**: `Id` هو «معرّف شركتها»، فالآلية آلية الكيانات الأخرى
    // لا دالة هوية منفصلة (قرار 2026-09-28). ومصدر واحد للقائمة والعدّاد (`G24`)
    private IQueryable<Company> Filter(Guid companyId) =>
        _context.Companies.AsNoTracking().Where(c => c.Id == companyId);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        _context.Companies.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    public async Task AddAsync(Company entity, CancellationToken ct = default) =>
        await _context.Companies.AddAsync(entity, ct);

    public void Update(Company entity) => _context.Companies.Update(entity);
}
