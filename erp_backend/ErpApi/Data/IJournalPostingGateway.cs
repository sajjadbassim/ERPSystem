using ErpApi.Core.Constants;

namespace ErpApi.Data;

public sealed record PostingLineInput(
    Guid Id,
    short LineNumber,
    Guid AccountId,
    string? Description,
    Guid CurrencyId,
    decimal ExchangeRate,
    DateOnly ExchangeRateDate,
    decimal DebitFC,
    decimal CreditFC,
    decimal DebitBase,
    decimal CreditBase);

public sealed record PostingResult(Guid JournalEntryId, string DocumentNumber);

// المسار الوحيد للكتابة في JournalEntries و JournalLines (R-GL-05).
// **ليس مستودعاً وليس اسمه كذلك عمداً**: مستودعا القيد والسطر للقراءة فقط ولا يحملان
// عضواً كاتباً (الحارسان L20 و L21). الكتابة هنا تمرّ بالإجراء المخزَّن حصراً،
// و DENY على حساب التطبيق يجعل الالتفاف مستحيلاً حتى لو كُتب كود يحاوله
public interface IJournalPostingGateway
{
    Task<PostingResult> PostAsync(
        Guid branchId,
        DateOnly postingDate,
        DateOnly documentDate,
        string? description,
        JournalSourceModule sourceModule,
        Guid createdByUserId,
        IReadOnlyList<PostingLineInput> lines,
        CancellationToken ct = default);

    Task<PostingResult> ReverseAsync(
        Guid originalJournalEntryId,
        Guid branchId,
        DateOnly postingDate,
        DateOnly documentDate,
        string? description,
        Guid createdByUserId,
        IReadOnlyList<Guid> reversalLineIds,
        CancellationToken ct = default);
}
