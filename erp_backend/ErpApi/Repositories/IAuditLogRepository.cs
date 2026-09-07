using ErpApi.Core.Models;

namespace ErpApi.Repositories;

// الكتابة فقط. لا قراءة من منطق العمل: السجل يُستجوَب بتقرير في §6.3، لا بفرع في كود
public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct = default);
}
