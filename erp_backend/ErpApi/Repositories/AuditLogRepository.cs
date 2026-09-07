using ErpApi.Core.Models;
using ErpApi.Data;

namespace ErpApi.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default) =>
        await _context.AuditLogEntries.AddAsync(entry, ct);
}
