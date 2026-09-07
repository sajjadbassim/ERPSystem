using ErpApi.Common;
using ErpApi.Core.DTO.Branches;

namespace ErpApi.Services.MasterData;

public interface IBranchService
{
    Task<PagedResponse<BranchResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default);

    Task<BranchResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<BranchResponseDto> CreateAsync(BranchCreateDto request, CancellationToken ct = default);
}
