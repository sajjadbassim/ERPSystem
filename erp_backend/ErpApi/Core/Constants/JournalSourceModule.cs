namespace ErpApi.Core.Constants;

// تتوسع بقيم جديدة مع وحدات الدفعة الثانية، عبر ترحيل يعدّل CK_JournalEntry_SourceModule
public enum JournalSourceModule : byte
{
    Manual = 1,
    OpeningBalance = 2,
    PeriodClosing = 3,
    ManualFxAdjustment = 4
}
