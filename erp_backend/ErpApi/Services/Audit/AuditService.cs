using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;

namespace ErpApi.Services.Audit;

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(
        IAuditLogRepository auditLogRepository, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _auditLogRepository = auditLogRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task RecordAccessAsync(
        string action, string entityName, string entityKey, CancellationToken ct = default)
    {
        await _auditLogRepository.AddAsync(new AuditLogEntry
        {
            UserId = _currentUser.UserId ?? SystemUser.Id,
            Action = action,
            EntityName = entityName,
            EntityKey = entityKey,

            // NULL لا سلسلة فارغة. NULL يقول «لا ينطبق» — لا قيمة سابقة ولا لاحقة لأن
            // لا شيء تغيّر. و'' كان سيقول «لقطة أُخذت فخرجت فارغة»، وهو ادّعاء كاذب.
            // والفرق عملي لا لفظي: `WHERE BeforeJson IS NULL` يجمع الإنشاءات وأحداث
            // الوصول في محمول واحد صادق، ولو خُزّن '' لاحتاج كل استعلام شرطاً ثانياً
            BeforeJson = null,
            AfterJson = null,

            CreatedAt = DateTime.UtcNow
        }, ct);

        // حفظ مستقل: حدث الوصول ليس جزءاً من معاملة عمل — يقع غالباً داخل قراءة
        // لا كتابة لها. ربطه بمعاملة أخرى كان سيجعل تراجعها يمحو أثر وصول وقع فعلاً
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
