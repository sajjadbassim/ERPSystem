namespace ErpApi.Core.Exceptions;

// الأصل المشترك لكل استثناء مصنَّف. الـ Middleware يترجم المشتقات إلى رموز الحالة (بند 8)،
// وما لا يرث منه يصير 500 برسالة عامة ولا تُسرَّب تفاصيله للعميل.
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message)
    {
    }
}
