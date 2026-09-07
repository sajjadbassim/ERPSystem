using System.ComponentModel.DataAnnotations;
using ErpApi.Core.Constants;

namespace ErpApi.Core.DTO.FxRates;

public class FxRateCreateDto
{
    [Required(ErrorMessage = "العملة المصدر مطلوبة.")]
    public Guid FromCurrencyId { get; set; }

    [Required(ErrorMessage = "العملة الهدف مطلوبة.")]
    public Guid ToCurrencyId { get; set; }

    // كم وحدة من ToCurrency تساوي وحدة واحدة من FromCurrency (R-FX-02).
    // موجبيته قاعدة عمل تُفحص في الخدمة لا DataAnnotation: حدّ Range على decimal
    // يمرّ بـ double فيفقد الدقة التي بُني عليها النوع (28,12)
    [Required(ErrorMessage = "سعر الصرف مطلوب.")]
    public decimal Rate { get; set; }

    [Required(ErrorMessage = "تاريخ السعر مطلوب.")]
    public DateOnly RateDate { get; set; }

    [Required(ErrorMessage = "مصدر السعر مطلوب.")]
    public FxRateSource RateSource { get; set; }
}

// R-API-02 على حدود الشبكة: لا رقم عارٍ بلا سياق عملته.
// السعر بلا عملتيه ورمزيهما وتاريخه صحيح عددياً وبلا معنى
public class FxRateResponseDto
{
    public required Guid Id { get; init; }

    public required Guid FromCurrencyId { get; init; }
    public required string FromCurrencyCode { get; init; }

    public required Guid ToCurrencyId { get; init; }
    public required string ToCurrencyCode { get; init; }

    public required decimal Rate { get; init; }
    public required DateOnly RateDate { get; init; }
    public required FxRateSource RateSource { get; init; }
}
