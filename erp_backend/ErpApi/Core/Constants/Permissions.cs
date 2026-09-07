namespace ErpApi.Core.Constants;

// المصدر الوحيد لكتالوج الصلاحيات. تُعرَض عبر نقطة نهاية، ويُمنع كتابة أي رمز يدوياً في الواجهة.
// لا يوجد جدول Permission عمداً: الجدول كان سيخلق مصدر حقيقة ثانياً يجب مزامنته.
public static class Permissions
{
    // النطاق الشامل للفروع. تنفيذ أي استعلام به يُسجَّل صراحة في التدقيق.
    public const string AllBranches = "System.AllBranches";

    public const string UserManage = "System.User.Manage";
    public const string RoleManage = "System.Role.Manage";

    public const string JournalEntryRead = "Accounting.JournalEntry.Read";
    public const string JournalEntryPost = "Accounting.JournalEntry.Post";

    public const string TrialBalanceRead = "Reporting.TrialBalance.Read";

    // قراءة واحدة لكل البيانات الأساسية، وإدارة مفصّلة لكل كيان:
    // إدخال سعر صرف مهمة يومية لموظف، وتغيير عملة أساس الشركة ليس كذلك
    public const string MasterDataRead = "MasterData.Read";
    public const string CurrencyManage = "MasterData.Currency.Manage";
    public const string CompanyManage = "MasterData.Company.Manage";
    public const string BranchManage = "MasterData.Branch.Manage";
    public const string FxRateManage = "MasterData.FxRate.Manage";
    public const string AccountManage = "MasterData.Account.Manage";
    public const string FiscalYearManage = "MasterData.FiscalYear.Manage";

    // إقفال الفترة تصرّف محاسبي لا إدارة بيانات: يمنع الترحيل فيها إلى الأبد (R-GL-04)،
    // فصلاحيته منفصلة عمّن يُنشئ السنة والفترات
    public const string FiscalPeriodClose = "Accounting.FiscalPeriod.Close";

    // العكس منفصل عن الترحيل: من يُرحّل قيداً ليس بالضرورة من يُلغيه (R-GL-03)
    public const string JournalEntryReverse = "Accounting.JournalEntry.Reverse";

    public static IReadOnlyList<string> All { get; } =
    [
        AllBranches,
        UserManage,
        RoleManage,
        JournalEntryRead,
        JournalEntryPost,
        TrialBalanceRead,
        MasterDataRead,
        CurrencyManage,
        CompanyManage,
        BranchManage,
        FxRateManage,
        AccountManage,
        FiscalYearManage,
        FiscalPeriodClose,
        JournalEntryReverse
    ];
}
