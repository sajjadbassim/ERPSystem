using ErpApi.Core.Models;

namespace ErpApi.Repositories;

// قراءة فقط بنيوياً كنظيره. الحارس L21
public interface IJournalLineRepository
{
    Task<List<JournalLine>> GetByEntryAsync(Guid journalEntryId, CancellationToken ct = default);

    Task<List<Guid>> GetLineIdsAsync(Guid journalEntryId, CancellationToken ct = default);
}
