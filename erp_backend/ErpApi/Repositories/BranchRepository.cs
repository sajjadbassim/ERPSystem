using ErpApi.Common;
using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly AppDbContext _context;

    public BranchRepository(AppDbContext context) => _context = context;

    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<List<Branch>> GetPagedAsync(
        Guid companyId, Guid? scopedUserId, PaginationParams pagination,
        CancellationToken ct = default) =>
        Filter(companyId, scopedUserId)
            .OrderBy(b => b.Code)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

    public Task<int> CountAsync(Guid companyId, Guid? scopedUserId, CancellationToken ct = default) =>
        Filter(companyId, scopedUserId).CountAsync(ct);

    // حدّان مستقلان يُطبَّقان معاً. والمصدر واحد للقائمة والعدّاد، فلا تنشأ صفحة
    // مقيَّدة بعدّاد غير مقيَّد — ترقيمٌ كاذب وصفحات فارغة تصدّقها الواجهة (الحارس K27)
    private IQueryable<Branch> Filter(Guid companyId, Guid? scopedUserId)
    {
        // الحدّ الأول: شركة الفاعل. يسري على الجميع بمن فيهم حاملُ AllBranches
        var query = _context.Branches.AsNoTracking().Where(b => b.CompanyId == companyId);

        if (scopedUserId is not { } userId)
        {
            return query;
        }

        // الحدّ الثاني: الفروع المخصَّصة وحدها. استعلام فرعيّ يترجمه EF إلى EXISTS
        // داخل الاستعلام الواحد — لا نداء لكل صف. وهو نمط UserRepository.cs:23-24 نفسه.
        // و`HasBranchAsync` لا يصلح هنا: `Task<bool>` منفَّذ لا يُركَّب في شجرة تعبير
        return query.Where(b => _context.UserBranches
            .Any(ub => ub.UserId == userId && ub.BranchId == b.Id));
    }

    public Task<bool> CodeExistsInCompanyAsync(
        Guid companyId, string code, CancellationToken ct = default) =>
        _context.Branches
            .AsNoTracking()
            .AnyAsync(b => b.CompanyId == companyId && b.Code == code, ct);

    public async Task AddAsync(Branch entity, CancellationToken ct = default) =>
        await _context.Branches.AddAsync(entity, ct);

    public void Update(Branch entity) => _context.Branches.Update(entity);
}
