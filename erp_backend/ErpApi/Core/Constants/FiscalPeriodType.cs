namespace ErpApi.Core.Constants;

// فترة التسويات تتداخل زمنياً مع الفترة العادية عمداً، ولا يُمنع تداخلها
public enum FiscalPeriodType : byte
{
    Regular = 1,
    Adjustment = 2
}
