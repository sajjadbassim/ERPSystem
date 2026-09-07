using ErpApi.Common;
using ErpApi.Core.Constants;
using ErpApi.Core.DTO.JournalEntries;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Models;
using ErpApi.Data;
using ErpApi.Repositories;
using ErpApi.Services.Identity;

namespace ErpApi.Services.Accounting;

public class JournalEntryService : IJournalEntryService
{
    private readonly IJournalEntryRepository _entryRepository;
    private readonly IJournalLineRepository _lineRepository;
    private readonly IJournalPostingGateway _postingGateway;
    private readonly IUserService _userService;

    public JournalEntryService(
        IJournalEntryRepository entryRepository,
        IJournalLineRepository lineRepository,
        IJournalPostingGateway postingGateway,
        IUserService userService)
    {
        _entryRepository = entryRepository;
        _lineRepository = lineRepository;
        _postingGateway = postingGateway;
        _userService = userService;
    }

    public async Task<PagedResponse<JournalEntryListItemDto>> GetPagedAsync(
        Guid? branchId, PaginationParams pagination, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.JournalEntryRead, ct);

        if (branchId is { } id)
        {
            await _userService.EnsureBranchAccessAsync(id, ct);
        }
        else
        {
            await _userService.EnsureAllBranchesScopeAsync(ct);
        }

        var items = await _entryRepository.GetPagedByBranchAsync(branchId, pagination, ct);

        return new PagedResponse<JournalEntryListItemDto>
        {
            Data = [.. items.Select(MapListItem)],
            TotalCount = await _entryRepository.CountByBranchAsync(branchId, ct),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    public async Task<JournalEntryResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.JournalEntryRead, ct);

        var entry = await _entryRepository.GetByIdWithLinesAsync(id, ct)
            ?? throw new NotFoundException("القيد غير موجود.");

        // نطاق الفرع يُفحص بعد التحميل: المعرّف وحده لا يكشف فرعه قبل قراءته
        await _userService.EnsureBranchAccessAsync(entry.BranchId, ct);

        return Map(entry, await _lineRepository.GetByEntryAsync(id, ct));
    }

    public async Task<JournalEntryResponseDto> PostAsync(
        PostJournalEntryRequestDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.JournalEntryPost, ct);
        await _userService.EnsureBranchAccessAsync(request.BranchId, ct);

        var actor = (await _userService.GetValidatedContextAsync(ct)).User.Id;

        var lines = request.Lines
            .Select((line, index) => new PostingLineInput(
                Guid.CreateVersion7(),
                (short)(index + 1),
                line.AccountId,
                line.Description,
                line.CurrencyId,
                line.ExchangeRate,
                line.ExchangeRateDate,
                line.DebitFC,
                line.CreditFC,
                line.DebitBase,
                line.CreditBase))
            .ToList();

        var result = await _postingGateway.PostAsync(
            request.BranchId, request.PostingDate, request.DocumentDate ?? request.PostingDate,
            request.Description, request.SourceModule, actor, lines, ct);

        return await LoadAfterWriteAsync(result.JournalEntryId, ct);
    }

    public async Task<JournalEntryResponseDto> ReverseAsync(
        Guid originalId, ReverseJournalEntryRequestDto request, CancellationToken ct = default)
    {
        await _userService.EnsurePermissionAsync(Permissions.JournalEntryReverse, ct);

        var original = await _entryRepository.GetByIdWithLinesAsync(originalId, ct)
            ?? throw new NotFoundException("القيد الأصلي غير موجود.");

        await _userService.EnsureBranchAccessAsync(original.BranchId, ct);

        var actor = (await _userService.GetValidatedContextAsync(ct)).User.Id;

        // الإجراء يبني سطور العكس من الأصل بنفسه (R-GL-03-a) ولا يقبلها من المستدعي.
        // ما يحتاجه منّا هو مفاتيح السطور الجديدة فحسب، وعددها يجب أن يطابق الأصل (50030)
        var originalLineCount = await _entryRepository.CountLinesAsync(originalId, ct);
        var reversalLineIds = Enumerable.Range(0, originalLineCount)
            .Select(_ => Guid.CreateVersion7())
            .ToList();

        var result = await _postingGateway.ReverseAsync(
            originalId, original.BranchId, request.PostingDate, request.DocumentDate,
            request.Description, actor, reversalLineIds, ct);

        return await LoadAfterWriteAsync(result.JournalEntryId, ct);
    }

    // القراءة بعد الكتابة من القاعدة لا من الطلب: رقم المستند والفترة المالية
    // يحدّدهما الإجراء، وإرجاع ما أرسله العميل كان سيُخفي ما فعله الإجراء فعلاً
    private async Task<JournalEntryResponseDto> LoadAfterWriteAsync(Guid id, CancellationToken ct)
    {
        var entry = await _entryRepository.GetByIdWithLinesAsync(id, ct)
            ?? throw new NotFoundException("تعذّر قراءة القيد بعد ترحيله.");

        return Map(entry, await _lineRepository.GetByEntryAsync(id, ct));
    }

    private static JournalEntryListItemDto MapListItem(JournalEntry entry) => new()
    {
        Id = entry.Id,
        DocumentNumber = entry.DocumentNumber,
        BranchId = entry.BranchId,
        PostingDate = entry.PostingDate,
        DocumentDate = entry.DocumentDate,
        SourceModule = entry.SourceModule,
        ReversalOfJournalEntryId = entry.ReversalOfJournalEntryId,
        Description = entry.Description
    };

    private static JournalEntryResponseDto Map(JournalEntry entry, List<JournalLine> lines) => new()
    {
        Id = entry.Id,
        DocumentNumber = entry.DocumentNumber,
        TransactionId = entry.TransactionId,
        BranchId = entry.BranchId,
        FiscalPeriodId = entry.FiscalPeriodId,
        PostingDate = entry.PostingDate,
        DocumentDate = entry.DocumentDate,
        SourceModule = entry.SourceModule,
        ReversalOfJournalEntryId = entry.ReversalOfJournalEntryId,
        Description = entry.Description,
        Lines = [.. lines.Select(MapLine)]
    };

    private static JournalLineResponseDto MapLine(JournalLine line) => new()
    {
        Id = line.Id,
        LineNumber = line.LineNumber,
        AccountId = line.AccountId,
        AccountCode = line.Account?.Code ?? string.Empty,
        AccountName = line.Account?.Name ?? string.Empty,
        CurrencyId = line.CurrencyId,
        CurrencyCode = line.Currency?.Code ?? string.Empty,
        ExchangeRate = line.ExchangeRate,
        ExchangeRateDate = line.ExchangeRateDate,
        DebitFC = line.DebitFC,
        CreditFC = line.CreditFC,
        DebitBase = line.DebitBase,
        CreditBase = line.CreditBase,
        Description = line.Description
    };
}
