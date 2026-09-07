namespace ErpApi.Core.Constants;

// أسماء المطالبات ثوابت لا نصوص متناثرة (بند 5).
// الصلاحيات والفروع لا تُوضع في الرمز عمداً: سحبها يجب أن يسري فوراً لا عند انتهاء الرمز،
// فتُقرأ من القاعدة عند كل طلب (الحارسان G09 و H05).
public static class AppClaimTypes
{
    public const string UserId = "erp:uid";
    public const string CompanyId = "erp:cid";
    public const string SecurityStamp = "erp:stamp";
}
