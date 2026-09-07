using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Currencies;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using ErpApi.Services.Identity;

namespace ErpApi.Services.MasterData;

public class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public CurrencyService(
        ICurrencyRepository currencyRepository, IUserService userService, IUnitOfWork unitOfWork)
    {
        _currencyRepository = currencyRepository;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResponse<CurrencyResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var items = await _currencyRepository.GetPagedAsync(pagination, ct);

        return new PagedResponse<CurrencyResponseDto>
        {
            Data = [.. items.Select(Map)],
            TotalCount = await _currencyRepository.CountAsync(ct),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    public async Task<CurrencyResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var currency = await _currencyRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("العملة غير موجودة.");

        return Map(currency);
    }

    public async Task<CurrencyResponseDto> CreateAsync(
        CurrencyCreateDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.CurrencyManage, ct);

        var code = request.Code.Trim().ToUpperInvariant();

        // تعارض حالة لا خطأ مدخلات، فهو 409 (بند 8.1)
        if (await _currencyRepository.CodeExistsAsync(code, ct))
        {
            throw new ConflictException($"رمز العملة {code} مستعمل بالفعل.");
        }

        var currency = new Currency
        {
            Code = code,
            Name = request.Name.Trim(),
            Symbol = request.Symbol?.Trim(),
            DecimalPlaces = request.DecimalPlaces,
            CreatedByUserId = (await _userService.GetValidatedContextAsync(ct)).User.Id
        };

        await _currencyRepository.AddAsync(currency, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Map(currency);
    }

    public async Task<CurrencyResponseDto> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.CurrencyManage, ct);

        var currency = await _currencyRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("العملة غير موجودة.");

        // R-LIFE-06 والمخاطرة 32-a: JournalLine بلا Query Filter وعلاقته بالعملة إلزامية.
        // فتعطيل عملة لها حركة يجعل EF يولّد INNER JOIN بمرشِّح المبدأ عند التنقّل،
        // فتختفي سطورها من كل استعلام دفتري — ميزان متوازن ينقصه حساب دون أن يُعلن ذلك.
        // القاعدة تُسقط المشكلة بدل إدارتها، فلا حاجة لتذكّر IgnoreQueryFilters في كل استعلام
        if (await _currencyRepository.HasJournalLinesAsync(id, ct))
        {
            throw new ConflictException(
                "لا يمكن تعطيل عملة لها حركة في الدفاتر. العملة تبقى مرئية ما دامت لها سطور مرحَّلة.");
        }

        currency.IsActive = false;
        _currencyRepository.Update(currency);

        await _unitOfWork.SaveChangesAsync(ct);

        return Map(currency);
    }

    private static CurrencyResponseDto Map(Currency currency) => new()
    {
        Id = currency.Id,
        Code = currency.Code,
        Name = currency.Name,
        Symbol = currency.Symbol,
        DecimalPlaces = currency.DecimalPlaces,
        IsActive = currency.IsActive
    };
}
