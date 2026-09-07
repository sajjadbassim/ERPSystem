using System.Reflection;
using ErpApi.Data;
using ErpApi.Tests.Infrastructure;

namespace ErpApi.Tests.Accounting;

// ‏R-GL-05: الكتابة عبر الإجراءين حصراً. الحارس D01 يفرض ذلك في القاعدة بـ DENY،
// وهذه المجموعة تفرضه في **شكل الكود**: مسار كتابة لا يوجد أصلاً لا يُستدعى سهواً.
//
// الفرق عملي: DENY يمنع الكتابة وقت التشغيل بخطأ صلاحية غامض، وغياب الميثود
// يمنعها وقت الترجمة. الحارسان معاً يجعلان الالتفاف مستحيلاً لا صعباً.
public class JournalReadOnlyStructureTests(TestDatabase database) : IdentityTestBase(database)
{
    private static readonly string[] MutatingPrefixes = ["Add", "Update", "Remove", "Delete", "Insert"];

    private static List<Type> RepositoryTypesFor(string entityName) =>
        [.. typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => t.Name.Contains($"{entityName}Repository", StringComparison.Ordinal))];

    private static List<string> MutatingMembers(IEnumerable<Type> types) =>
        [.. types
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => MutatingPrefixes.Any(p => m.Name.StartsWith(p, StringComparison.Ordinal)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")];

    // L20
    [Fact]
    public void L20_JournalEntryRepository_ExposesNoMutatingMember()
    {
        var types = RepositoryTypesFor("JournalEntry");

        Assert.True(types.Count > 0, "لا مستودع لـ JournalEntry — لم يُبنَ بعد");

        var mutating = MutatingMembers(types);

        Assert.True(mutating.Count == 0,
            $"مسار كتابة على كيان قراءة فقط: {string.Join(", ", mutating)}");
    }

    // L21
    [Fact]
    public void L21_JournalLineRepository_ExposesNoMutatingMember()
    {
        var types = RepositoryTypesFor("JournalLine");

        Assert.True(types.Count > 0, "لا مستودع لـ JournalLine — لم يُبنَ بعد");

        var mutating = MutatingMembers(types);

        Assert.True(mutating.Count == 0,
            $"مسار كتابة على كيان قراءة فقط: {string.Join(", ", mutating)}");
    }
}
