using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.FxRates;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using ErpApi.Services.Identity;

namespace ErpApi.Services.MasterData;

public class FxRateService : IFxRateService
{
    private readonly IFxRateRepository _fxRateRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public FxRateService(
        IFxRateRepository fxRateRepository,
        ICurrencyRepository currencyRepository,
        IUserService userService,
        IUnitOfWork unitOfWork)
    {
        _fxRateRepository = fxRateRepository;
        _currencyRepository = currencyRepository;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResponse<FxRateResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var items = await _fxRateRepository.GetPagedAsync(pagination, ct);

        return new PagedResponse<FxRateResponseDto>
        {
            Data = [.. items.Select(Map)],
            TotalCount = await _fxRateRepository.CountAsync(ct),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    public async Task<FxRateResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var rate = await _fxRateRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("سعر الصرف غير موجود.");

        return Map(rate);
    }

    public async Task<FxRateResponseDto> CreateAsync(
        FxRateCreateDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.FxRateManage, ct);

        // R-FX-01/R-FX-02: السعر يجيب سؤالاً بين عملتين مختلفتين.
        // من عملة إلى نفسها ليس سعراً بل واحد، ولا معنى لتخزينه
        if (request.FromCurrencyId == request.ToCurrencyId)
        {
            throw new BusinessRuleException("سعر الصرف يكون بين عملتين مختلفتين.");
        }

        if (request.Rate <= 0m)
        {
            throw new BusinessRuleException("سعر الصرف يجب أن يكون أكبر من صفر.");
        }

        var from = await _currencyRepository.GetByIdAsync(request.FromCurrencyId, ct)
            ?? throw new BusinessRuleException("العملة المصدر غير موجودة.");

        var to = await _currencyRepository.GetByIdAsync(request.ToCurrencyId, ct)
            ?? throw new BusinessRuleException("العملة الهدف غير موجودة.");

        // سعران لنفس الزوج والتاريخ والمصدر يجعلان سؤال «بأي سعر؟» بلا جواب
        if (await _fxRateRepository.ScopeExistsAsync(
                request.FromCurrencyId, request.ToCurrencyId, request.RateDate, request.RateSource, ct))
        {
            throw new ConflictException("يوجد سعر مسجَّل لنفس الزوج والتاريخ والمصدر.");
        }

        var rate = new FxRate
        {
            FromCurrencyId = from.Id,
            ToCurrencyId = to.Id,
            Rate = request.Rate,
            RateDate = request.RateDate,
            RateSource = request.RateSource,
            CreatedByUserId = (await _userService.GetValidatedContextAsync(ct)).User.Id,
            FromCurrency = from,
            ToCurrency = to
        };

        await _fxRateRepository.AddAsync(rate, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Map(rate);
    }

    private static FxRateResponseDto Map(FxRate rate) => new()
    {
        Id = rate.Id,
        FromCurrencyId = rate.FromCurrencyId,
        FromCurrencyCode = rate.FromCurrency?.Code ?? string.Empty,
        ToCurrencyId = rate.ToCurrencyId,
        ToCurrencyCode = rate.ToCurrency?.Code ?? string.Empty,
        Rate = rate.Rate,
        RateDate = rate.RateDate,
        RateSource = rate.RateSource
    };
}
