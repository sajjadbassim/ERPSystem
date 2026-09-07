using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<FxRate> FxRates => Set<FxRate>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // يُكتب ولا يُقرأ من منطق العمل. الكتابة عبر Interceptor التدقيق والمسار الصريح،
    // ولا مسار يعدّله أو يحذفه — يفرض ذلك TR_AuditLogEntry_PreventModification
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    // للقراءة فقط. الكتابة عبر usp_JournalEntry_Post حصراً (R-GL-05)
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
