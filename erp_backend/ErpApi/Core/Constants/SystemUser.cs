namespace ErpApi.Core.Constants;

// المستخدم الجذر. يُبذَر في الترحيل بمعرّف ثابت لأنه يحل حلقة CreatedByUserId المفرغة:
// أول صف في النظام يحتاج منشئاً، والمنشئ نفسه صف يحتاج منشئاً.
// لا ينتمي لشركة (فهو سابق لوجودها) ولا يُسجَّل دخوله (IsActive = 0).
public static class SystemUser
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public const string IdLiteral = "00000000-0000-0000-0000-000000000001";

    public const string UserName = "system";
}
