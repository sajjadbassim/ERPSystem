using ErpApi.Common;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly AppDbContext _context;

    public BranchRepository(AppDbContext context) => _context = context;

    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<List<Branch>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default) =>
        _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.Code)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Branches.AsNoTracking().CountAsync(ct);

    public Task<bool> CodeExistsInCompanyAsync(
        Guid companyId, string code, CancellationToken ct = default) =>
        _context.Branches
            .AsNoTracking()
            .AnyAsync(b => b.CompanyId == companyId && b.Code == code, ct);

    public async Task AddAsync(Branch entity, CancellationToken ct = default) =>
        await _context.Branches.AddAsync(entity, ct);

    public void Update(Branch entity) => _context.Branches.Update(entity);
}
