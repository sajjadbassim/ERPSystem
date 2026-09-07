using ErpApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpApi.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    // نقطة الترجمة المركزية لكل كتابة تمرّ بـ EF: القيود الفريدة و CHECK والتريجرات
    // تصل هنا كـ DbUpdateException، فتُترجم مرة واحدة بدل تكرار try/catch في كل خدمة
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (SqlErrorTranslator.TryTranslate(exception, out var translated) && translated is not null)
        {
            throw translated;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await operation();
            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
