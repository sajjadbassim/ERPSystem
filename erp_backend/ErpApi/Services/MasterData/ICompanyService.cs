using ErpApi.Common;
using ErpApi.Core.DTO.Companies;

namespace ErpApi.Services.MasterData;

public interface ICompanyService
{
    Task<PagedResponse<CompanyResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default);

    Task<CompanyResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    // يشتق FxRoundingToleranceBase من دقة عملة الأساس (R-AMT-07-a)
    Task<CompanyResponseDto> CreateAsync(CompanyCreateDto request, CancellationToken ct = default);
}
