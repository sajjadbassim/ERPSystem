namespace ErpApi.Services.Audit;

// المسار الثاني للكتابة. حدث الوصول ليس تغيير كيان — لا صف تبدّل ولا «قبل» ولا «بعد» —
// فلا يلتقطه Interceptor يعمل على ChangeTracker. يُستدعى صراحةً ممن يعرف أن وصولاً وقع
public interface IAuditService
{
    Task RecordAccessAsync(
        string action, string entityName, string entityKey, CancellationToken ct = default);
}
