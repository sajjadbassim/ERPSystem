namespace ErpApi.Tests.Docs;

// ‏`ERP-CORE-RULES.md` قانون يحكم المجلدين معاً (قسمه 12: R-API-01..06 و R-UI-01..03).
// وقد كان له نسختان — `erp_backend/docs/` و `erp_frontend/docs/` — فانحرفتا ستة إصدارات
// (1.7 مقابل 1.1) بلا كشف، وغابت عن نسخة الواجهة سبع قواعد كاملة.
//
// ‏`M01` يحرس العقد من الانحراف عن الكود، و `M04` يحرس الأنواع من الانحراف عن العقد —
// وكلاهما **يقارن** لأن طرفيه يجب أن يتعايشا: آلة تستهلك كلاً منهما. أما القانون فلا
// آلة تستهلكه، فلا مبرر لنسخة ثانية أصلاً. ولذلك **لا يقارن هذا الحارس شيئاً بشيء**:
// النسخة واحدة، والانحراف يصير مستحيل الوقوع لا مكشوفاً بعد وقوعه. ووظيفة `M05` أن
// يمنع **عودة** النسخة الثانية — فمن ينسخ الملف يوماً لراحته يصطدم باختبار أحمر فوراً،
// لا بانحراف يُكتشف بعد ستة إصدارات.
//
// ولا يُشارك هذا الملف `ContractPaths` صعودَه رغم تطابق المنطق: تلك الفئة وُجدت لتُبقي
// **الكاتب والمقارِن** متفقين على موضع واحد، وهنا لا كاتب — قارئ واحد فقط، فلا خطر
// الانحراف الذي وُجدت لأجله.
public class CoreRulesSingleSourceTests
{
    private const string CoreRulesFileName = "ERP-CORE-RULES.md";

    private const string CanonicalRelativePath = "docs/" + CoreRulesFileName;

    // مجلدات مولَّدة لا مصادر. نسخة تظهر في `bin` أو `node_modules` أثرُ بناء لا قرار
    // بشري، وكنسها يُبطئ الاختبار ويعرّضه لمسارات أطول مما يحتمله النظام.
    private static readonly string[] SkippedDirectories =
    [
        "node_modules", "dist", "bin", "obj", ".git", ".vs", "coverage", "TestResults"
    ];

    // M05
    [Fact]
    public void M05_CoreRules_ExistsOnlyAtItsSingleCanonicalPath()
    {
        var repositoryRoot = ResolveRepositoryRoot();

        var canonical = Path.GetFullPath(
            Path.Combine(repositoryRoot, CanonicalRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        // حارس ضد النجاح الفارغ: لو نُقل الملف يوماً ولم يُحدَّث هذا المسار، لصار الكنس
        // لا يجد شيئاً ولاجتاز الاختبار وهو لا يحرس شيئاً على الإطلاق.
        Assert.True(File.Exists(canonical),
            $"القانون غائب عن مساره المعتمَد الوحيد: {canonical}. "
            + "إن نُقل عمداً فحدّث CanonicalRelativePath هنا وأشِر إليه من CLAUDE.md الجذر.");

        var found = new List<string>();
        CollectCoreRulesFiles(new DirectoryInfo(repositoryRoot), found);

        // الكنس على **الاسم** في كل الشجرة لا على لائحة مواضع معروفة. واللائحة كانت
        // ستفشل مفتوحة: النسخة الجديدة في مجلد لم يخطر ببال أحد ببساطة ليست فيها —
        // وهو فخّ `L34` نفسه، والصياغة الشاملة هنا تفشل **مغلقة** كما في `M02` و `M03`.
        Assert.True(found.Count > 0,
            $"لم يُعثر على أي ملف باسم {CoreRulesFileName} بالكنس من {repositoryRoot} — "
            + "الكنس منقطع عن الشجرة، والاختبار كان سيجتاز بلا فحص.");

        var strays = found
            .Where(path => !string.Equals(path, canonical, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(strays.Count == 0,
            $"وُجدت {strays.Count} نسخة من القانون خارج مساره الوحيد ({CanonicalRelativePath}). "
            + "النسخة الثانية هي ما أنتجت انحراف الإصدارات 1.1 مقابل 1.7 — احذفها وأشِر "
            + "إلى المصدر الوحيد بدلها:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, strays.Select(path => $"  {path}")));
    }

    private static void CollectCoreRulesFiles(DirectoryInfo directory, List<string> found)
    {
        foreach (var file in directory.EnumerateFiles(CoreRulesFileName))
        {
            found.Add(file.FullName);
        }

        foreach (var child in directory.EnumerateDirectories())
        {
            if (SkippedDirectories.Contains(child.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            CollectCoreRulesFiles(child, found);
        }
    }

    // الصعود إلى جذر المستودع: مجلد `erp_backend` هو ما يحوي `ErpApi/ErpApi.csproj`،
    // وأبوه هو الجذر الذي يضمّ المجلدين معاً. والصعود لا يتوقف عند `.git` لأن غيابه في
    // أرشيف مصدري كان سيُسقط الاختبار على أمر لا علاقة له بما يحرسه.
    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpApi", "ErpApi.csproj")))
            {
                return directory.Parent?.FullName
                       ?? throw new InvalidOperationException(
                           $"‏{directory.FullName} بلا مجلد أب — تعذّر تحديد جذر المستودع.");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"تعذّر تحديد موقع مشروع ErpApi بالصعود من {AppContext.BaseDirectory}.");
    }
}
