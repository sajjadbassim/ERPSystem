using ErpApi.Common;
using ErpApi.Core.DTO.Currencies;

namespace ErpApi.Services.MasterData;

public interface ICurrencyService
{
    Task<PagedResponse<CurrencyResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default);

    Task<CurrencyResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CurrencyResponseDto> CreateAsync(CurrencyCreateDto request, CancellationToken ct = default);

    // R-LIFE-06: يُرفض تعطيل عملة لها سطر مالي مرحَّل
    Task<CurrencyResponseDto> DeactivateAsync(Guid id, CancellationToken ct = default);
}
