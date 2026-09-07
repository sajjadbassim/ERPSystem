namespace ErpApi.Core.Models;

// كيان قراءة فقط في EF Core. الكتابة حصراً عبر usp_JournalEntry_Post (R-GL-05).
// «من أنشأ» يُشتق من رأس القيد ولا يُكرَّر هنا، وكذلك TransactionId.
public class JournalLine
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public short LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string? Description { get; set; }

    // الحقول الخمسة الإلزامية لكل سطر مالي (R-AMT-02)
    public Guid CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateOnly ExchangeRateDate { get; set; }
    public decimal DebitFC { get; set; }
    public decimal CreditFC { get; set; }
    public decimal DebitBase { get; set; }
    public decimal CreditBase { get; set; }

    public DateTime CreatedAt { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
}
