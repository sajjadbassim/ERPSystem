namespace ErpApi.Core.Constants;

// أفعال السجل ثوابت لا نصوص متناثرة (بند 5). أفعال تغيير الكيانات يولّدها الـ Interceptor
// من اسم الكيان وحالته بالصيغة نفسها: User.Updated, UserRole.Deleted
public static class AuditActions
{
    public const string QueryAllBranches = "Query.AllBranches";

    // مفتاح الكيان حين يكون النطاق «كل ما تسمح به الصلاحية» لا صفاً بعينه
    public const string AllScope = "*";
}
