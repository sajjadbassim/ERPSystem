using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

// **قراءة فقط بنيوياً.** لا Add ولا Update ولا Remove — ولا يُضاف أيّها أبداً.
// الكتابة عبر IJournalPostingGateway حصراً (R-GL-05). الحارس L20 يفرض ذلك بالانعكاس:
// غياب الميثود يمنع الالتفاف وقت الترجمة، لا وقت التشغيل بخطأ صلاحية غامض
public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdWithLinesAsync(Guid id, CancellationToken ct = default);

    // ‏`scopedCompanyId` هو نطاق «كل الفروع» حين لا فرع محدَّد. ليس اختيارياً بالمعنى
    // المنطقي رغم قابليته للعدم: مسار `branchId` لا يحتاجه (حدّ الشركة مفروض في
    // ‏`EnsureBranchAccessAsync`)، ومسار غيابه لا يقوم بدونه (الحارس G10)
    Task<List<JournalEntry>> GetPagedByBranchAsync(
        Guid? branchId, Guid? scopedCompanyId, PaginationParams pagination,
        CancellationToken ct = default);

    Task<int> CountByBranchAsync(
        Guid? branchId, Guid? scopedCompanyId, CancellationToken ct = default);

    Task<int> CountLinesAsync(Guid journalEntryId, CancellationToken ct = default);
}
