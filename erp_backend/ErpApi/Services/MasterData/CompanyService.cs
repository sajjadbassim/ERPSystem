using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Companies;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using ErpApi.Services.Identity;

namespace ErpApi.Services.MasterData;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public CompanyService(
        ICompanyRepository companyRepository,
        ICurrencyRepository currencyRepository,
        IUserService userService,
        IUnitOfWork unitOfWork)
    {
        _companyRepository = companyRepository;
        _currencyRepository = currencyRepository;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    // ‏R-AMT-07-a: الحد = 10^(−DecimalPlaces) لعملة الأساس — أصغر وحدة قابلة للعرض.
    // باقٍ أصغر منها لا يظهر للمستخدم أصلاً، وأكبر منها رقم حقيقي يستحق تفسيراً لا ابتلاعاً.
    //
    // القسمة العشرية المتكررة لا Math.Pow: الأخيرة تمرّ بـ double فتُدخل خطأ تمثيل
    // في قيمة يُقارَن بها فرق مالي.
    internal static decimal DeriveTolerance(byte decimalPlaces)
    {
        var tolerance = 1m;

        for (var step = 0; step < decimalPlaces; step++)
        {
            tolerance /= 10m;
        }

        // ⚠ «+ 0.0000m» ليس جمعاً بل **تثبيت للمقياس العشري (Scale)**. لا يغيّر القيمة
        // ولا يمكن الاستغناء عنه: decimal في ‎.NET يحمل مقياسه معه، و1m مقياسه صفر
        // فيُنتج ToString النصّ "1"، بينما العمود (19,4) والواجهة يتوقعان "1.0000".
        // جمع صفر بأربع خانات يرفع المقياس إلى أربع بلا مساس بالقيمة.
        //
        // لا تحذفه في تنظيف لاحق بوصفه «جمعاً بلا أثر» — حذفه يكسر K10..K12
        // ويجعل حد التقريب يعبر إلى الواجهة بمقياس غير مقياس عموده
        return tolerance + 0.0000m;
    }

    public async Task<PagedResponse<CompanyResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var items = await _companyRepository.GetPagedAsync(pagination, ct);
        var mapped = new List<CompanyResponseDto>(items.Count);

        foreach (var company in items)
        {
            mapped.Add(await MapAsync(company, ct));
        }

        return new PagedResponse<CompanyResponseDto>
        {
            Data = mapped,
            TotalCount = await _companyRepository.CountAsync(ct),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    public async Task<CompanyResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var company = await _companyRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("الشركة غير موجودة.");

        return await MapAsync(company, ct);
    }

    public async Task<CompanyResponseDto> CreateAsync(
        CompanyCreateDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.CompanyManage, ct);

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _companyRepository.CodeExistsAsync(code, ct))
        {
            throw new ConflictException($"رمز الشركة {code} مستعمل بالفعل.");
        }

        var baseCurrency = await _currencyRepository.GetByIdAsync(request.BaseCurrencyId, ct)
            ?? throw new BusinessRuleException("عملة الدفاتر المحددة غير موجودة.");

        var company = new Company
        {
            Code = code,
            Name = request.Name.Trim(),
            BaseCurrencyId = baseCurrency.Id,

            // الاشتقاق يحدث مرة واحدة في عمر الشركة، لأن عملة الأساس تُقفل
            // عند أول عملية مالية (R-BASE-01) فلا تتغير الدقة بعدها
            FxRoundingToleranceBase = DeriveTolerance(baseCurrency.DecimalPlaces),

            CreatedByUserId = (await _userService.GetValidatedContextAsync(ct)).User.Id
        };

        await _companyRepository.AddAsync(company, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await MapAsync(company, ct);
    }

    private async Task<CompanyResponseDto> MapAsync(Company company, CancellationToken ct)
    {
        var baseCurrency = await _currencyRepository.GetByIdAsync(company.BaseCurrencyId, ct);

        return new CompanyResponseDto
        {
            Id = company.Id,
            Code = company.Code,
            Name = company.Name,
            BaseCurrencyId = company.BaseCurrencyId,
            BaseCurrencyCode = baseCurrency?.Code ?? string.Empty,
            FxRoundingToleranceBase = company.FxRoundingToleranceBase,
            IsBaseCurrencyLocked = company.IsBaseCurrencyLocked,
            IsActive = company.IsActive
        };
    }
}
