using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

public class FxRate : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FromCurrencyId { get; set; }
    public Guid ToCurrencyId { get; set; }

    // كم وحدة من ToCurrency تساوي وحدة واحدة من FromCurrency (R-FX-02)
    public decimal Rate { get; set; }
    public DateOnly RateDate { get; set; }
    public FxRateSource RateSource { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Currency FromCurrency { get; set; } = null!;
    public Currency ToCurrency { get; set; } = null!;
}
