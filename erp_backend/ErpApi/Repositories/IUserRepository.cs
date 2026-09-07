using ErpApi.Core.Models;

namespace ErpApi.Repositories;

// وصول للبيانات فقط، بلا قرار عمل وبلا SaveChanges (بند 4)
public interface IUserRepository
{
    // يُرجع كل المطابقين لأن UserName فريد داخل الشركة لا في النظام (الحارس E07).
    // حسم أيّهم قرار عمل يخص الخدمة لا المستودع.
    // companyCode اختياري: حضوره يقصر البحث على شركة واحدة فيرفع الالتباس من أصله
    Task<List<User>> GetActiveByUserNameAsync(
        string userName, string? companyCode, CancellationToken ct = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default);

    Task<bool> HasBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default);

    Task<Guid?> GetBranchCompanyIdAsync(Guid branchId, CancellationToken ct = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(byte[] tokenHash, CancellationToken ct = default);

    Task<List<RefreshToken>> GetActiveRefreshTokensAsync(Guid userId, CancellationToken ct = default);

    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);

    void Update(User user);
}
