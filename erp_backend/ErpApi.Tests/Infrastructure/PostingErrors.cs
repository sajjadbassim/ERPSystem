namespace ErpApi.Tests.Infrastructure;

// عقد الأخطاء بين الإجراء المخزَّن والاختبارات.
// الفحص على الرقم لا على النص، حتى لا تنكسر الاختبارات بتعديل صياغة رسالة عربية.
public static class PostingErrors
{
    public const int Unbalanced = 50001;
    public const int ResidualExceedsTolerance = 50002;
    public const int RoundingAccountMissing = 50003;
    public const int PeriodClosed = 50004;
    public const int FiscalYearClosed = 50005;
    public const int PostingDateOutsidePeriod = 50006;
    public const int PeriodBelongsToAnotherCompany = 50007;
    public const int AccountBelongsToAnotherCompany = 50008;
    public const int AccountNotPostable = 50009;
    public const int AccountInactive = 50010;
    public const int LineCurrencyConflictsWithAccount = 50011;
    public const int BaseCurrencyRateMustBeOne = 50012;
    public const int BaseCurrencyAmountsMustMatch = 50013;
    public const int DebitCreditInvalid = 50014;
    public const int BaseNotEqualConverted = 50015;
    public const int NoLines = 50016;
    public const int SingleLine = 50017;
    public const int DuplicateLineNumber = 50018;
    public const int EmptyTransactionId = 50019;
    public const int NonPositiveExchangeRate = 50020;
    public const int NumberSequenceNotConfigured = 50021;
    public const int ReferencedEntityNotFound = 50022;
    public const int NoRegularPeriodForDate = 50023;
    public const int SameCurrencyDifferentRates = 50024;
    public const int ManualDescriptionRequired = 50025;
    public const int DocumentDateAfterPostingDate = 50026;

    public const int ReversalOriginalAlreadyReversed = 50027;
    public const int ReversalOfReversal = 50028;
    public const int ReversalCrossCompany = 50029;
    public const int ReversalLineIdCountMismatch = 50030;
    public const int ReversalNotAllowedViaPost = 50031;

    // أخطاء التريجرات — الدفاع ضد الكتابة المباشرة
    public const int TriggerUnbalanced = 51001;
    public const int TriggerModificationForbidden = 51002;
    public const int TriggerPostingContextInvalid = 51003;
    public const int TriggerLineContextInvalid = 51004;
    public const int TriggerBaseCurrencyUnlockForbidden = 51005;
    public const int TriggerSequenceDeleteForbidden = 51006;
    public const int TriggerAccountConfigurationInvalid = 51007;
    public const int TriggerFiscalYearOverlap = 51008;
    public const int TriggerFiscalYearHasOpenPeriods = 51009;
    public const int TriggerFiscalPeriodOutsideYear = 51010;
    public const int TriggerFiscalPeriodOverlap = 51011;
    public const int TriggerUserBranchCrossCompany = 51012;
    public const int TriggerUserRoleCrossCompany = 51013;
    public const int TriggerAuditLogImmutable = 51014;

    // انتهاك قيد CHECK أو FK في SQL Server
    public const int ConstraintViolation = 547;
    // رفض صلاحية
    public const int PermissionDenied = 229;
}
