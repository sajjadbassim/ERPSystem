using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Reports;
using ErpApi.Data;
using ErpApi.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Services.Reports;

// ‏نتيجتا الاستعلام الخام. عامّتان لأن `SqlQuery<T>` يبني منهما نوعاً عابراً بالانعكاس،
// ولا تُستعملان خارج هذه الخدمة
public sealed class TrialBalanceHeaderRow
{
    public required Guid BaseCurrencyId { get; init; }
    public required string BaseCurrencyCode { get; init; }
}

public sealed class TrialBalanceBalanceRow
{
    public required Guid AccountId { get; init; }
    public required string AccountCode { get; init; }
    public required string AccountName { get; init; }
    public required decimal DebitBalance { get; init; }
    public required decimal CreditBalance { get; init; }
    public required decimal TotalDebit { get; init; }
    public required decimal TotalCredit { get; init; }
}

// ‏**Read Service (بند 10.1):** استعلام مباشر، التجميع في القاعدة، بلا مستودع.
//
// ‏**‏`Database.SqlQuery<T>` لا `SqlCommand`** — والسابقة `JournalPostingGateway` تستعمل
// الثاني لأنها تحتاج إجراءً مخزَّناً ونوع جدول ومعاملاً مُخرَجاً، ولا شيء منها هنا.
// و`SqlQuery` يعطي: معاملات من الاستيفاء (`{branchId}` يصير `DbParameter` لا نصّاً —
// فلا حقن)، وربطاً مكتوب النوع، واتصال EF نفسه بلا فتح يدويّ. **ولا يمرّ بأي Query
// Filter** لأن النصّ خام — وهذا مقصود هنا لا عرَض.
public class TrialBalanceService : ITrialBalanceService
{
    private readonly IUserService _userService;
    private readonly AppDbContext _context;

    public TrialBalanceService(IUserService userService, AppDbContext context)
    {
        _userService = userService;
        _context = context;
    }

    public async Task<TrialBalanceResponseDto> GetAsync(Guid branchId, CancellationToken ct = default)
    {
        // ‏الفحص **قبل** بناء الاستعلام (بند 13.1): الاستعلام خارج EF فلا حارس غيره.
        // والفرع المفحوص هنا هو نفسه الذي يرشّح الاستعلام — لا معامل ثانٍ يُثق به
        await _userService.EnsurePermissionAsync(Permissions.TrialBalanceRead, ct);
        await _userService.EnsureBranchAccessAsync(branchId, ct);

        // ‏عملة الدفاتر **من الشركة المشتقّة من الفرع** (R-CUR-03) لا من معامل
        var header = await _context.Database.SqlQuery<TrialBalanceHeaderRow>($"""
            SELECT c.BaseCurrencyId, cur.Code AS BaseCurrencyCode
            FROM dbo.Branches b
            INNER JOIN dbo.Companies c ON c.Id = b.CompanyId
            INNER JOIN dbo.Currencies cur ON cur.Id = c.BaseCurrencyId
            WHERE b.Id = {branchId}
            """).SingleAsync(ct);

        // ‏الدفتر المرحَّل مصدر الأرقام الوحيد (R-RPT-01): `JournalLines` لا يكتبه إلا الإجراء.
        //
        // ‏**لا `IsDeleted` على `Accounts` بقصد (R-LIFE-06):** حساب محذوف منطقياً وله حركة
        // يظهر، وإلا خرج ميزان متوازن ينقصه حساب بلا إعلان.
        //
        // ‏والصافي بلا تقريب (R-AMT-07): `DECIMAL(19,4)` تُجمع في `DECIMAL(38,4)`. والمجموعان
        // في القاعدة كذلك (`SUM ... OVER ()`)، لا في الذاكرة
        var rows = await _context.Database.SqlQuery<TrialBalanceBalanceRow>($"""
            WITH Balances AS
            (
                SELECT l.AccountId, SUM(l.DebitBase) - SUM(l.CreditBase) AS Net
                FROM dbo.JournalLines l
                INNER JOIN dbo.JournalEntries e ON e.Id = l.JournalEntryId
                WHERE e.BranchId = {branchId}
                GROUP BY l.AccountId
            )
            SELECT a.Id AS AccountId,
                   a.Code AS AccountCode,
                   a.Name AS AccountName,
                   CASE WHEN b.Net > 0 THEN b.Net ELSE 0 END AS DebitBalance,
                   CASE WHEN b.Net < 0 THEN -b.Net ELSE 0 END AS CreditBalance,
                   SUM(CASE WHEN b.Net > 0 THEN b.Net ELSE 0 END) OVER () AS TotalDebit,
                   SUM(CASE WHEN b.Net < 0 THEN -b.Net ELSE 0 END) OVER () AS TotalCredit
            FROM Balances b
            INNER JOIN dbo.Accounts a ON a.Id = b.AccountId
            ORDER BY a.Code
            """).ToListAsync(ct);

        BaseAmountDto Money(decimal amount) =>
            new() { AmountBase = amount, CurrencyId = header.BaseCurrencyId, CurrencyCode = header.BaseCurrencyCode };

        // ‏فرع بلا حركة: لا صفوف، فالمجموعان صفر — لا قراءة من صفّ غير موجود
        var first = rows.FirstOrDefault();

        return new TrialBalanceResponseDto
        {
            Basis = TrialBalanceResponseDto.BaseCurrencyBasis,
            BranchId = branchId,
            BaseCurrencyId = header.BaseCurrencyId,
            BaseCurrencyCode = header.BaseCurrencyCode,
            Rows = [.. rows.Select(row => new TrialBalanceRowDto
            {
                AccountId = row.AccountId,
                AccountCode = row.AccountCode,
                AccountName = row.AccountName,
                DebitBalance = Money(row.DebitBalance),
                CreditBalance = Money(row.CreditBalance)
            })],
            Totals = new TrialBalanceTotalsDto
            {
                TotalDebit = Money(first?.TotalDebit ?? 0m),
                TotalCredit = Money(first?.TotalCredit ?? 0m)
            }
        };
    }
}
