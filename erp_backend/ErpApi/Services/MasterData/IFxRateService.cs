using ErpApi.Common;
using ErpApi.Core.DTO.FxRates;

namespace ErpApi.Services.MasterData;

public interface IFxRateService
{
    Task<PagedResponse<FxRateResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default);

    Task<FxRateResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<FxRateResponseDto> CreateAsync(FxRateCreateDto request, CancellationToken ct = default);
}
