using ErpApi.Common;
using ErpApi.Core.DTO.Reports;
using ErpApi.Services.Reports;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

// رقيق: الفحص (الصلاحية ثم نطاق الفرع) في الخدمة قبل بناء الاستعلام (بند 13.1)، لأن
// الاستعلام مباشر خارج EF (بند 10.1) فلا يحرسه أي Query Filter. G06 يفرض ذلك
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ITrialBalanceService _trialBalanceService;

    public ReportsController(ITrialBalanceService trialBalanceService) => _trialBalanceService = trialBalanceService;

    [HttpGet("trial-balance")]
    public async Task<ActionResult<ApiResponse<TrialBalanceResponseDto>>> TrialBalance(
        [FromQuery] Guid branchId, CancellationToken ct) =>
        Ok(ApiResponse<TrialBalanceResponseDto>.Ok(await _trialBalanceService.GetAsync(branchId, ct)));
}
