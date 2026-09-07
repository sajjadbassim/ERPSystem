namespace ErpApi.Core.Exceptions;

// 403 — الهوية صالحة والصلاحية أو النطاق ناقص.
// الحرّاس: G02, G03, G06, G07, G08, G09, H04, H05, H06, H08
public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
