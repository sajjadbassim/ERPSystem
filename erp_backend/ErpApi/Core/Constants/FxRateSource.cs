namespace ErpApi.Core.Constants;

// التمييز بين السعر الرسمي وسعر السوق والسعر المتفق عليه (R-FX-06)
public enum FxRateSource : byte
{
    Official = 1,
    Market = 2,
    Negotiated = 3
}
