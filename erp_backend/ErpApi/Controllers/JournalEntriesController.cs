using ErpApi.Common;
using ErpApi.Core.DTO.JournalEntries;
using ErpApi.Services.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

// كل فحص تخويل ونطاق فرع داخل الخدمة (بند 13.1). الكنترولر يستدعي ويغلّف فقط
[ApiController]
[Route("api/journal-entries")]
public class JournalEntriesController : ControllerBase
{
    private readonly IJournalEntryService _journalEntryService;

    public JournalEntriesController(IJournalEntryService journalEntryService) =>
        _journalEntryService = journalEntryService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<JournalEntryListItemDto>>>> GetPaged(
        [FromQuery] Guid? branchId,
        [FromQuery] PaginationParams pagination,
        CancellationToken ct)
    {
        var page = await _journalEntryService.GetPagedAsync(branchId, pagination, ct);
        return Ok(ApiResponse<PagedResponse<JournalEntryListItemDto>>.Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<JournalEntryResponseDto>>> GetById(
        Guid id, CancellationToken ct)
    {
        var entry = await _journalEntryService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<JournalEntryResponseDto>.Ok(entry));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<JournalEntryResponseDto>>> Post(
        PostJournalEntryRequestDto request, CancellationToken ct)
    {
        var posted = await _journalEntryService.PostAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = posted.Id },
            ApiResponse<JournalEntryResponseDto>.Ok(posted, "تم ترحيل القيد بنجاح"));
    }

    [HttpPost("{id:guid}/reverse")]
    public async Task<ActionResult<ApiResponse<JournalEntryResponseDto>>> Reverse(
        Guid id, ReverseJournalEntryRequestDto request, CancellationToken ct)
    {
        var reversal = await _journalEntryService.ReverseAsync(id, request, ct);

        return CreatedAtAction(nameof(GetById), new { id = reversal.Id },
            ApiResponse<JournalEntryResponseDto>.Ok(reversal, "تم عكس القيد بنجاح"));
    }
}
