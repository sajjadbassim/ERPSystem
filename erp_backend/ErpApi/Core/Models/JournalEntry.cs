using ErpApi.Core.Constants;

namespace ErpApi.Core.Models;

// كيان قراءة فقط في EF Core. الكتابة حصراً عبر usp_JournalEntry_Post (R-GL-05).
// لا يطبّق واجهات التدقيق لأن غرضها ملء القيم عبر Interceptor، والإجراء المخزَّن هو من يملؤها.
// لا يحمل UpdatedAt لأن المرحَّل لا يُعدَّل، ولا IsDeleted لأنه لا يُحذف (R-LIFE-02, R-LIFE-03).
public class JournalEntry
{
    public Guid Id { get; set; }

    // الهوية الموحدة للحركة، تُنسخ في كل أثر تنتجه العملية الاقتصادية (R-TRC-01)
    public Guid TransactionId { get; set; }

    public Guid BranchId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;

    // التاريخ المحاسبي. الفترة تُحدَّد منه ولا تُشتق من CreatedAt إطلاقاً
    public DateOnly PostingDate { get; set; }
    public DateOnly DocumentDate { get; set; }

    public string? Description { get; set; }
    public JournalSourceModule SourceModule { get; set; }

    // يُملأ على القيد العكسي وحده. تاريخه وفترته مستقلان تماماً عن الأصل
    public Guid? ReversalOfJournalEntryId { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Branch Branch { get; set; } = null!;
    public FiscalPeriod FiscalPeriod { get; set; } = null!;
    public JournalEntry? ReversalOfJournalEntry { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}
