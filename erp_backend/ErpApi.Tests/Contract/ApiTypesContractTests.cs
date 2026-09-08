using System.ComponentModel;
using System.Diagnostics;
using ErpApi.ContractTool;

namespace ErpApi.Tests.Contract;

// ‏M01 يحرس `openapi.json` من أن ينحرف عن الكود. وهذا يحرس الحلقة التالية:
// ‏`schema.d.ts` من أن ينحرف عن `openapi.json`.
//
// بلا هذا الحارس تبقى السلسلة مقطوعة عند آخر وصلة: من يعيد توليد العقد ولا يشغّل
// ‏`npm run generate:api-types` يترك الأنواع قديمة **ولا شيء يفشل**. وكان ذلك بلا
// ضرر يوم كانت الأنواع تقول `AccountType: number` — فـ`number` لا يحمل معلومة تبلى.
// ومتى صار `1 | 2 | 3 | 4 | 5` صار النوع **حاملاً لمدى**، وقِدَمه يعني أن الواجهة
// تصدّق مدى ملغى وتمنع قيمة صار الخادم يقبلها، أو تُجيز قيمة صار يرفضها.
//
// **الاحتياط الذي أنقذ M01 مطبَّق هنا حرفياً:** التوليد يقع في مسار مؤقت، ولا يمسّ
// الملف المتعقَّب أبداً. لو ولّد الحارسُ فوق ما يقارن به لَمُحي الانحراف قبل أن يُقاس،
// ولَعجز الاختبار عن الفشل أبداً.
//
// ويستدعي `generate-api-types.mjs` نفسه الذي يستدعيه أمر npm — مولِّد واحد لا اثنان.
public class ApiTypesContractTests
{
    private const int MaxReportedDifferences = 12;

    private const int GeneratorTimeoutMilliseconds = 120_000;

    // M04
    [Fact]
    public void M04_CommittedApiTypes_MatchTypesGeneratedFromCurrentContract()
    {
        var committedPath = ContractPaths.ResolveApiTypesPath();

        Assert.True(File.Exists(committedPath),
            $"ملف الأنواع المولَّدة غير موجود: {committedPath}. شغّل npm run generate:api-types.");

        var temporaryPath = Path.Combine(
            Path.GetTempPath(), $"erp-api-types-{Guid.CreateVersion7():N}.d.ts");

        try
        {
            RunGenerator(temporaryPath);

            var committed = ReadLines(committedPath);
            var generated = ReadLines(temporaryPath);

            var differences = CollectDifferences(committed, generated);

            Assert.True(differences.Count == 0,
                "الأنواع المعتمَدة تخالف ما يولّده العقد الحالي — شغّل npm run generate:api-types "
                + "داخل erp_frontend واعتمد الفرق:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, differences.Take(MaxReportedDifferences))
                + (differences.Count > MaxReportedDifferences
                    ? $"{Environment.NewLine}... و{differences.Count - MaxReportedDifferences} اختلافاً آخر"
                    : string.Empty));
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    // غياب Node أو node_modules **يُرسب** الاختبار ولا يتخطّاه. التخطّي هنا هو نمط
    // ‏`[Fact(Skip)]` المرفوض في تمهيد §4.4 بعينه: يفشل مفتوحاً، فتمرّ التشغيلة خضراء
    // على جهاز لا يفحص شيئاً — وهو أسوأ من غياب الحارس لأنه يوهم بوجوده
    private static void RunGenerator(string outputPath)
    {
        var scriptPath = ContractPaths.ResolveApiTypesGeneratorPath();

        Assert.True(File.Exists(scriptPath),
            $"مولِّد الأنواع غير موجود: {scriptPath}.");

        var startInfo = new ProcessStartInfo("node")
        {
            ArgumentList = { scriptPath, outputPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = StartOrFail(startInfo);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();

        Assert.True(process.WaitForExit(GeneratorTimeoutMilliseconds),
            $"لم ينتهِ مولِّد الأنواع خلال {GeneratorTimeoutMilliseconds / 1000} ثانية.");

        Assert.True(process.ExitCode == 0,
            $"فشل مولِّد الأنواع برمز {process.ExitCode}:"
            + Environment.NewLine + standardError + standardOutput);
    }

    private static Process StartOrFail(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo)
                   ?? throw new InvalidOperationException("تعذّر بدء عملية node.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "تعذّر تشغيل node. هذا الحارس يتطلب Node ومجلد node_modules في erp_frontend "
                + "(شغّل npm ci هناك). ولا يُتخطّى عند غيابهما: التخطّي يفشل مفتوحاً.",
                exception);
        }
    }

    // التطبيع على نهايات الأسطر وحدها. مخرج المولِّد حتمي بايتاً ببايت — مقيس
    // بتشغيلتين متتاليتين على العقد نفسه — فلا حاجة لأكثر من ذلك، وأي تساهل زائد
    // يخفي انحرافاً حقيقياً
    private static string[] ReadLines(string path) =>
        File.ReadAllText(path).ReplaceLineEndings("\n").Split('\n');

    // **الفصل بين ما يحكم وما يُقرأ:** الحكم تطابق تسلسلي صارم — أي فرق مهما صغر
    // يُرسب. أما التقرير فبفرق المجموعات، لأن المقارنة الموضعية تحوّل ستة أسطر مُدرَجة
    // إلى مئات «الاختلافات» بإزاحة كل ما بعدها. وتقرير لا يُقرأ يُدرَّب على تجاهله،
    // فيصير الحارس حاضراً بالاسم غائباً بالأثر
    private static List<string> CollectDifferences(string[] committed, string[] generated)
    {
        if (committed.SequenceEqual(generated, StringComparer.Ordinal))
        {
            return [];
        }

        var firstDivergence = Enumerable
            .Range(0, Math.Min(committed.Length, generated.Length))
            .FirstOrDefault(i => !string.Equals(committed[i], generated[i], StringComparison.Ordinal),
                Math.Min(committed.Length, generated.Length));

        var differences = new List<string>
        {
            $"  أول اختلاف عند السطر {firstDivergence + 1}"
                + $" (المعتمَد {committed.Length} سطراً، المولَّد {generated.Length})"
        };

        differences.AddRange(Excess(generated, committed).Select(entry => $"  ينقص المعتمَد: {entry}"));
        differences.AddRange(Excess(committed, generated).Select(entry => $"  زائد في المعتمَد: {entry}"));

        return differences;
    }

    // الأسطر التي يزيد بها الطرف الأول على الثاني، بعددها. والفارغة والأقواس تتساوى
    // في الطرفين فتسقط من التقرير تلقائياً، ولا يبقى إلا ما تغيّر فعلاً
    private static IEnumerable<string> Excess(string[] source, string[] other)
    {
        var otherCounts = other.GroupBy(line => line, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return source.GroupBy(line => line, StringComparer.Ordinal)
            .Select(group => (group.Key, Excess: group.Count() - otherCounts.GetValueOrDefault(group.Key)))
            .Where(entry => entry.Excess > 0)
            .Select(entry => entry.Excess == 1
                ? $"«{entry.Key.Trim()}»"
                : $"«{entry.Key.Trim()}» ×{entry.Excess}");
    }
}
