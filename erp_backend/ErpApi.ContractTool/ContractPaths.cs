namespace ErpApi.ContractTool;

// موقع الملف المعتمَد معرَّف مرة واحدة هنا، ويقرأه الكاتب واختبارا الانحراف معاً.
// تكراره في الطرفين يعني أن تحريك الملف يوماً يجعل الكاتب يكتب في موضع والمقارِن
// يقرأ من آخر — فيمرّ الانحراف بلا كشف.
public static class ContractPaths
{
    public const string DocumentFileName = "openapi.json";

    public const string ApiTypesFileName = "schema.d.ts";

    private const string FrontendDirectoryName = "erp_frontend";

    public static string ResolveDocumentPath() =>
        Path.Combine(ResolveBackendRoot(), "ErpApi", DocumentFileName);

    // الأنواع المولَّدة وموّلدها يعيشان في مشروع الواجهة، ويقرأهما الحارس M04.
    // موضعهما هنا لا هناك للسبب نفسه الذي وُضع لأجله مسار العقد: تحريك أحدهما يوماً
    // يجب أن يكسر موضعاً واحداً لا موضعين ينحرفان
    public static string ResolveApiTypesPath() =>
        Path.Combine(ResolveFrontendRoot(), "api-types", ApiTypesFileName);

    public static string ResolveApiTypesGeneratorPath() =>
        Path.Combine(ResolveFrontendRoot(), "scripts", "generate-api-types.mjs");

    private static string ResolveFrontendRoot() =>
        Path.Combine(Directory.GetParent(ResolveBackendRoot())?.FullName
                     ?? throw new InvalidOperationException(
                         "تعذّر تحديد جذر المستودع فوق مجلد erp_backend."),
            FrontendDirectoryName);

    // يُصعَد من مجلد التنفيذ حتى يُعثر على مشروع ErpApi. البديل — نسخ الملف إلى
    // مجلد المخرجات — مرفوض: بناء تزايدي يقارن حينها نسخة قديمة فيمرّ الانحراف.
    private static string ResolveBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var projectFile = Path.Combine(directory.FullName, "ErpApi", "ErpApi.csproj");

            if (File.Exists(projectFile))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"تعذّر تحديد موقع مشروع ErpApi بالصعود من {AppContext.BaseDirectory}. "
            + "مرّر مسار الملف صراحةً كوسيط أول.");
    }
}
