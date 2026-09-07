using System.ComponentModel.DataAnnotations;
using ErpApi.Core.Constants;

namespace ErpApi.Core.DTO.Accounts;

public class AccountCreateDto
{
    [Required(ErrorMessage = "الشركة مطلوبة.")]
    public Guid CompanyId { get; set; }

    [Required(ErrorMessage = "رمز الحساب مطلوب.")]
    [MaxLength(20, ErrorMessage = "رمز الحساب أطول من الحد المسموح.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الحساب مطلوب.")]
    [MaxLength(200, ErrorMessage = "اسم الحساب أطول من الحد المسموح.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الحساب مطلوب.")]
    public AccountType AccountType { get; set; }

    [Required(ErrorMessage = "الطبيعة المحاسبية مطلوبة.")]
    public NormalBalance NormalBalance { get; set; }

    public bool IsPostable { get; set; } = true;

    public Guid? ParentAccountId { get; set; }

    // تقييد الحساب بعملة واحدة: صندوق الدولار حساب مستقل عن صندوق الدينار
    public Guid? CurrencyId { get; set; }

    public SystemAccountRole? SystemAccountRole { get; set; }
}

public class AccountResponseDto
{
    public required Guid Id { get; init; }
    public required Guid CompanyId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required AccountType AccountType { get; init; }
    public required NormalBalance NormalBalance { get; init; }
    public required bool IsPostable { get; init; }
    public Guid? ParentAccountId { get; init; }
    public Guid? CurrencyId { get; init; }
    public SystemAccountRole? SystemAccountRole { get; init; }
    public required bool IsActive { get; init; }
}
