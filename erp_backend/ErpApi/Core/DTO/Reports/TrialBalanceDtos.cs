namespace ErpApi.Core.DTO.Reports;

// مبلغ بعملة الدفاتر كائناً كاملاً لا رقماً عارياً (R-API-02). والاسم `AmountBase` من
// قائمة R-AMT-05 المعتمدة: المعنى في اسم الحقل نفسه، لا في الحاوية التي تضمّه.
// والقيمة تعبر نصّاً بالمحوّل العام (R-API-03)، لا بتنسيق هنا
public class BaseAmountDto
{
    public required decimal AmountBase { get; init; }
    public required Guid CurrencyId { get; init; }
    public required string CurrencyCode { get; init; }
}

public class TrialBalanceRowDto
{
    public required Guid AccountId { get; init; }
    public required string AccountCode { get; init; }
    public required string AccountName { get; init; }

    // الرصيد الصافي في عمود واحد والآخر صفر: الموجب مدين، والسالب دائن بقيمته المطلقة
    public required BaseAmountDto DebitBalance { get; init; }
    public required BaseAmountDto CreditBalance { get; init; }
}

// المجموعان من الخادم لا من الواجهة: الواجهة لا تجمع (R-API-01)
public class TrialBalanceTotalsDto
{
    public required BaseAmountDto TotalDebit { get; init; }
    public required BaseAmountDto TotalCredit { get; init; }
}

public class TrialBalanceResponseDto
{
    // R-RPT-02: التقرير يعلن أساسه في رأسه. والقيمة ثابتة لأن الميزان بعملة الدفاتر حصراً
    public const string BaseCurrencyBasis = "BaseCurrency";

    // ‏‏`required` لا قيمة افتراضية: الافتراضي كان يُخرجه من `required` في العقد، فيصير
    // اختيارياً في الأنواع المولَّدة — وإعلان الأساس ليس اختيارياً
    public required string Basis { get; init; }

    public required Guid BranchId { get; init; }
    public required Guid BaseCurrencyId { get; init; }
    public required string BaseCurrencyCode { get; init; }

    public required IReadOnlyList<TrialBalanceRowDto> Rows { get; init; }
    public required TrialBalanceTotalsDto Totals { get; init; }
}
