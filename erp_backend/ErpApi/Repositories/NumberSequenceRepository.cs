using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class NumberSequenceRepository : INumberSequenceRepository
{
    private readonly AppDbContext _context;

    public NumberSequenceRepository(AppDbContext context) => _context = context;

    public Task<bool> ExistsForBranchAsync(
        Guid branchId, SequenceDocumentType documentType, CancellationToken ct = default) =>
        _context.NumberSequences
            .AsNoTracking()
            .AnyAsync(s => s.BranchId == branchId && s.DocumentType == documentType, ct);

    public Task<List<NumberSequence>> GetByBranchAsync(Guid branchId, CancellationToken ct = default) =>
        _context.NumberSequences
            .AsNoTracking()
            .Where(s => s.BranchId == branchId)
            .OrderBy(s => s.DocumentType)
            .ToListAsync(ct);

    public async Task AddAsync(NumberSequence entity, CancellationToken ct = default) =>
        await _context.NumberSequences.AddAsync(entity, ct);
}
