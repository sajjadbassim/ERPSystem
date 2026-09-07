using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    public Task<List<User>> GetActiveByUserNameAsync(
        string userName, string? companyCode, CancellationToken ct = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.UserName == userName && u.IsActive);

        // مرشِّح Companies للمحذوف منطقياً يسري هنا، فشركة محذوفة لا يُسجَّل الدخول إليها
        if (!string.IsNullOrWhiteSpace(companyCode))
        {
            query = query.Where(u => _context.Companies
                .Any(c => c.Id == u.CompanyId && c.Code == companyCode));
        }

        return query.ToListAsync(ct);
    }

    // متعقَّب لا AsNoTracking: تغيير كلمة المرور يعدّل الصف نفسه
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    // القراءة عند كل طلب لا مرة عند الدخول: سحب الدور يسري فوراً (الحارس H05).
    // الدور المعطَّل أو المحذوف لا يمنح شيئاً، ويتكفل Query Filter الخاص بـ Role بالمحذوف
    public Task<List<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default) =>
        _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(_context.Roles.Where(r => r.IsActive),
                  ur => ur.RoleId, r => r.Id, (ur, r) => r.Id)
            .Join(_context.RolePermissions,
                  roleId => roleId, rp => rp.RoleId, (roleId, rp) => rp.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

    public Task<bool> HasBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default) =>
        _context.UserBranches
            .AsNoTracking()
            .AnyAsync(ub => ub.UserId == userId && ub.BranchId == branchId, ct);

    public async Task<Guid?> GetBranchCompanyIdAsync(Guid branchId, CancellationToken ct = default) =>
        await _context.Branches
            .AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => (Guid?)b.CompanyId)
            .FirstOrDefaultAsync(ct);

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(byte[] tokenHash, CancellationToken ct = default) =>
        _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<List<RefreshToken>> GetActiveRefreshTokensAsync(Guid userId, CancellationToken ct = default) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default) =>
        await _context.RefreshTokens.AddAsync(token, ct);

    public void Update(User user) => _context.Users.Update(user);
}
