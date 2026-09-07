using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Accounts;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using ErpApi.Services.Identity;

namespace ErpApi.Services.MasterData;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public AccountService(
        IAccountRepository accountRepository,
        ICompanyRepository companyRepository,
        IUserService userService,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository;
        _companyRepository = companyRepository;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResponse<AccountResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var items = await _accountRepository.GetPagedAsync(pagination, ct);

        return new PagedResponse<AccountResponseDto>
        {
            Data = [.. items.Select(Map)],
            TotalCount = await _accountRepository.CountAsync(ct),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    public async Task<AccountResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var account = await _accountRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("الحساب غير موجود.");

        return Map(account);
    }

    public async Task<AccountResponseDto> CreateAsync(
        AccountCreateDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.AccountManage, ct);

        _ = await _companyRepository.GetByIdAsync(request.CompanyId, ct)
            ?? throw new BusinessRuleException("الشركة المحددة غير موجودة.");

        var code = request.Code.Trim();

        if (await _accountRepository.CodeExistsInCompanyAsync(request.CompanyId, code, ct))
        {
            throw new ConflictException($"رمز الحساب {code} مستعمل في هذه الشركة.");
        }

        // الأصول والمصروفات طبيعتها مدينة، والخصوم وحقوق الملكية والإيرادات دائنة.
        // الفحص هنا يسبق CK_Account_NormalBalanceMatchesType ليُنتج رسالة مفهومة
        // بدل رسالة قيد قاعدة بيانات
        if (!IsNormalBalanceConsistent(request.AccountType, request.NormalBalance))
        {
            throw new BusinessRuleException(
                "الطبيعة المحاسبية تناقض نوع الحساب: الأصول والمصروفات مدينة، وما عداها دائن.");
        }

        // حساب دور نظامي غير قابل للترحيل يعطّل كل قيد يظهر فيه باقي تقريب
        if (request.SystemAccountRole is not null && !request.IsPostable)
        {
            throw new BusinessRuleException("حساب الدور النظامي يجب أن يكون قابلاً للترحيل.");
        }

        var account = new Account
        {
            CompanyId = request.CompanyId,
            Code = code,
            Name = request.Name.Trim(),
            AccountType = request.AccountType,
            NormalBalance = request.NormalBalance,
            IsPostable = request.IsPostable,
            ParentAccountId = request.ParentAccountId,
            CurrencyId = request.CurrencyId,
            SystemAccountRole = request.SystemAccountRole,
            CreatedByUserId = (await _userService.GetValidatedContextAsync(ct)).User.Id
        };

        await _accountRepository.AddAsync(account, ct);

        // الأب من شركة أخرى والدورة في الشجرة يلتقطهما TR_Account_ValidateConfiguration (51007)
        // ويترجمهما SqlErrorTranslator إلى 400 برسالته العربية
        await _unitOfWork.SaveChangesAsync(ct);

        return Map(account);
    }

    public async Task<AccountResponseDto> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.AccountManage, ct);

        var account = await _accountRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("الحساب غير موجود.");

        // R-LIFE-06 والمخاطرة 32-a: JournalLine بلا Query Filter وعلاقته بالحساب إلزامية.
        // تعطيل حساب له حركة يجعل EF يولّد INNER JOIN بمرشِّح المبدأ عند التنقّل،
        // فتختفي سطوره من كل استعلام دفتري — ميزان متوازن ينقصه حساب دون أن يُعلن ذلك
        if (await _accountRepository.HasJournalLinesAsync(id, ct))
        {
            throw new ConflictException(
                "لا يمكن تعطيل حساب له حركة في الدفاتر. الحساب يبقى مرئياً ما دامت له سطور مرحَّلة.");
        }

        account.IsActive = false;
        _accountRepository.Update(account);

        await _unitOfWork.SaveChangesAsync(ct);

        return Map(account);
    }

    private static bool IsNormalBalanceConsistent(AccountType type, NormalBalance balance) =>
        type is AccountType.Asset or AccountType.Expense
            ? balance == NormalBalance.Debit
            : balance == NormalBalance.Credit;

    private static AccountResponseDto Map(Account account) => new()
    {
        Id = account.Id,
        CompanyId = account.CompanyId,
        Code = account.Code,
        Name = account.Name,
        AccountType = account.AccountType,
        NormalBalance = account.NormalBalance,
        IsPostable = account.IsPostable,
        ParentAccountId = account.ParentAccountId,
        CurrencyId = account.CurrencyId,
        SystemAccountRole = account.SystemAccountRole,
        IsActive = account.IsActive
    };
}
