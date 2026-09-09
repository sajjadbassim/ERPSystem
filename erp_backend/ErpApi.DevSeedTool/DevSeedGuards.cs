using Microsoft.Data.SqlClient;

namespace ErpApi.DevSeedTool;

// ‏**حزامان لا واحد.** الأداة تُستدعى بيد إنسان، واليد تخطئ: أمرٌ يُنسخ من توثيق
// إلى طرفية على خادم، أو متغيّر بيئة منسيّ. فالحاجز الأول يسأل «أي بيئة؟» والثاني
// يسأل «أي قاعدة؟» — وتخطّي أحدهما وحده لا يكفي لكتابة صفّ واحد.
//
// ولا يُستعمل `IsDevelopment()` من الإطار هنا بل يُقرأ المتغيّر صراحةً: الإطار يقرأ
// ‏`DOTNET_ENVIRONMENT` أو `ASPNETCORE_ENVIRONMENT` بحسب نوع المضيف، والالتباس في
// حارس أمان ثمنه أعلى من سطر صريح.
public static class DevSeedGuards
{
    public const string EnvironmentVariableName = "ASPNETCORE_ENVIRONMENT";

    private const string RequiredEnvironment = "Development";

    // ‏مضيفات محلية حصراً. `.` و`.\SQLEXPRESS` و`localhost` و`(localdb)` و`127.0.0.1`
    private static readonly string[] LocalHostPrefixes =
        [".", "localhost", "(localdb)", "127.0.0.1"];

    public static void EnsureDevelopmentEnvironment()
    {
        var current = Environment.GetEnvironmentVariable(EnvironmentVariableName);

        if (!string.Equals(current, RequiredEnvironment, StringComparison.Ordinal))
        {
            throw new DevSeedRefusedException(
                $"الحاجز الأول: {EnvironmentVariableName} يجب أن يساوي {RequiredEnvironment} بالضبط، "
                + $"والقيمة الحالية {Describe(current)}. البذرة بيانات تطوير ولا تُكتب في غير بيئته.");
        }
    }

    public static void EnsureLocalConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DevSeedRefusedException(
                "سلسلة الاتصال غائبة. اضبط ConnectionStrings:DefaultConnection في User Secrets.");
        }

        // ‏التحليل بالباني لا بمطابقة نصّ: `Server=` و`Data Source=` و`Addr=` أسماء
        // مترادفة للمفتاح نفسه، ومطابقة النصّ كانت ستُمرّر واحداً منها
        var dataSource = new SqlConnectionStringBuilder(connectionString).DataSource;

        if (!IsLocal(dataSource))
        {
            throw new DevSeedRefusedException(
                $"الحاجز الثاني: مصدر البيانات «{dataSource}» ليس محلياً. "
                + "البذرة لا تُكتب إلا في " + string.Join(" أو ", LocalHostPrefixes) + ".");
        }
    }

    private static bool IsLocal(string dataSource) =>
        LocalHostPrefixes.Any(prefix =>
            dataSource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string Describe(string? value) =>
        value is null ? "غير مضبوطة" : $"«{value}»";
}

public sealed class DevSeedRefusedException : Exception
{
    public DevSeedRefusedException(string message) : base(message)
    {
    }
}
