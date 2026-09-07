using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

// **قراءة فقط بنيوياً.** لا Add ولا Update ولا Remove — ولا يُضاف أيّها أبداً.
// الكتابة عبر IJournalPostingGateway حصراً (R-GL-05). الحارس L20 يفرض ذلك بالانعكاس:
// غياب الميثود يمنع الالتفاف وقت الترجمة، لا وقت التشغيل بخطأ صلاحية غامض
public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdWithLinesAsync(Guid id, CancellationToken ct = default);

    Task<List<JournalEntry>> GetPagedByBranchAsync(
        Guid? branchId, PaginationParams pagination, CancellationToken ct = default);

    Task<int> CountByBranchAsync(Guid? branchId, CancellationToken ct = default);

    Task<int> CountLinesAsync(Guid journalEntryId, CancellationToken ct = default);
}
