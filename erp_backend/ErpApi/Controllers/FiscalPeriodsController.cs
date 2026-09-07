using ErpApi.Common;
using ErpApi.Core.DTO.FiscalCalendar;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/fiscal-periods")]
public class FiscalPeriodsController : ControllerBase
{
    private readonly IFiscalCalendarService _calendarService;

    public FiscalPeriodsController(IFiscalCalendarService calendarService) =>
        _calendarService = calendarService;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<FiscalPeriodResponseDto>>> GetById(
        Guid id, CancellationToken ct)
    {
        var period = await _calendarService.GetPeriodByIdAsync(id, ct);
        return Ok(ApiResponse<FiscalPeriodResponseDto>.Ok(period));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FiscalPeriodResponseDto>>> Create(
        FiscalPeriodCreateDto request, CancellationToken ct)
    {
        var created = await _calendarService.CreatePeriodAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<FiscalPeriodResponseDto>.Ok(created, "تم إنشاء الفترة المالية بنجاح"));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ApiResponse<FiscalPeriodResponseDto>>> Close(
        Guid id, CancellationToken ct)
    {
        var period = await _calendarService.ClosePeriodAsync(id, ct);
        return Ok(ApiResponse<FiscalPeriodResponseDto>.Ok(period, "تم إقفال الفترة"));
    }
}
