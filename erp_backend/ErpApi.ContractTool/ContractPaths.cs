namespace ErpApi.ContractTool;

// موقع الملف المعتمَد معرَّف مرة واحدة هنا، ويقرأه الكاتب واختبارا الانحراف معاً.
// تكراره في الطرفين يعني أن تحريك الملف يوماً يجعل الكاتب يكتب في موضع والمقارِن
// يقرأ من آخر — فيمرّ الانحراف بلا كشف.
public static class ContractPaths
{
    public const string DocumentFileName = "openapi.json";

    // يُصعَد من مجلد التنفيذ حتى يُعثر على مشروع ErpApi. البديل — نسخ الملف إلى
    // مجلد المخرجات — مرفوض: بناء تزايدي يقارن حينها نسخة قديمة فيمرّ الانحراف.
    public static string ResolveDocumentPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var projectFile = Path.Combine(directory.FullName, "ErpApi", "ErpApi.csproj");

            if (File.Exists(projectFile))
            {
                return Path.Combine(directory.FullName, "ErpApi", DocumentFileName);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"تعذّر تحديد موقع مشروع ErpApi بالصعود من {AppContext.BaseDirectory}. "
            + "مرّر مسار الملف صراحةً كوسيط أول.");
    }
}
