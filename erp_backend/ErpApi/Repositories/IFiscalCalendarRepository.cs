using ErpApi.Common;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

// السنة وفتراتها مستودع واحد: الفترة لا وجود لها خارج سنتها، وفصلهما كان
// يفرّق منطقاً يُقرأ ويُكتب معاً دائماً
public interface IFiscalCalendarRepository
{
    Task<FiscalYear?> GetYearAsync(Guid id, CancellationToken ct = default);

    Task<List<FiscalYear>> GetYearsPagedAsync(
        PaginationParams pagination, CancellationToken ct = default);

    Task<int> CountYearsAsync(CancellationToken ct = default);

    Task<bool> YearCodeExistsAsync(Guid companyId, string code, CancellationToken ct = default);

    Task<FiscalPeriod?> GetPeriodAsync(Guid id, CancellationToken ct = default);

    Task<bool> HasOpenPeriodsAsync(Guid fiscalYearId, CancellationToken ct = default);

    Task AddYearAsync(FiscalYear entity, CancellationToken ct = default);

    Task AddPeriodAsync(FiscalPeriod entity, CancellationToken ct = default);

    void UpdateYear(FiscalYear entity);

    void UpdatePeriod(FiscalPeriod entity);
}
