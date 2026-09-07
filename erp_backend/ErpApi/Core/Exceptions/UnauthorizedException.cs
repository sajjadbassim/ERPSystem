namespace ErpApi.Core.Exceptions;

// 401 — لا هوية صالحة. الحرّاس: E02, E04, E05, F02, F03, F05, F06, F08, F09, F10
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
