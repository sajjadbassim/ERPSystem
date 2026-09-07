using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class JournalLineRepository : IJournalLineRepository
{
    private readonly AppDbContext _context;

    public JournalLineRepository(AppDbContext context) => _context = context;

    public Task<List<JournalLine>> GetByEntryAsync(Guid journalEntryId, CancellationToken ct = default) =>
        _context.JournalLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.Currency)
            .Where(l => l.JournalEntryId == journalEntryId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

    public Task<List<Guid>> GetLineIdsAsync(Guid journalEntryId, CancellationToken ct = default) =>
        _context.JournalLines
            .AsNoTracking()
            .Where(l => l.JournalEntryId == journalEntryId)
            .OrderBy(l => l.LineNumber)
            .Select(l => l.Id)
            .ToListAsync(ct);
}
