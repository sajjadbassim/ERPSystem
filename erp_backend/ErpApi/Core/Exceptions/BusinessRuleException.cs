namespace ErpApi.Core.Exceptions;

// 400 — خطأ في مدخلات الطلب لا تعارض مع حالة المورد.
// التمييز عن ConflictException مقصود (بند 8.1): «سعر صفري» مدخل خاطئ،
// و«سعر لنفس الزوج والتاريخ موجود» تعارض حالة. الحرّاس: K20, K21
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
