using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.FiscalCalendar;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using ErpApi.Services.Identity;

namespace ErpApi.Services.MasterData;

public class FiscalCalendarService : IFiscalCalendarService
{
    private readonly IFiscalCalendarRepository _calendarRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public FiscalCalendarService(
        IFiscalCalendarRepository calendarRepository,
        ICompanyRepository companyRepository,
        IUserService userService,
        IUnitOfWork unitOfWork)
    {
        _calendarRepository = calendarRepository;
        _companyRepository = companyRepository;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResponse<FiscalYearResponseDto>> GetYearsPagedAsync(
        PaginationParams pagination, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var items = await _calendarRepository.GetYearsPagedAsync(pagination, ct);

        return new PagedResponse<FiscalYearResponseDto>
        {
            Data = [.. items.Select(MapYear)],
            TotalCount = await _calendarRepository.CountYearsAsync(ct),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    public async Task<FiscalYearResponseDto> GetYearByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        return MapYear(await LoadYearAsync(id, ct));
    }

    public async Task<FiscalYearResponseDto> CreateYearAsync(
        FiscalYearCreateDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.FiscalYearManage, ct);

        _ = await _companyRepository.GetByIdAsync(request.CompanyId, ct)
            ?? throw new BusinessRuleException("الشركة المحددة غير موجودة.");

        if (request.EndDate <= request.StartDate)
        {
            throw new BusinessRuleException("تاريخ نهاية السنة يجب أن يلي تاريخ بدايتها.");
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _calendarRepository.YearCodeExistsAsync(request.CompanyId, code, ct))
        {
            throw new ConflictException($"رمز السنة {code} مستعمل في هذه الشركة.");
        }

        var year = new FiscalYear
        {
            CompanyId = request.CompanyId,
            Code = code,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedByUserId = (await _userService.GetValidatedContextAsync(ct)).User.Id
        };

        await _calendarRepository.AddYearAsync(year, ct);

        // التداخل يلتقطه TR_FiscalYear_PreventOverlap (51008) ويترجمه المترجم إلى 409
        await _unitOfWork.SaveChangesAsync(ct);

        return MapYear(year);
    }

    public async Task<FiscalYearResponseDto> CloseYearAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.FiscalPeriodClose, ct);

        var year = await LoadYearAsync(id, ct);

        // الفحص هنا يسبق TR_FiscalYear_PreventCloseWithOpenPeriods (51009) ليُنتج رسالة
        // تقول ما ينقص. التريجر يبقى الحارس الأخير على الكتابة المباشرة
        if (await _calendarRepository.HasOpenPeriodsAsync(id, ct))
        {
            throw new ConflictException("لا تُقفل السنة وفيها فترة مفتوحة. أقفل الفترات أولاً.");
        }

        year.IsClosed = true;
        _calendarRepository.UpdateYear(year);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapYear(year);
    }

    public async Task<FiscalPeriodResponseDto> GetPeriodByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        return MapPeriod(await LoadPeriodAsync(id, ct));
    }

    public async Task<FiscalPeriodResponseDto> CreatePeriodAsync(
        FiscalPeriodCreateDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.FiscalYearManage, ct);

        var year = await LoadYearAsync(request.FiscalYearId, ct);

        if (request.EndDate < request.StartDate)
        {
            throw new BusinessRuleException("تاريخ نهاية الفترة يسبق تاريخ بدايتها.");
        }

        // الخروج عن مدى السنة خطأ مدخلات لا تعارض حالة: الفترة تُنشأ داخل سنتها أو لا تُنشأ
        if (request.StartDate < year.StartDate || request.EndDate > year.EndDate)
        {
            throw new BusinessRuleException("الفترة تخرج عن مدى سنتها المالية.");
        }

        var period = new FiscalPeriod
        {
            FiscalYearId = request.FiscalYearId,
            PeriodNumber = request.PeriodNumber,
            PeriodType = request.PeriodType,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedByUserId = (await _userService.GetValidatedContextAsync(ct)).User.Id
        };

        await _calendarRepository.AddPeriodAsync(period, ct);

        // التداخل بين فترتين عاديتين يلتقطه TR_FiscalPeriod_PreventOverlap (51011) ⟶ 409
        await _unitOfWork.SaveChangesAsync(ct);

        return MapPeriod(period);
    }

    public async Task<FiscalPeriodResponseDto> ClosePeriodAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.FiscalPeriodClose, ct);

        var period = await LoadPeriodAsync(id, ct);

        period.IsClosed = true;
        _calendarRepository.UpdatePeriod(period);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapPeriod(period);
    }

    private async Task<FiscalYear> LoadYearAsync(Guid id, CancellationToken ct) =>
        await _calendarRepository.GetYearAsync(id, ct)
        ?? throw new NotFoundException("السنة المالية غير موجودة.");

    private async Task<FiscalPeriod> LoadPeriodAsync(Guid id, CancellationToken ct) =>
        await _calendarRepository.GetPeriodAsync(id, ct)
        ?? throw new NotFoundException("الفترة المالية غير موجودة.");

    private static FiscalYearResponseDto MapYear(FiscalYear year) => new()
    {
        Id = year.Id,
        CompanyId = year.CompanyId,
        Code = year.Code,
        StartDate = year.StartDate,
        EndDate = year.EndDate,
        IsClosed = year.IsClosed
    };

    private static FiscalPeriodResponseDto MapPeriod(FiscalPeriod period) => new()
    {
        Id = period.Id,
        FiscalYearId = period.FiscalYearId,
        PeriodNumber = period.PeriodNumber,
        PeriodType = period.PeriodType,
        Name = period.Name,
        StartDate = period.StartDate,
        EndDate = period.EndDate,
        IsClosed = period.IsClosed
    };
}
