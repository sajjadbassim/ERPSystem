using ErpApi.Core.DTO.Reports;

namespace ErpApi.Services.Reports;

public interface ITrialBalanceService
{
    Task<TrialBalanceResponseDto> GetAsync(Guid branchId, CancellationToken ct = default);
}
