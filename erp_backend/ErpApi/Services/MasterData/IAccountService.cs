using ErpApi.Common;
using ErpApi.Core.DTO.Accounts;

namespace ErpApi.Services.MasterData;

public interface IAccountService
{
    Task<PagedResponse<AccountResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default);

    Task<AccountResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<AccountResponseDto> CreateAsync(AccountCreateDto request, CancellationToken ct = default);

    // R-LIFE-06: يُرفض تعطيل حساب له سطر مالي مرحَّل
    Task<AccountResponseDto> DeactivateAsync(Guid id, CancellationToken ct = default);
}
