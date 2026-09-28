namespace ErpApi.Core.Constants;

public static class AuthMessages
{
    // رسالة واحدة لكل حالات فشل الدخول: اسم مجهول، كلمة خاطئة، حساب معطَّل، اسم متكرر
    // بين شركتين. أي تمييز بينها يكشف وجود الحساب من عدمه (بند 13). الحارس: E03
    public const string InvalidCredentials = "بيانات الدخول غير صحيحة.";

    public const string SessionNoLongerValid = "انتهت الجلسة. الرجاء تسجيل الدخول من جديد.";

    public const string BranchOutOfScope = "لا تملك صلاحية الوصول إلى هذا الفرع.";

    // ‏كيان موجود في شركة غير شركة الفاعل (الدين 8). والتمييز عن 404 مقصود بقرار: نمط
    // ‏`JournalEntryService.GetByIdAsync` لا نمط `EnsureBranchAccessAsync`
    public const string CompanyOutOfScope = "لا تملك صلاحية الوصول إلى بيانات هذه الشركة.";

    public const string PermissionMissing = "لا تملك الصلاحية اللازمة لهذه العملية.";
}
