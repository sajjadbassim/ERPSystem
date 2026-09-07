using ErpApi.Common;
using ErpApi.Core.DTO.JournalEntries;

namespace ErpApi.Services.Accounting;

public interface IJournalEntryService
{
    Task<PagedResponse<JournalEntryListItemDto>> GetPagedAsync(
        Guid? branchId, PaginationParams pagination, CancellationToken ct = default);

    Task<JournalEntryResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    // الكتابة عبر usp_JournalEntry_Post حصراً (R-GL-05)
    Task<JournalEntryResponseDto> PostAsync(
        PostJournalEntryRequestDto request, CancellationToken ct = default);

    Task<JournalEntryResponseDto> ReverseAsync(
        Guid originalId, ReverseJournalEntryRequestDto request, CancellationToken ct = default);
}
