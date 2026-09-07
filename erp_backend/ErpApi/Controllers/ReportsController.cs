using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Services.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

// ميزان المراجعة تقرير قراءة يُنفَّذ باستعلام مباشر خارج EF (بند 10.1)، فلا يحرسه
// أي Query Filter. الفحص الصريح في الخدمة هو حارسه الوحيد — وهو ما يفرضه G06.
// أرقام التقرير نفسها تُبنى في §6.3
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IUserService _userService;

    public ReportsController(IUserService userService) => _userService = userService;

    [HttpGet("trial-balance")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<object>>>> TrialBalance(
        [FromQuery] Guid branchId, CancellationToken ct)
    {
        await _userService.EnsurePermissionAsync(Permissions.TrialBalanceRead, ct);
        await _userService.EnsureBranchAccessAsync(branchId, ct);

        return Ok(ApiResponse<IReadOnlyList<object>>.Ok([]));
    }
}
