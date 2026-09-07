using ErpApi.Common;
using ErpApi.Core.DTO.Accounts;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService) => _accountService = accountService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<AccountResponseDto>>>> GetPaged(
        [FromQuery] PaginationParams pagination, CancellationToken ct)
    {
        var page = await _accountService.GetPagedAsync(pagination, ct);
        return Ok(ApiResponse<PagedResponse<AccountResponseDto>>.Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> GetById(Guid id, CancellationToken ct)
    {
        var account = await _accountService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<AccountResponseDto>.Ok(account));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> Create(
        AccountCreateDto request, CancellationToken ct)
    {
        var created = await _accountService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<AccountResponseDto>.Ok(created, "تم إنشاء الحساب بنجاح"));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ApiResponse<AccountResponseDto>>> Deactivate(
        Guid id, CancellationToken ct)
    {
        var account = await _accountService.DeactivateAsync(id, ct);
        return Ok(ApiResponse<AccountResponseDto>.Ok(account, "تم تعطيل الحساب"));
    }
}
