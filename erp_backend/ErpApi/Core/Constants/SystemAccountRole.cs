namespace ErpApi.Core.Constants;

// الأدوار المحجوزة لحسابات يستدعيها محرك الترحيل بنفسه (R-GL-07)
public enum SystemAccountRole : byte
{
    RealizedFxGain = 1,
    RealizedFxLoss = 2,
    UnrealizedFxGain = 3,
    UnrealizedFxLoss = 4,
    FxRoundingDifference = 5
}
