using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Branches;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using ErpApi.Services.Identity;

namespace ErpApi.Services.MasterData;

public class BranchService : IBranchService
{
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly INumberSequenceRepository _numberSequenceRepository;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public BranchService(
        IBranchRepository branchRepository,
        ICompanyRepository companyRepository,
        INumberSequenceRepository numberSequenceRepository,
        IUserService userService,
        IUnitOfWork unitOfWork)
    {
        _branchRepository = branchRepository;
        _companyRepository = companyRepository;
        _numberSequenceRepository = numberSequenceRepository;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResponse<BranchResponseDto>> GetPagedAsync(
        PaginationParams pagination, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var items = await _branchRepository.GetPagedAsync(pagination, ct);

        return new PagedResponse<BranchResponseDto>
        {
            Data = [.. items.Select(Map)],
            TotalCount = await _branchRepository.CountAsync(ct),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    public async Task<BranchResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.MasterDataRead, ct);

        var branch = await _branchRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("الفرع غير موجود.");

        return Map(branch);
    }

    public async Task<BranchResponseDto> CreateAsync(
        BranchCreateDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.BranchManage, ct);

        _ = await _companyRepository.GetByIdAsync(request.CompanyId, ct)
            ?? throw new BusinessRuleException("الشركة المحددة غير موجودة.");

        var code = request.Code.Trim().ToUpperInvariant();

        // التفرّد داخل الشركة لا في النظام: نفس الرمز مقبول في شركة أخرى (UQ_Branch_CompanyId_Code)
        if (await _branchRepository.CodeExistsInCompanyAsync(request.CompanyId, code, ct))
        {
            throw new ConflictException($"رمز الفرع {code} مستعمل في هذه الشركة.");
        }

        var actor = (await _userService.GetValidatedContextAsync(ct)).User.Id;

        var branch = new Branch
        {
            CompanyId = request.CompanyId,
            Code = code,
            Name = request.Name.Trim(),
            CreatedByUserId = actor
        };

        await _branchRepository.AddAsync(branch, ct);

        // §4.3: التهيئة عند إنشاء الفرع لا عند أول ترحيل. الفرق ليس تجميلياً —
        // التهيئة الكسولة تجعل أول ترحيل في فرع جديد يُنشئ العدّاد داخل معاملته،
        // فيتسابق ترحيلان متزامنان على إنشائه، والنتيجة فجوة في تسلسل مستندات
        // محاسبية وهو ما يمنعه R-NUM-01 من أصله.
        // وتقع في نفس SaveChanges: فرع بلا عدّاده حالة وسيطة لا يجوز أن تُرى
        await _numberSequenceRepository.AddAsync(new NumberSequence
        {
            CompanyId = request.CompanyId,
            BranchId = branch.Id,
            DocumentType = SequenceDocumentType.JournalEntry,

            // ترقيم القيد على مستوى الفرع لا السنة: FiscalYearId يبقى فارغاً،
            // ويحرس CK_NumberSequence_JournalScope أن BranchId إلزامي للنوع 1
            FiscalYearId = null,

            Prefix = "JV-",
            CurrentValue = 0,
            PaddingLength = 6,
            CreatedByUserId = actor
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return Map(branch);
    }

    private static BranchResponseDto Map(Branch branch) => new()
    {
        Id = branch.Id,
        CompanyId = branch.CompanyId,
        Code = branch.Code,
        Name = branch.Name,
        IsActive = branch.IsActive
    };
}
