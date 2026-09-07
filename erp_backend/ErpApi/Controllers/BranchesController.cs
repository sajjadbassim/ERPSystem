using ErpApi.Common;
using ErpApi.Core.DTO.Branches;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/branches")]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService) => _branchService = branchService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<BranchResponseDto>>>> GetPaged(
        [FromQuery] PaginationParams pagination, CancellationToken ct)
    {
        var page = await _branchService.GetPagedAsync(pagination, ct);
        return Ok(ApiResponse<PagedResponse<BranchResponseDto>>.Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BranchResponseDto>>> GetById(Guid id, CancellationToken ct)
    {
        var branch = await _branchService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<BranchResponseDto>.Ok(branch));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<BranchResponseDto>>> Create(
        BranchCreateDto request, CancellationToken ct)
    {
        var created = await _branchService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<BranchResponseDto>.Ok(created, "تم إنشاء الفرع بنجاح"));
    }
}
