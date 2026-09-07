using ErpApi.Common;
using ErpApi.Core.DTO.Companies;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService) => _companyService = companyService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<CompanyResponseDto>>>> GetPaged(
        [FromQuery] PaginationParams pagination, CancellationToken ct)
    {
        var page = await _companyService.GetPagedAsync(pagination, ct);
        return Ok(ApiResponse<PagedResponse<CompanyResponseDto>>.Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CompanyResponseDto>>> GetById(Guid id, CancellationToken ct)
    {
        var company = await _companyService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<CompanyResponseDto>.Ok(company));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CompanyResponseDto>>> Create(
        CompanyCreateDto request, CancellationToken ct)
    {
        var created = await _companyService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<CompanyResponseDto>.Ok(created, "تم إنشاء الشركة بنجاح"));
    }
}
