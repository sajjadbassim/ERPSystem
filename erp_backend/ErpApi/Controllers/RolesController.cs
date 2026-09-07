using ErpApi.Common;
using ErpApi.Core.DTO.Roles;
using ErpApi.Services.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _roleService.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "تم حذف الدور."));
    }

    [HttpPost("{id:guid}/rename")]
    public async Task<ActionResult<ApiResponse<RoleResponseDto>>> Rename(
        Guid id, RenameRoleRequestDto request, CancellationToken ct)
    {
        var role = await _roleService.RenameAsync(id, request, ct);
        return Ok(ApiResponse<RoleResponseDto>.Ok(role));
    }
}
