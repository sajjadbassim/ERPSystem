using System.Text;

namespace ErpApi.ContractTool;

// الكاتب الوحيد للملف المعتمَد، وهو تنفيذيّ مستقل عمداً: مشغّل الاختبارات يشغّل
// تجميعات اختبار، وهذه ليست واحدة منها — فلا مرشِّح يُنسى ولا وسم يُحذف سهواً.
// الحاجز بين «تحديث الحقيقة» و«المقارنة بالحقيقة» بنيوي لا إجرائي.
internal static class ContractWriter
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var outputPath = args.Length > 0
                ? Path.GetFullPath(args[0])
                : ContractPaths.ResolveDocumentPath();

            var json = await OpenApiDocumentProducer.ProduceAsync();

            // UTF-8 بلا BOM: الملف يستهلكه مولّد أنواع من npm، وبعض المحلّلات
            // تتعثّر بعلامة الترتيب في مستهلّ JSON
            await File.WriteAllTextAsync(outputPath, json, new UTF8Encoding(false));

            Console.WriteLine($"كُتب عقد الـ API: {outputPath}");
            Console.WriteLine($"الطول: {json.Length} محرفاً");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"فشل إخراج العقد: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
