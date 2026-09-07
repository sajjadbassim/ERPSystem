namespace ErpApi.Core.Interfaces;

// يغلّف IHttpContextAccessor. يُمنع حقن IHttpContextAccessor في أي خدمة مباشرة (بند 13).
// يقرأ المطالبات فقط ولا يلمس القاعدة: التحقق من صلاحية الجلسة مسؤولية IUserService،
// لأن المطالبة تقول ما كان صحيحاً وقت إصدار الرمز لا ما هو صحيح الآن.
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? CompanyId { get; }

    Guid? SecurityStamp { get; }
}
