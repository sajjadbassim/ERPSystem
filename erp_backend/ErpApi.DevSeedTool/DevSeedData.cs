namespace ErpApi.DevSeedTool;

// ‏**بيانات دخول DEV محلية — لا تُستخدم في الإنتاج.**
//
// كلمة المرور مكتوبة هنا صراحةً وهذا مقصود: إخفاؤها يوهم بأنها سرّ، وهي ليست كذلك —
// حساب تطوير في قاعدة محلية يحرسه حزاما `DevSeedGuards`. وسرٌّ زائف أخطر من قيمة
// معلنة يعرف كل قارئ حدودها.
public static class DevSeedData
{
    public const string UserName = "devadmin";

    public const string Password = "Dev@Local!2026";

    public const string FullName = "مدير التطوير المحلي";

    public const string CompanyCode = "DEV";

    public const string CompanyName = "شركة التطوير المحلية";

    public const string BranchCode = "DEV-01";

    public const string BranchName = "الفرع الرئيسي (تطوير)";

    public const string RoleCode = "DEV-ADMIN";

    public const string RoleName = "مدير التطوير";

    // ‏عملة الأساس. صفر خانات، فحدّ باقي التقريب 10^0 = 1.0000 (R-AMT-07-a)
    public const string CurrencyCode = "IQD";

    public const string CurrencyName = "دينار عراقي";

    public const string CurrencySymbol = "د.ع";

    public const byte CurrencyDecimalPlaces = 0;
}
