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

    public Task<List<Company>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default) =>
        _context.Companies
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Companies.AsNoTracking().CountAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        _context.Companies.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    public async Task AddAsync(Company entity, CancellationToken ct = default) =>
        await _context.Companies.AddAsync(entity, ct);

    public void Update(Company entity) => _context.Companies.Update(entity);
}
