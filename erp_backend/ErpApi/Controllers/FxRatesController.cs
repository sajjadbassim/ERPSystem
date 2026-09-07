using ErpApi.Common;
using ErpApi.Core.DTO.FxRates;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/fx-rates")]
public class FxRatesController : ControllerBase
{
    private readonly IFxRateService _fxRateService;

    public FxRatesController(IFxRateService fxRateService) => _fxRateService = fxRateService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<FxRateResponseDto>>>> GetPaged(
        [FromQuery] PaginationParams pagination, CancellationToken ct)
    {
        var page = await _fxRateService.GetPagedAsync(pagination, ct);
        return Ok(ApiResponse<PagedResponse<FxRateResponseDto>>.Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<FxRateResponseDto>>> GetById(Guid id, CancellationToken ct)
    {
        var rate = await _fxRateService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<FxRateResponseDto>.Ok(rate));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FxRateResponseDto>>> Create(
        FxRateCreateDto request, CancellationToken ct)
    {
        var created = await _fxRateService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<FxRateResponseDto>.Ok(created, "تم تسجيل سعر الصرف بنجاح"));
    }
}
