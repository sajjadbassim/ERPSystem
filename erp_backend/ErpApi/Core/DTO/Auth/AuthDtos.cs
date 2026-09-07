using System.ComponentModel.DataAnnotations;

namespace ErpApi.Core.DTO.Auth;

public class LoginRequestDto
{
    [Required(ErrorMessage = "اسم المستخدم مطلوب.")]
    [MaxLength(50, ErrorMessage = "اسم المستخدم أطول من الحد المسموح.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة.")]
    public string Password { get; set; } = string.Empty;

    // اختياري. يلزم وحده حين يتكرر اسم المستخدم بين شركتين — وهو مسموح بنيوياً
    // لأن التفرّد على مستوى الشركة لا النظام (الحارس E07)
    [MaxLength(20, ErrorMessage = "رمز الشركة أطول من الحد المسموح.")]
    public string? CompanyCode { get; set; }
}

public class RefreshRequestDto
{
    [Required(ErrorMessage = "رمز التجديد مطلوب.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ChangePasswordRequestDto
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة.")]
    [MinLength(8, ErrorMessage = "كلمة المرور الجديدة أقصر من ثمانية محارف.")]
    public string NewPassword { get; set; } = string.Empty;
}

// بلا أي حقل حساس: لا تجزئة ولا ختم أمان. الحارس: E09
public class TokenPairDto
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime AccessTokenExpiresAt { get; init; }
}
