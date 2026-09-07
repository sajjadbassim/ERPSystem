using System.ComponentModel.DataAnnotations;

namespace ErpApi.Core.DTO.Branches;

public class BranchCreateDto
{
    [Required(ErrorMessage = "الشركة مطلوبة.")]
    public Guid CompanyId { get; set; }

    [Required(ErrorMessage = "رمز الفرع مطلوب.")]
    [MaxLength(20, ErrorMessage = "رمز الفرع أطول من الحد المسموح.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الفرع مطلوب.")]
    [MaxLength(200, ErrorMessage = "اسم الفرع أطول من الحد المسموح.")]
    public string Name { get; set; } = string.Empty;
}

public class BranchResponseDto
{
    public required Guid Id { get; init; }
    public required Guid CompanyId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}
