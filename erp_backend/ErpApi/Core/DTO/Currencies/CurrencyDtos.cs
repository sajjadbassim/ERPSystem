using System.ComponentModel.DataAnnotations;

namespace ErpApi.Core.DTO.Currencies;

public class CurrencyCreateDto
{
    [Required(ErrorMessage = "رمز العملة مطلوب.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "رمز العملة ثلاثة محارف بالضبط.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم العملة مطلوب.")]
    [MaxLength(100, ErrorMessage = "اسم العملة أطول من الحد المسموح.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10, ErrorMessage = "رمز العرض أطول من الحد المسموح.")]
    public string? Symbol { get; set; }

    // المدى 0..4 مطابق لـ CK_Currency_DecimalPlaces، ولدقة FxRoundingToleranceBase
    // التي تُشتق منه. فحص الشكل هنا يمنع الرحلة إلى القاعدة (بند 9)
    [Range(0, 4, ErrorMessage = "الخانات العشرية بين صفر وأربعة.")]
    public byte DecimalPlaces { get; set; }
}

public class CurrencyUpdateDto
{
    [MaxLength(100, ErrorMessage = "اسم العملة أطول من الحد المسموح.")]
    public string? Name { get; set; }

    [MaxLength(10, ErrorMessage = "رمز العرض أطول من الحد المسموح.")]
    public string? Symbol { get; set; }
}

public class CurrencyResponseDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Symbol { get; init; }
    public required byte DecimalPlaces { get; init; }
    public required bool IsActive { get; init; }
}
