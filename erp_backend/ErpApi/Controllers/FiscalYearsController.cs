using ErpApi.Common;
using ErpApi.Core.DTO.FiscalCalendar;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/fiscal-years")]
public class FiscalYearsController : ControllerBase
{
    private readonly IFiscalCalendarService _calendarService;

    public FiscalYearsController(IFiscalCalendarService calendarService) =>
        _calendarService = calendarService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<FiscalYearResponseDto>>>> GetPaged(
        [FromQuery] PaginationParams pagination, CancellationToken ct)
    {
        var page = await _calendarService.GetYearsPagedAsync(pagination, ct);
        return Ok(ApiResponse<PagedResponse<FiscalYearResponseDto>>.Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<FiscalYearResponseDto>>> GetById(
        Guid id, CancellationToken ct)
    {
        var year = await _calendarService.GetYearByIdAsync(id, ct);
        return Ok(ApiResponse<FiscalYearResponseDto>.Ok(year));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FiscalYearResponseDto>>> Create(
        FiscalYearCreateDto request, CancellationToken ct)
    {
        var created = await _calendarService.CreateYearAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<FiscalYearResponseDto>.Ok(created, "تم إنشاء السنة المالية بنجاح"));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ApiResponse<FiscalYearResponseDto>>> Close(
        Guid id, CancellationToken ct)
    {
        var year = await _calendarService.CloseYearAsync(id, ct);
        return Ok(ApiResponse<FiscalYearResponseDto>.Ok(year, "تم إقفال السنة المالية"));
    }
}
