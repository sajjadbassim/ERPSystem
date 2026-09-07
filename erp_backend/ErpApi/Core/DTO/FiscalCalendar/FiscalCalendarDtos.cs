using System.ComponentModel.DataAnnotations;
using ErpApi.Core.Constants;

namespace ErpApi.Core.DTO.FiscalCalendar;

public class FiscalYearCreateDto
{
    [Required(ErrorMessage = "الشركة مطلوبة.")]
    public Guid CompanyId { get; set; }

    [Required(ErrorMessage = "رمز السنة مطلوب.")]
    [MaxLength(20, ErrorMessage = "رمز السنة أطول من الحد المسموح.")]
    public string Code { get; set; } = string.Empty;

    // تواريخ محاسبية: DateOnly لا DateTime، ولا تخضع لأي تحويل منطقة زمنية (بند 12.3)
    [Required(ErrorMessage = "تاريخ البداية مطلوب.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "تاريخ النهاية مطلوب.")]
    public DateOnly EndDate { get; set; }
}

public class FiscalYearResponseDto
{
    public required Guid Id { get; init; }
    public required Guid CompanyId { get; init; }
    public required string Code { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsClosed { get; init; }
}

public class FiscalPeriodCreateDto
{
    [Required(ErrorMessage = "السنة المالية مطلوبة.")]
    public Guid FiscalYearId { get; set; }

    [Range(1, 14, ErrorMessage = "رقم الفترة بين 1 و14.")]
    public byte PeriodNumber { get; set; }

    [Required(ErrorMessage = "نوع الفترة مطلوب.")]
    public FiscalPeriodType PeriodType { get; set; }

    [Required(ErrorMessage = "اسم الفترة مطلوب.")]
    [MaxLength(50, ErrorMessage = "اسم الفترة أطول من الحد المسموح.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "تاريخ البداية مطلوب.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "تاريخ النهاية مطلوب.")]
    public DateOnly EndDate { get; set; }
}

public class FiscalPeriodResponseDto
{
    public required Guid Id { get; init; }
    public required Guid FiscalYearId { get; init; }
    public required byte PeriodNumber { get; init; }
    public required FiscalPeriodType PeriodType { get; init; }
    public required string Name { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required bool IsClosed { get; init; }
}
