using ErpApi.Core.DTO.Auth;

namespace ErpApi.Services.Identity;

public interface IAuthService
{
    Task<TokenPairDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default);

    Task<TokenPairDto> RefreshAsync(RefreshRequestDto request, CancellationToken ct = default);

    Task ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken ct = default);
}
