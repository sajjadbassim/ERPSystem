using ErpApi.Common;
using ErpApi.Core.DTO.FiscalCalendar;

namespace ErpApi.Services.MasterData;

public interface IFiscalCalendarService
{
    Task<PagedResponse<FiscalYearResponseDto>> GetYearsPagedAsync(
        PaginationParams pagination, CancellationToken ct = default);

    Task<FiscalYearResponseDto> GetYearByIdAsync(Guid id, CancellationToken ct = default);

    Task<FiscalYearResponseDto> CreateYearAsync(
        FiscalYearCreateDto request, CancellationToken ct = default);

    Task<FiscalYearResponseDto> CloseYearAsync(Guid id, CancellationToken ct = default);

    Task<FiscalPeriodResponseDto> GetPeriodByIdAsync(Guid id, CancellationToken ct = default);

    Task<FiscalPeriodResponseDto> CreatePeriodAsync(
        FiscalPeriodCreateDto request, CancellationToken ct = default);

    Task<FiscalPeriodResponseDto> ClosePeriodAsync(Guid id, CancellationToken ct = default);
}
