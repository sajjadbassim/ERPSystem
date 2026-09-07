using ErpApi.Common;
using ErpApi.Core.DTO.Auth;
using ErpApi.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<TokenPairDto>>> Login(
        LoginRequestDto request, CancellationToken ct)
    {
        var pair = await _authService.LoginAsync(request, ct);
        return Ok(ApiResponse<TokenPairDto>.Ok(pair));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<TokenPairDto>>> Refresh(
        RefreshRequestDto request, CancellationToken ct)
    {
        var pair = await _authService.RefreshAsync(request, ct);
        return Ok(ApiResponse<TokenPairDto>.Ok(pair));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        ChangePasswordRequestDto request, CancellationToken ct)
    {
        await _authService.ChangePasswordAsync(request, ct);
        return Ok(ApiResponse<object>.Ok(null!, "تم تغيير كلمة المرور. الرجاء تسجيل الدخول من جديد."));
    }
}
