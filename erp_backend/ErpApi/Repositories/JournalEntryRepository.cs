using ErpApi.Common;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class JournalEntryRepository : IJournalEntryRepository
{
    private readonly AppDbContext _context;

    public JournalEntryRepository(AppDbContext context) => _context = context;

    // AsNoTracking في كل استعلام: الكيان للقراءة فقط، وتعقّبه يفتح باب تعديل يُحفظ سهواً
    public Task<JournalEntry?> GetByIdWithLinesAsync(Guid id, CancellationToken ct = default) =>
        _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines.OrderBy(l => l.LineNumber))
                .ThenInclude(l => l.Account)
            .Include(e => e.Lines)
                .ThenInclude(l => l.Currency)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<JournalEntry>> GetPagedByBranchAsync(
        Guid? branchId, PaginationParams pagination, CancellationToken ct = default) =>
        Filter(branchId)
            .OrderByDescending(e => e.PostingDate)
            .ThenBy(e => e.Id)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountByBranchAsync(Guid? branchId, CancellationToken ct = default) =>
        Filter(branchId).CountAsync(ct);

    public Task<int> CountLinesAsync(Guid journalEntryId, CancellationToken ct = default) =>
        _context.JournalLines.AsNoTracking().CountAsync(l => l.JournalEntryId == journalEntryId, ct);

    private IQueryable<JournalEntry> Filter(Guid? branchId)
    {
        var query = _context.JournalEntries.AsNoTracking();

        return branchId is { } id ? query.Where(e => e.BranchId == id) : query;
    }
}
