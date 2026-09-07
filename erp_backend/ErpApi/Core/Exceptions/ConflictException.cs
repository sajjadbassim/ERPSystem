namespace ErpApi.Core.Exceptions;

// 409 — تعارض مع حالة المورد لا خطأ في المدخلات. الحارسان: H01, H02
public class ConflictException : AppException
{
    public ConflictException(string message) : base(message)
    {
    }
}
