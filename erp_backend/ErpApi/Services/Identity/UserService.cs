using ErpApi.Core.Constants;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using ErpApi.Services.Audit;

namespace ErpApi.Services.Identity;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public UserService(
        IUserRepository userRepository, ICurrentUserService currentUser, IAuditService auditService)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<UserContext> GetValidatedContextAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.SecurityStamp is not { } stamp)
        {
            throw new UnauthorizedException(AuthMessages.SessionNoLongerValid);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);

        // ثلاثة أسباب لإسقاط جلسة قائمة برمز ما زال صالح التوقيع:
        // المستخدم محذوف، أو عُطِّل (F10)، أو تغيّر ختمه بتغيير كلمة المرور أو الإبطال (F03, F09)
        if (user is null || !user.IsActive || user.SecurityStamp != stamp)
        {
            throw new UnauthorizedException(AuthMessages.SessionNoLongerValid);
        }

        var permissions = await _userRepository.GetPermissionCodesAsync(userId, ct);

        return new UserContext { User = user, Permissions = permissions };
    }

    public async Task EnsurePermissionAsync(string permissionCode, CancellationToken ct = default)
    {
        var context = await GetValidatedContextAsync(ct);

        // المطابقة على الرمز المطلوب لا على وجود أي رمز: الرمز المجهول المخزَّن
        // لا يطابق شيئاً فلا يمنح شيئاً (الحارس H06)، ومن بلا أدوار قائمته فارغة (الحارس H08)
        if (!context.Has(permissionCode))
        {
            throw new ForbiddenException(AuthMessages.PermissionMissing);
        }
    }

    public async Task EnsureBranchAccessAsync(Guid branchId, CancellationToken ct = default)
    {
        var context = await GetValidatedContextAsync(ct);

        var branchCompanyId = await _userRepository.GetBranchCompanyIdAsync(branchId, ct);

        // حدّ الشركة يسبق كل شيء، والنطاق الشامل لا يتجاوزه (الحارس G08).
        // فرع غير موجود يُرفض بـ 403 لا 404: الرد بـ 404 يكشف أي المعرّفات حقيقي
        if (branchCompanyId is null
            || context.User.CompanyId is not { } companyId
            || branchCompanyId != companyId)
        {
            throw new ForbiddenException(AuthMessages.BranchOutOfScope);
        }

        if (context.Has(Permissions.AllBranches))
        {
            // الوصول عبر النطاق الشامل يترك أثراً حتى حين يكون الفرع محدَّداً:
            // «فلان بلغ هذا الفرع بصلاحيته العامة لا لأنه من فروعه» واقعة تستحق التسجيل
            await _auditService.RecordAccessAsync(
                AuditActions.QueryAllBranches, nameof(Branch), branchId.ToString(), ct);
            return;
        }

        // الغياب يعني «لا فرع» لا «كل الفروع»: الفشل مغلق (الحارس G07)
        if (!await _userRepository.HasBranchAsync(context.User.Id, branchId, ct))
        {
            throw new ForbiddenException(AuthMessages.BranchOutOfScope);
        }
    }

    public async Task EnsureAllBranchesScopeAsync(CancellationToken ct = default)
    {
        var context = await GetValidatedContextAsync(ct);

        // الفشل مغلق: بلا الصلاحية الشاملة لا استعلام بلا فرع
        if (!context.Has(Permissions.AllBranches))
        {
            throw new ForbiddenException(AuthMessages.BranchOutOfScope);
        }

        // النجمة تعني «كل فروع شركته»، وهو النطاق الفعلي — فحدّ الشركة لا يُتجاوز
        await _auditService.RecordAccessAsync(
            AuditActions.QueryAllBranches, nameof(Branch), AuditActions.AllScope, ct);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken ct = default)
    {
        var context = await GetValidatedContextAsync(ct);
        return context.Permissions;
    }

    public async Task<bool> IsSessionValidAsync(
        Guid userId, Guid securityStamp, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        return user is not null && user.IsActive && user.SecurityStamp == securityStamp;
    }
}
