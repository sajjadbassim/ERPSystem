using ErpApi.Core.Models;

namespace ErpApi.Services.Identity;

// نطاق قراءة الفروع للفاعل الحالي — **وصفٌ لا بوّابة**.
//
// ‏`HasAllBranches` يوسّع النطاق من «فروعي المخصَّصة» إلى «كل فروع شركتي»،
// ‏**ولا يتجاوز `CompanyId` بحال**. فالحدّان مستقلان يُطبَّقان معاً لا أحدهما.
//
// و`UserId` هنا لا زيادة: هو ما يُرشَّح به جدول الربط حين يضيق النطاق، وقراءته
// من نداء ثانٍ كانت ستضرب القاعدة مرتين لسؤال واحد
public sealed record BranchScope(Guid UserId, Guid CompanyId, bool HasAllBranches);

// الجلسة الموثَّقة بعد فحص القاعدة، لا ما تدّعيه المطالبات وحدها
public sealed class UserContext
{
    public required User User { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }

    public bool Has(string permissionCode) => Permissions.Contains(permissionCode);
}

// كل فحص صلاحية ونطاق فرع يقع هنا. الكنترولر يستدعي ولا يقرّر (بند 2.3، R-API-01).
// السبب أعمق من ترتيب الطبقات: تقرير ميزان المراجعة يُنفَّذ باستعلام مباشر خارج EF (بند 10.1)
// فلا يحرسه أي Query Filter — الحارس الوحيد فحص صريح في الخدمة (الحارس G06).
public interface IUserService
{
    // يُعاد تحميلها من القاعدة عند كل طلب: التعطيل وتغيير الختم وسحب الدور
    // يجب أن تسري فوراً لا عند انتهاء الرمز (الحرّاس F09, F10, H05)
    Task<UserContext> GetValidatedContextAsync(CancellationToken ct = default);

    Task EnsurePermissionAsync(string permissionCode, CancellationToken ct = default);

    Task EnsureBranchAccessAsync(Guid branchId, CancellationToken ct = default);

    // الاستعلام بلا فرع محدَّد. غياب المرشِّح ليس تخفيفاً بل أوسع نطاق ممكن،
    // فيتطلب الصلاحية الشاملة ويترك أثراً في كل مرة (الحارسان G05 و J18).
    //
    // **تُرجع معرّف شركة الفاعل، ولا تُسقطه.** «النطاق الشامل» يعني كل فروع شركته
    // لا كل فروع النظام، وذلك الحدّ **بيانٌ يجب أن يبلغ الاستعلام** لا نيّةٌ تُوصف في
    // تعليق: المستدعي لا يملك مصدراً آخر له، فإسقاطه هنا كان يترك الاستعلام بلا مرشِّح
    // شركة فيقرأ دفاتر كل الشركات (الحارس G10)
    Task<Guid> EnsureAllBranchesScopeAsync(CancellationToken ct = default);

    // **استفهام لا بوّابة** — وهذا ما يميّزه عن `EnsureAllBranchesScopeAsync` ويمنع
    // إعادة استعماله مكانه. تلك تَأذن أو تمنع، فتفشل مغلقة بلا `AllBranches`؛
    // وهذه تصف النطاق ليُبنى عليه مرشِّح، فغياب `AllBranches` عندها **نتيجة لا رفض**:
    // يضيق النطاق إلى الفروع المخصَّصة ولا يُردّ الطلب.
    //
    // ولا تكتب أثر تدقيق: لا قرار وصول يقع هنا، والأثر يُكتب حيث يقع القرار (§4.2)
    Task<BranchScope> GetBranchScopeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken ct = default);

    // تُستدعى من حدث التحقق من الرمز، قبل أن يُبنى HttpContext.User — فتأخذ المطالبات
    // وسيطين لا من ICurrentUserService. توقيع الرمز سليم لكن الجلسة قد تكون سقطت
    Task<bool> IsSessionValidAsync(Guid userId, Guid securityStamp, CancellationToken ct = default);
}
