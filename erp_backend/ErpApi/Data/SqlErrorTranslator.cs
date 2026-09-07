using ErpApi.Core.Exceptions;
using Microsoft.Data.SqlClient;

namespace ErpApi.Data;

// المترجم المركزي الوحيد لأخطاء القاعدة. يقع على حدّ طبقة البيانات لا في الكنترولر
// ولا في كل خدمة: الرقم الخام ونصّ SQL لا يعبران هذا الحدّ أبداً.
//
// رسائل THROW في الإجراءات والتريجرات عربية ومكتوبة للمستخدم أصلاً، فتُمرَّر كما هي.
// أما 547 و2601 و2627 فرسائلها نصّ SQL إنجليزي يذكر أسماء القيود والجداول — تُستبدل
// برسالة عامة، لأن تسريب بنية المخطط للعميل كشف لا إفادة (بند 8.4).
public static class SqlErrorTranslator
{
    private const int CheckOrForeignKeyViolation = 547;
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private const int ProcedureErrorFloor = 50001;
    private const int ProcedureErrorCeiling = 50031;
    private const int TriggerErrorFloor = 51001;
    private const int TriggerErrorCeiling = 51014;

    // تعارض مع **حالة** المورد ⟶ 409. ما عداه من نطاقَي الإجراءات والتريجرات
    // خطأ في **مدخلات** الطلب ⟶ 400 (بند 8.1)
    private static readonly HashSet<int> ConflictNumbers =
    [
        50027, // القيد الأصلي معكوس بالفعل
        50028, // عكس قيد عكسي
        51002, // القيد المرحَّل لا يُعدَّل ولا يُحذف
        51005, // قفل عملة الأساس لا يُخفض
        51006, // منع حذف العدّاد
        51008, // تداخل السنوات المالية
        51009, // إقفال سنة وفيها فترة مفتوحة
        51011, // تداخل فترتين عاديتين
        51014  // سجل التدقيق لا يُعدَّل ولا يُحذف
    ];

    public static bool TryTranslate(Exception exception, out AppException translated)
    {
        var sql = Find(exception);

        if (sql is null)
        {
            translated = null!;
            return false;
        }

        translated = Map(sql);
        return true;
    }

    private static SqlException? Find(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql;
            }
        }

        return null;
    }

    private static AppException Map(SqlException sql) => sql.Number switch
    {
        UniqueIndexViolation or UniqueConstraintViolation =>
            new ConflictException("القيمة المُدخلة مستعملة بالفعل."),

        CheckOrForeignKeyViolation =>
            new BusinessRuleException("البيانات المُدخلة تخالف قيداً على مستوى قاعدة البيانات."),

        var n when IsKnownDomainError(n) && ConflictNumbers.Contains(n) =>
            new ConflictException(CleanMessage(sql)),

        var n when IsKnownDomainError(n) =>
            new BusinessRuleException(CleanMessage(sql)),

        // غير معروف: يُترك ليصير 500 برسالة عامة و TraceId. تمريره كـ 400 برسالته
        // كان سيسرّب نصّ SQL خاماً تحت غطاء «خطأ مدخلات»
        _ => null!
    };

    private static bool IsKnownDomainError(int number) =>
        number is >= ProcedureErrorFloor and <= ProcedureErrorCeiling
            or (>= TriggerErrorFloor and <= TriggerErrorCeiling);

    // رسالة THROW وحدها. SqlException.Message قد يلحق بها سطوراً تقنية عند التغليف
    private static string CleanMessage(SqlException sql)
    {
        var message = sql.Errors.Count > 0 ? sql.Errors[0].Message : sql.Message;
        var firstLine = message.Split('\n')[0].Trim();

        return string.IsNullOrWhiteSpace(firstLine)
            ? "تعذّر إتمام العملية لمخالفتها قاعدة محاسبية."
            : firstLine;
    }
}
