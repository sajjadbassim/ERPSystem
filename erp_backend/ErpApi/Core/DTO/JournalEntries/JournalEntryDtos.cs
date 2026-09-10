using System.ComponentModel.DataAnnotations;
using ErpApi.Core.Constants;

namespace ErpApi.Core.DTO.JournalEntries;

// R-AMT-02: الحقول الخمسة الإلزامية لكل سطر مالي. لا سطر بلا عملته وسعره وتاريخ سعره
public class JournalLineCreateDto
{
    [Required(ErrorMessage = "الحساب مطلوب.")]
    public Guid AccountId { get; set; }

    [MaxLength(500, ErrorMessage = "وصف السطر أطول من الحد المسموح.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "عملة السطر مطلوبة.")]
    public Guid CurrencyId { get; set; }

    [Required(ErrorMessage = "سعر الصرف مطلوب.")]
    public decimal ExchangeRate { get; set; }

    [Required(ErrorMessage = "تاريخ سعر الصرف مطلوب.")]
    public DateOnly ExchangeRateDate { get; set; }

    public decimal DebitFC { get; set; }
    public decimal CreditFC { get; set; }

    // DebitBase و CreditBase **ليسا هنا بقصد** — إنفاذاً لـ R-API-01 [صارم]:
    // «الواجهة لا تحسب، لا تحوّل عملة… تعرض ما يصلها فقط».
    //
    // كانا حقلين في هذا الـDTO، فكان العقد **يطلب من الواجهة أن تضرب المبلغ في
    // السعر** — وهو نصّ ما تمنعه القاعدة. والقاعدة تتحقق من الناتج (الخطأ 50015)
    // فلا يمرّ خطأ، لكن **مصدر** الرقم كان الواجهة.
    //
    // وإسقاطهما يجعل الخرق **مستحيلاً بنيوياً** لا مرفوضاً بالانضباط — منطق R-GL-05
    // نفسه (DENY في القاعدة بدل الاعتماد على الالتزام). وحقلٌ اختياري يُحسب عند غيابه
    // كان سيُبقي الواجهة قادرة على الحساب، أي يفشل مفتوحاً.
    //
    // يحسبهما JournalEntryService.PostAsync، وتُرجعهما JournalLineResponseDto تأكيداً.
}

public class PostJournalEntryRequestDto
{
    [Required(ErrorMessage = "الفرع مطلوب.")]
    public Guid BranchId { get; set; }

    [Required(ErrorMessage = "تاريخ الترحيل مطلوب.")]
    public DateOnly PostingDate { get; set; }

    // اختياري ويسقط إلى تاريخ الترحيل. إلزامه هنا يجعل التحقق من الشكل يسبق فحص
    // التخويل، فيُرد طلبٌ من فرع خارج النطاق بـ 400 بدل 403 — ويعرف المُرسِل
    // أن حمولته ناقصة قبل أن يُمنع أصلاً (الحارس G03)
    public DateOnly? DocumentDate { get; set; }

    [MaxLength(500, ErrorMessage = "وصف القيد أطول من الحد المسموح.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "نوع المصدر مطلوب.")]
    public JournalSourceModule SourceModule { get; set; } = JournalSourceModule.Manual;

    // بلا MinLength: **عدد** السطور يفحصه الإجراء المخزَّن (50016 و50017) وهو الحارس
    // المعتبر (R-GL-05). تكراره هنا يزيد نسخة ثانية من القاعدة تنحرف بصمت،
    // ويُقدّم التحقق من الشكل على فحص النطاق.
    //
    // أما Required فلوجود القائمة لا لعددها. المُهيِّئ `= []` يغطي **غياب** الحقل وحده،
    // لأن System.Text.Json يستدعي الواضع بـ null حين يحضر الحقل بقيمة null فيمحوه.
    // و MVC يرفض ذلك ضمناً (مرجع غير قابل للعدم) — لكن برسالة إنجليزية.
    // التصريح هنا لاستبدال الرسالة بالعربية لا لتغيير السلوك (بند 8.4). الحارس L34
    [Required(ErrorMessage = "سطور القيد مطلوبة.")]
    public List<JournalLineCreateDto> Lines { get; set; } = [];
}

public class ReverseJournalEntryRequestDto
{
    // تاريخ العكس وفترته مستقلان تماماً عن الأصل (R-GL-03-b)
    [Required(ErrorMessage = "تاريخ الترحيل مطلوب.")]
    public DateOnly PostingDate { get; set; }

    [Required(ErrorMessage = "تاريخ المستند مطلوب.")]
    public DateOnly DocumentDate { get; set; }

    [MaxLength(500, ErrorMessage = "وصف العكس أطول من الحد المسموح.")]
    public string? Description { get; set; }
}

// R-API-02: لا رقم عارٍ بلا هويته. كل مبلغ هنا معه عملته ورمزها وسعره وتاريخه
public class JournalLineResponseDto
{
    public required Guid Id { get; init; }
    public required short LineNumber { get; init; }

    public required Guid AccountId { get; init; }
    public required string AccountCode { get; init; }
    public required string AccountName { get; init; }

    public required Guid CurrencyId { get; init; }
    public required string CurrencyCode { get; init; }

    public required decimal ExchangeRate { get; init; }
    public required DateOnly ExchangeRateDate { get; init; }

    public required decimal DebitFC { get; init; }
    public required decimal CreditFC { get; init; }
    public required decimal DebitBase { get; init; }
    public required decimal CreditBase { get; init; }

    public string? Description { get; init; }
}

public class JournalEntryResponseDto
{
    public required Guid Id { get; init; }
    public required string DocumentNumber { get; init; }
    public required Guid TransactionId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid FiscalPeriodId { get; init; }
    public required DateOnly PostingDate { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public required JournalSourceModule SourceModule { get; init; }
    public Guid? ReversalOfJournalEntryId { get; init; }
    public string? Description { get; init; }

    public List<JournalLineResponseDto> Lines { get; init; } = [];
}

// عنصر القائمة بلا سطور: تحميلها لكل صف في صفحة يجعل الاستعلام يتضخم بلا داع
public class JournalEntryListItemDto
{
    public required Guid Id { get; init; }
    public required string DocumentNumber { get; init; }
    public required Guid BranchId { get; init; }
    public required DateOnly PostingDate { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public required JournalSourceModule SourceModule { get; init; }
    public Guid? ReversalOfJournalEntryId { get; init; }
    public string? Description { get; init; }
}
