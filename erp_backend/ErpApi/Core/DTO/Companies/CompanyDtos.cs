using System.ComponentModel.DataAnnotations;

namespace ErpApi.Core.DTO.Companies;

// لا حقل لـ FxRoundingToleranceBase عمداً: يُشتق من دقة عملة الأساس ولا يُستقبل
// من العميل (R-AMT-07-a). قبوله كمدخل كان يسمح برفع الحد ليبتلع أخطاء تحويل حقيقية
public class CompanyCreateDto
{
    [Required(ErrorMessage = "رمز الشركة مطلوب.")]
    [MaxLength(20, ErrorMessage = "رمز الشركة أطول من الحد المسموح.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الشركة مطلوب.")]
    [MaxLength(200, ErrorMessage = "اسم الشركة أطول من الحد المسموح.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "عملة الدفاتر مطلوبة.")]
    public Guid BaseCurrencyId { get; set; }
}

public class CompanyResponseDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }

    public required Guid BaseCurrencyId { get; init; }
    public required string BaseCurrencyCode { get; init; }

    // يعبر كنصّ كبقية العشريات (R-API-03)
    public required decimal FxRoundingToleranceBase { get; init; }

    public required bool IsBaseCurrencyLocked { get; init; }
    public required bool IsActive { get; init; }
}
