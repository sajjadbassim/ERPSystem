namespace ErpApi.Core.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    // لا قيمة افتراضية: المفتاح يأتي من User Secrets أو متغير بيئة، ويفشل الإقلاع بدونه (بند 18)
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // قصير عمداً (بند 13). الإبطال الفوري يقع عبر SecurityStamp لا بانتظار الانتهاء
    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;
}
