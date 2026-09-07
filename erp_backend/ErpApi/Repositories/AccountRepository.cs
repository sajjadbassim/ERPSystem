using ErpApi.Common;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context) => _context = context;

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<Account>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default) =>
        _context.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Code)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _context.Accounts.AsNoTracking().CountAsync(ct);

    public Task<bool> CodeExistsInCompanyAsync(
        Guid companyId, string code, CancellationToken ct = default) =>
        _context.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.CompanyId == companyId && a.Code == code, ct);

    // JournalLines بلا Query Filter، فالفحص يرى كل السطور المرحَّلة بلا استثناء
    public Task<bool> HasJournalLinesAsync(Guid accountId, CancellationToken ct = default) =>
        _context.JournalLines.AsNoTracking().AnyAsync(l => l.AccountId == accountId, ct);

    public async Task AddAsync(Account entity, CancellationToken ct = default) =>
        await _context.Accounts.AddAsync(entity, ct);

    public void Update(Account entity) => _context.Accounts.Update(entity);
}
