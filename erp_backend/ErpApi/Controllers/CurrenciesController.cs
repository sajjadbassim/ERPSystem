using ErpApi.Common;
using ErpApi.Core.DTO.Currencies;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/currencies")]
public class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrenciesController(ICurrencyService currencyService) => _currencyService = currencyService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<CurrencyResponseDto>>>> GetPaged(
        [FromQuery] PaginationParams pagination, CancellationToken ct)
    {
        var page = await _currencyService.GetPagedAsync(pagination, ct);
        return Ok(ApiResponse<PagedResponse<CurrencyResponseDto>>.Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CurrencyResponseDto>>> GetById(Guid id, CancellationToken ct)
    {
        var currency = await _currencyService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<CurrencyResponseDto>.Ok(currency));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CurrencyResponseDto>>> Create(
        CurrencyCreateDto request, CancellationToken ct)
    {
        var created = await _currencyService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<CurrencyResponseDto>.Ok(created, "تم إنشاء العملة بنجاح"));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ApiResponse<CurrencyResponseDto>>> Deactivate(
        Guid id, CancellationToken ct)
    {
        var currency = await _currencyService.DeactivateAsync(id, ct);
        return Ok(ApiResponse<CurrencyResponseDto>.Ok(currency, "تم تعطيل العملة"));
    }
}
