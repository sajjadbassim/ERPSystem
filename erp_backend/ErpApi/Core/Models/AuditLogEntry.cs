namespace ErpApi.Core.Models;

// سطر تدقيقي دائم لا كيان دورة حياة: بلا IsActive وبلا IsDeleted وبلا Query Filter،
// ولا يُعدَّل ولا يُحذف بعد إدراجه. سجل قابل للتحرير يوثّق ما يريده المحرِّر لا ما جرى.
//
// لا يطبّق ICreationAuditable ولا IAuditableEntity عمداً: UserId هو الفاعل وCreatedAt
// هو الزمن، فحقول تدقيق ثانية فوقهما تكرار. والأهم أن بقاءه خارج الواجهتين يُبقيه
// خارج مسار Interceptor التدقيق نفسه، فلا يُدقَّق السجل بسجل.
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // الفاعل: من نفّذ العملية، لا من وقعت عليه. المتأثِّر يُعرف من EntityKey
    public Guid UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    // نصّ لا مفتاح أجنبي: جداول الربط تُحذف فعلياً، والمفتاح كان سيمنع الحذف
    // أو يجرّ السجل معه. مركّب بفاصل | لجداول الربط
    public string EntityKey { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
