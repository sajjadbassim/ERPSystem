# قواعد تصميم الـ Backend الموحّدة

> **تعليمات لعميل الذكاء الاصطناعي:** هذا الملف هو المعيار الموحّد الملزِم لأي كود Backend في هذا المشروع (ASP.NET Core Web API). الهدف: كل مشروع بنفس البنية، نفس التسمية، نفس أسلوب معالجة الأخطاء والاستجابات — بغض النظر عن الدومين (تجارة إلكترونية، ERP، أنظمة مراقبة...).
>
> اقرأ الملف بالكامل قبل كتابة أي كود. إذا خالف الكود القائم هذه القواعد، لا تُصلح كل شيء دفعة واحدة — التزم بالقواعد في الكود الجديد فقط، وأشِر إلى المخالفات إن طُلب منك ذلك صراحة.
>
> عند تعارض ظاهري بين بندين، الأولوية للبند الأكثر تحديداً، ثم لما هو مطبَّق فعلاً في المشروع (بند 2.1).
>
> **قاعدة الأسبقية مع قواعد الدومين المالي:** في المشاريع المحاسبية، يُقرأ هذا الملف مع `ERP-CORE-RULES.md` — **نسخته الوحيدة على جذر المستودع: `docs/ERP-CORE-RULES.md`، لا في هذا المجلد**. عند التعارض في أي أمر يمسّ البيانات المالية — المبالغ، العملات، أسعار الصرف، القيود، الدفعات، التكلفة، دورة حياة المستند المالي — **الأسبقية لقواعد ERP**، وهذا يشمل تجاوز البند 2.1 والبند 2.2 داخل هذا النطاق. حقول البنية المالية (العملة، سعر الصرف، `AmountBase`، `TransactionId`) ليست «سيناريو افتراضي» بمعنى البند 2.2 بل عقد بيانات إلزامي يُبنى منذ الجدول الأول. السبب: مخالفة قاعدة معمارية تنتج كوداً يُصلَح لاحقاً، ومخالفة قاعدة مالية تنتج بيانات لا تُصلَح.

---

## 1. التقنيات الأساسية (Stack)

- **ASP.NET Core Web API** (أحدث نسخة LTS) + C#، مع `<Nullable>enable</Nullable>` و `<TreatWarningsAsErrors>` مفعّلين.
- **Entity Framework Core** كـ ORM، مع SQL Server ما لم يُحدَّد غير ذلك.
- **JWT Bearer** للمصادقة، مع Policies قائمة على Roles للصلاحيات.
- **AutoMapper** أو Mapping يدوي بسيط — استخدم نفس الأسلوب المتبع في المشروع القائم، ولا تُدخل مكتبة جديدة بدون داعٍ واضح.
- **Serilog** (أو `ILogger` الافتراضي) للتسجيل، بصيغة Structured Logging.
- **xUnit** للاختبارات.

---

## 2. مبادئ عامة

1. **الاتساق أهم من "الأفضل نظرياً"**: عند التردد بين طريقتين صحيحتين، اختر المستخدمة فعلاً في بقية المشروع.
2. **لا تُصمّم لسيناريوهات افتراضية**: لا Interfaces ولا Abstractions ولا Feature Flags لأشياء غير مطلوبة. الاستثناء الوحيد المسموح هو الـ Interfaces المنصوص عليها في بند 4 (لها مبرر: الاختبارات + الاتساق).
3. **الطبقة الرقيقة**: الـ Controller لا يحتوي منطق عمل إطلاقاً — استقبال الطلب، استدعاء الـ Service، إرجاع الاستجابة.
4. **كل شيء Async**: أي عملية I/O تكون `async`/`await`، ترجع `Task`/`Task<T>`، تنتهي بـ `Async` في الاسم، **وتستقبل `CancellationToken` وتمرّره للطبقة التالية حتى `DbContext`**. ممنوع `.Result` أو `.Wait()` أو `async void`.
5. **لا تعليقات زائدة**: التعليقات بالعربية مسموحة لشرح قاعدة عمل غير واضحة (مثال: `// السعر يشمل الضريبة`). ممنوع تعليقات تشرح الكود الواضح، وممنوع رموز تعبيرية داخل الكود أو التعليقات.
6. **الأسرار لا تُكتب في الكود**: راجع بند 18.

---

## 3. هيكل المجلدات القياسي

```
{ProjectName}/
├── {ProjectName}.csproj
├── Program.cs
├── appsettings.json
├── Controllers/
│   └── {Feature}Controller.cs
├── Core/
│   ├── Models/                  # Entities
│   ├── Constants/               # Enums / ثوابت (OrderStatus, UserRoles, PolicyNames...)
│   ├── Exceptions/              # Custom Exceptions
│   ├── Interfaces/              # IUnitOfWork, IFileService, ICurrentUserService...
│   └── DTO/
│       └── {Feature}/
│           ├── {Feature}CreateDto.cs
│           ├── {Feature}UpdateDto.cs
│           └── {Feature}ResponseDto.cs
├── Common/
│   ├── ApiResponse.cs
│   ├── PaginationParams.cs
│   └── PagedResponse.cs
├── Data/
│   ├── AppDbContext.cs
│   ├── UnitOfWork.cs
│   ├── Interceptors/
│   │   └── AuditableEntityInterceptor.cs
│   └── Configurations/          # IEntityTypeConfiguration<T> لكل Entity
├── Repositories/
│   ├── I{Feature}Repository.cs
│   └── {Feature}Repository.cs
├── Services/
│   ├── {Feature}/
│   │   ├── I{Feature}Service.cs
│   │   └── {Feature}Service.cs
│   └── Files/
│       ├── IFileService.cs
│       └── FileService.cs
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── ApplicationBuilderExtensions.cs
└── Migrations/
```

قواعد تسمية الملفات: **بدون مسافات نهائياً** (خطأ: `Address Repository .cs`)، بدون امتداد مكرر (`.cs.cs`)، واسم الملف = اسم الكلاس بالضبط. ملف واحد = نوع واحد عام.

---

## 4. طبقات المعمارية

```
Controller → Service (I + Impl) → Repository (I + Impl) → AppDbContext
                  ↓
             IUnitOfWork (SaveChanges / Transaction)
```

### Repository
- **وصول للبيانات فقط**: استعلامات + `Add`/`Update`/`Remove`. بدون أي قرار عمل.
- **لا يستدعي `SaveChangesAsync` إطلاقاً** — الحفظ مسؤولية الـ Service عبر `IUnitOfWork` (بند 7).
- **لا يُخرج `IQueryable` خارج حدوده** — يُرجع `T`, `List<T>`, `bool`, `int` فقط. سبب المنع: تسريب `IQueryable` ينقل منطق الاستعلام إلى الـ Service ويجعل الـ Repository بلا قيمة.
- **يُسمح بتمرير `Expression<Func<T, bool>>`** كـ predicate بدل تفريخ ميثودات مثل `GetByNameAndCategoryAndActiveAsync`.
- استعلامات القراءة فقط تستخدم `AsNoTracking()`.
- لكل Repository واجهة تبدأ بـ `I`.

```csharp
public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Category>> GetPagedAsync(PaginationParams pagination, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<Category, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<Category, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(Category entity, CancellationToken ct = default);
    void Update(Category entity);
    void Remove(Category entity);
}
```

> ملاحظة: `Update` و `Remove` ليست `async` لأنها لا تلمس قاعدة البيانات — تُعدّل الـ ChangeTracker فقط. هذا استثناء مقصود من بند 2.4.

### Service
- يحتوي كل منطق العمل: التحقق من قواعد العمل، الحسابات، رفع الملفات، الإشعارات.
- يستدعي Repository واحداً أو أكثر، ثم **يستدعي `SaveChangesAsync` مرة واحدة** في نهاية العملية.
- يرمي Custom Exceptions عند الخطأ (بند 6).
- **لا يُرجع Entity أبداً** — يحوّل إلى `ResponseDto` قبل الإرجاع.
- لا يعرف شيئاً عن `HttpContext` أو `ActionResult` أو رموز الحالة.

### Controller
- يستدعي الـ Service فقط، يغلّف الناتج بـ `ApiResponse<T>`، **لا يحتوي `try/catch` إطلاقاً**.
- يحدّد رمز الحالة الصحيح (بند 6.2).

### التسجيل في DI
كل Repository و Service يُسجَّل `Scoped`.

---

## 5. قواعد التسمية

| العنصر | القاعدة | مثال |
|---|---|---|
| Controller | `{Feature}Controller` (جمع) | `CategoriesController` |
| Service | `I{Feature}Service` / `{Feature}Service` | `ICategoryService` |
| Repository | `I{Feature}Repository` / `{Feature}Repository` | `ICategoryRepository` |
| DTO | `{Feature}CreateDto` / `UpdateDto` / `ResponseDto` | `CategoryCreateDto` |
| Private field | `_camelCase` | `_categoryService` |
| Async method | ينتهي بـ `Async` | `GetByIdAsync` |
| Primary Key | `Guid Id` ما لم يوجد سبب لـ `int` (بند 12) | — |
| Custom Exception | ينتهي بـ `Exception` | `NotFoundException` |
| Policy | ثابت في `Core/Constants/PolicyNames.cs` | `PolicyNames.AdminOnly` |
| Migration | PascalCase تصف الفعل | `AddVendorCoverImage` |

ممنوع أسماء Policies أو Roles كنصوص حرفية متناثرة في الكود — دائماً ثوابت.

---

## 6. نمط الاستجابة الموحّد

### 6.1 الشكل

كل استجابة — نجاح أو فشل — تمرّ عبر `ApiResponse<T>`، وليس Anonymous Object يُبنى يدوياً:

```csharp
// Common/ApiResponse.cs
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, string? traceId = null) =>
        new() { Success = false, Message = message, TraceId = traceId };
}
```

`TraceId` يُملأ في استجابات الفشل فقط، ليُربط بلاغ المستخدم بسجلات الخادم.

### 6.2 رموز الحالة

| العملية | الرمز | الميثود |
|---|---|---|
| قراءة ناجحة | 200 | `Ok(...)` |
| إنشاء ناجح | 201 | `CreatedAtAction(...)` |
| تحديث ناجح يُرجع بيانات | 200 | `Ok(...)` |
| حذف / تحديث بلا محتوى | 204 | `NoContent()` |

**ملاحظة**: استجابة 204 لا تحمل body، وهذا استثناء مقبول من قاعدة "كل استجابة `ApiResponse<T>`". إن كان العميل (Frontend) يشترط body موحّداً، أرجِع 200 مع `ApiResponse<object>.Ok(null)` والتزم بذلك في كل المشروع.

```csharp
[HttpGet("{id:guid}")]
public async Task<ActionResult<ApiResponse<CategoryResponseDto>>> GetById(Guid id, CancellationToken ct)
{
    var category = await _categoryService.GetByIdAsync(id, ct);
    return Ok(ApiResponse<CategoryResponseDto>.Ok(category));
}

[HttpPost]
public async Task<ActionResult<ApiResponse<CategoryResponseDto>>> Create(CategoryCreateDto dto, CancellationToken ct)
{
    var created = await _categoryService.CreateAsync(dto, ct);
    return CreatedAtAction(nameof(GetById), new { id = created.Id },
        ApiResponse<CategoryResponseDto>.Ok(created, "تم إنشاء التصنيف بنجاح"));
}
```

### 6.3 التحقق من المدخلات (مهم)

`[ApiController]` يُرجع تلقائياً `ValidationProblemDetails` بشكل **مخالف** لـ `ApiResponse<T>`، ولا يمرّ عبر الـ Middleware لأنه ليس Exception. لذلك **يجب** تجاوزه في `Program.cs`:

```csharp
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join(" | ", context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));

        return new BadRequestObjectResult(
            ApiResponse<object>.Fail(message, context.HttpContext.TraceIdentifier));
    };
});
```

### 6.4 الترقيم

القوائم المُقسّمة صفحات تُرجع دائماً `ApiResponse<PagedResponse<T>>` — راجع بند 10.

---

## 7. الحفظ والمعاملات (SaveChanges & Transactions)

**القاعدة**: الـ Repository يُجهّز التغييرات، والـ Service يحفظها. مرة واحدة، في نهاية العملية.

```csharp
// Core/Interfaces/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}
```

```csharp
// Data/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await operation();
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
```

- **عملية على Repository واحد**: يكفي `await _unitOfWork.SaveChangesAsync(ct);` — الـ `SaveChanges` نفسه معاملة ضمنية.
- **عملية على أكثر من Repository، أو تتضمّن استدعاءً خارجياً بينها** (رفع ملف، استدعاء API): استخدم `ExecuteInTransactionAsync`.
- ممنوع استدعاء `SaveChangesAsync` أكثر من مرة داخل عملية عمل واحدة بدون معاملة صريحة.
- **العمليات غير القابلة للتراجع** (رفع ملف، إرسال بريد) تُنفَّذ **بعد** نجاح الحفظ، لا قبله. إن تعذّر ذلك، فالـ Service مسؤول عن التنظيف عند الفشل.

---

## 8. معالجة الأخطاء المركزية

### 8.1 الاستثناءات

```csharp
// Core/Exceptions/
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public class NotFoundException : AppException          // 404
{
    public NotFoundException(string message) : base(message) { }
}

public class BusinessRuleException : AppException      // 400 — قاعدة عمل مخالَفة
{
    public BusinessRuleException(string message) : base(message) { }
}

public class ConflictException : AppException          // 409 — تعارض حالة أو تكرار
{
    public ConflictException(string message) : base(message) { }
}

public class UnauthorizedException : AppException      // 401 — بيانات اعتماد خاطئة
{
    public UnauthorizedException(string message) : base(message) { }
}

public class ForbiddenException : AppException         // 403 — مسجّل دخول لكن لا يملك الصلاحية
{
    public ForbiddenException(string message) : base(message) { }
}
```

**التمييز المهم**: التكرار (اسم موجود مسبقاً) هو `ConflictException` وليس `BusinessRuleException`. القاعدة: خطأ في *مدخلات* الطلب → 400، تعارض مع *حالة* المورد → 409.

### 8.2 الـ Middleware

```csharp
// Middleware/ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;

            var (statusCode, message) = ex switch
            {
                NotFoundException      => (StatusCodes.Status404NotFound, ex.Message),
                BusinessRuleException  => (StatusCodes.Status400BadRequest, ex.Message),
                ConflictException      => (StatusCodes.Status409Conflict, ex.Message),
                UnauthorizedException  => (StatusCodes.Status401Unauthorized, ex.Message),
                ForbiddenException     => (StatusCodes.Status403Forbidden, ex.Message),
                _ => (StatusCodes.Status500InternalServerError,
                      $"حدث خطأ غير متوقع في الخادم. الرجاء ذكر الرقم التالي عند التواصل مع الدعم: {traceId}")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}, Path: {Path}",
                    traceId, context.Request.Path);
            else
                _logger.LogWarning("Handled exception {ExceptionType}. TraceId: {TraceId}, Message: {Message}",
                    ex.GetType().Name, traceId, ex.Message);

            if (context.Response.HasStarted)
                return;

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message, traceId));
        }
    }
}
```

### 8.3 الترتيب في الـ Pipeline

الـ Middleware يُسجَّل **أول شيء** في الـ pipeline (بعد `UseSerilogRequestLogging` إن وُجد)، وليس فقط قبل `UseAuthentication` — وإلا لن يلتقط استثناءات ما قبله:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("Default");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

### 8.4 قواعد اللغة

رسائل الأخطاء الموجّهة للمستخدم **بالعربية**. رسائل الـ Log الداخلية وأسماء الحقول في Structured Logging **بالإنجليزية**. ممنوع تسريب `ex.Message` الخام أو `StackTrace` للعميل في أخطاء 500.

---

## 9. الـ DTOs

- منظّمة في `Core/DTO/{Feature}/`.
- ثلاثة أنواع: `CreateDto` (إدخال الإنشاء)، `UpdateDto` (حقول Nullable للتحديث الجزئي)، `ResponseDto` (المُرجَع للعميل).
- **التحقق من شكل البيانات** عبر `DataAnnotations` على الـ DTO مباشرة (`[Required]`, `[MaxLength]`, `[Range]`, `[EmailAddress]`) — وليس داخل الـ Service.
- **التحقق من قواعد العمل** داخل الـ Service فقط (مثال: "الاسم مكرر"، "الرصيد لا يكفي").
- رسائل `DataAnnotations` بالعربية عبر `ErrorMessage`.
- الـ `ResponseDto` لا يحتوي حقولاً حساسة (كلمات مرور، Hash، مفاتيح داخلية).
- ممنوع استخدام Entity كمدخل لـ Action أو كمخرج منه.

---

## 10. الترقيم (Pagination)

```csharp
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : Math.Min(value, MaxPageSize);
    }
}

public class PagedResponse<T>
{
    public List<T> Data { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
}
```

- `PagedResponse<T>` **لا يحتوي حقل `Success`** — هو دائماً مُغلَّف داخل `ApiResponse<PagedResponse<T>>` حتى يبقى شكل الاستجابة واحداً.
- كل Endpoint يُرجع قائمة قابلة للنمو يجب أن يدعم Pagination. ممنوع `GetAll()` بدون حدّ على جداول تنمو مع الاستخدام.
- الترتيب (`OrderBy`) **إلزامي** قبل `Skip`/`Take` وإلا فالنتائج غير مضمونة.

### 10.1 استثناء: التقارير المالية

حد `MaxPageSize` والمعمارية `Controller → Service → Repository` **لا تنطبقان على تقارير القراءة المالية** (ميزان المراجعة، دفتر الأستاذ، أعمار الديون، كشف حساب، تقييم المخزون). هذه التقارير:

- تُنفَّذ في `Services/Reports/` كـ **Read Services** عبر استعلامات مباشرة أو Views، وتُرجع DTO مباشرة دون المرور بـ Repository.
- **لا تُبنى بتجميع كيانات في الذاكرة**. التجميع في قاعدة البيانات.
- نقاط نهاية التصدير تدعم البث (streaming) دون حد صفحات.

هذا لا يخالف بند 20 لأن الـ `IQueryable` لا يغادر طبقة القراءة. المصدر الوحيد لأرقام التقرير المحاسبي هو الدفتر المُرحَّل، لا قيم المستندات.

---

## 11. الحذف الناعم وحالة الكيان

فصل واضح بين مفهومين كثيراً ما يُخلط بينهما:

| الحقل | المعنى | من يراه |
|---|---|---|
| `IsDeleted` | محذوف منطقياً، غير موجود بالنسبة للنظام | لا أحد (إلا الأدمن صراحة) |
| `IsActive` | موجود لكن معطّل مؤقتاً (منتج غير متاح، مستخدم موقوف) | يظهر في لوحة الإدارة، يُخفى عن العميل |

- الحذف الناعم يُطبَّق عبر **Global Query Filter** وليس بشرط يدوي في كل استعلام:

```csharp
modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
```

- لاسترجاع المحذوف (شاشات الأدمن): `.IgnoreQueryFilters()` داخل ميثود Repository مخصّصة وصريحة الاسم (`GetByIdIncludingDeletedAsync`).
- انتبه: الـ Query Filter لا يُطبَّق على `Find()` ولا على العلاقات المُحمّلة بـ `Include` من كيان غير مفلتر — تحقّق من ذلك عند الجداول المرتبطة.
- **الحذف الفعلي** (`DELETE`) يُستخدم فقط للسجلات التابعة التي لا معنى لبقائها (سطور سلة، عنوان شحن)، أو بطلب صريح من المستخدم.

### 11.1 استثناء إلزامي: الكيانات المالية المرحَّلة

الكيانات المالية المرحَّلة — `JournalEntry`, `JournalLine`, `Payment`, `PaymentAllocation`, `StockMovement`, وما شابهها — **لا تحمل حقل `IsDeleted` إطلاقاً، ولا Query Filter، ولا تُحذف فعلياً**.

السبب: الـ Query Filter يُخفي القيد عن الاستعلام دون أن يُخفي أثره، فينتج ميزان مراجعة «متوازن» شكلاً وخاطئ محاسبياً. والرقم الصحيح عددياً والخاطئ محاسبياً أسوأ من غياب الرقم.

- الإلغاء يتم **بعكس القيد** في معاملة ذرية، لا بحذفه ولا بتعطيله.
- استثناء «السجلات التابعة» أعلاه **لا ينطبق** على `JournalLine` ولا `PaymentAllocation`.
- حقل `IsActive` يبقى مسموحاً على البيانات الأساسية (حساب، صنف، عميل) بمعناه المعتاد: معطّل لا محذوف.

---

## 12. المفاتيح والطوابع الزمنية

### 12.1 المفتاح الأساسي

`Guid Id` هو الافتراضي، لكن **ممنوع `Guid.NewGuid()` كمفتاح Clustered في SQL Server** لأن العشوائية تسبب تشظّي فهارس شديداً. استخدم أحد الخيارين:

```csharp
public Guid Id { get; set; } = Guid.CreateVersion7();   // ‎.NET 9+‎ — مرتّب زمنياً
```

أو على مستوى قاعدة البيانات:

```csharp
builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
```

### 12.2 التدقيق (Auditing)

كل Entity رئيسي يطبّق:

```csharp
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
```

القيم تُضبط **تلقائياً عبر Interceptor**، وليس يدوياً في الـ Repository (لأن النسيان حتمي مع تعدد الـ Repositories):

```csharp
// Data/Interceptors/AuditableEntityInterceptor.cs
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return base.SavingChangesAsync(eventData, result, ct);

        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

- الأعمدة الزمنية في SQL Server تكون `datetime2` وليس `datetime`.

### 12.3 فصل إلزامي: تواريخ تقنية مقابل تواريخ محاسبية

**لا تُطبَّق قاعدة UTC على التواريخ المحاسبية.** النوعان منفصلان تماماً:

| النوع | أمثلة | النوع البرمجي | العمود |
|---|---|---|---|
| تقني | `CreatedAt`, `UpdatedAt` | `DateTime` بالـ UTC | `datetime2` |
| محاسبي | `PostingDate`, `DocumentDate`, `RateDate`, `DueDate` | `DateOnly` | `date` |

السبب: عملية تحدث في بغداد الساعة 2:00 صباحاً في الأول من الشهر تصبح بالـ UTC في 11:00 مساءً من الشهر السابق — أي في فترة مالية مختلفة، ربما مقفلة. التاريخ المحاسبي حقيقة تجارية لا لحظة زمنية، ولا يحمل وقتاً ولا منطقة زمنية ولا يخضع لأي تحويل.

- الفترة المالية تُحدَّد من `PostingDate` **حصراً**، ولا تُشتق من `CreatedAt` أبداً.
- `UpdatedAt` بلا معنى على مستند مالي مرحَّل، لأن المرحَّل لا يُعدَّل. وجود قيمة فيه مؤشر خلل.

---

## 13. المصادقة والصلاحيات

- JWT Bearer، والصلاحيات عبر **Policies قائمة على Roles** تُعرَّف مرة واحدة في `Program.cs`، وأسماؤها ثوابت في `Core/Constants/PolicyNames.cs`.
- **الافتراضي أن كل Endpoint محمي**. طبّق ذلك على مستوى المشروع بدل الاعتماد على الانتباه:

```csharp
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
```

  ثم علّم الـ Endpoints العامة صراحة بـ `[AllowAnonymous]`.
- استخدم `[Authorize(Policy = PolicyNames.AdminOnly)]`. لا تتحقق من الـ Role يدوياً إلا في حالات الملكية (مثال: "صاحب المورد نفسه فقط") — وهذه تكون داخل الـ Service وترمي `ForbiddenException`.
- الوصول لهوية المستخدم الحالي عبر `ICurrentUserService` (يغلّف `IHttpContextAccessor`) — ممنوع حقن `IHttpContextAccessor` داخل الـ Services مباشرة.
- **مدة صلاحية الـ Access Token قصيرة** (15–30 دقيقة)، مع **Refresh Token** مخزّن في قاعدة البيانات، قابل للإبطال، ويُدوَّر عند كل استخدام (rotation).
- `ClockSkew` يُضبط على `TimeSpan.Zero` بدل الافتراضي (5 دقائق).
- كلمات المرور تُخزَّن عبر `PasswordHasher<T>` أو BCrypt. ممنوع أي تجزئة يدوية أو MD5/SHA1.
- رسائل فشل تسجيل الدخول موحّدة ("بيانات الدخول غير صحيحة") ولا تكشف أي الحقلين خاطئ.

### 13.1 استثناء إلزامي: التفويض على بيانات ديناميكية يُفحص في الخدمة

البند أعلاه يوجب `[Authorize(Policy = ...)]`. **هذا ينطبق على التفويض الثابت وحده** — ما يمكن الحكم فيه من محتوى الرمز: هل يحمل حاملُه الدور الفلاني أو الصلاحية الفلانية.

**أما التفويض الذي يعتمد على علاقة مخزَّنة في القاعدة — نطاق الفرع مثالُه الأول — فيُفحص صراحةً في طبقة الخدمة، ويرمي `ForbiddenException`.** لا Policy له.

**السبب الأول — Policy لا تملك الجواب:** «هل يملك هذا المستخدم هذا الفرع؟» جوابه صفٌّ في `UserBranches` وحدُّ شركة في `Branches`. فأي `AuthorizationHandler` سيحقن مستودعاً ويستعلم — أي أنه سيعيد بناء الفحص نفسه في طبقة أخرى. النتيجة **مصدرا حقيقة لمنطق واحد**، وهو عين ما يمنعه بند 2.2 و R-CUR-03. ووضع النطاق في الرمز بدل الاستعلام يجعل سحب فرع لا يسري حتى انتهاء الرمز.

**السبب الثاني وهو الحاسم — Policy لا تحرس كل المسارات:** التقارير المالية تُنفَّذ باستعلام مباشر خارج EF (بند 10.1)، فلا يحرسها أي Global Query Filter. وطبقة الـ Policy تقف عند حدّ الـ Endpoint ولا تعرف شيئاً عن الفرع الذي سيرشِّح به الاستعلامُ صفوفَه. الحارس الوحيد الذي يقع **على مسار البيانات نفسه** هو فحص صريح في الخدمة قبل بناء الاستعلام.

**الشكل المعتمد:**

```csharp
// داخل الخدمة، لا في الكنترولر ولا في Policy
await _userService.EnsurePermissionAsync(Permissions.TrialBalanceRead, ct);
await _userService.EnsureBranchAccessAsync(branchId, ct);
```

- **الصلاحية الثابتة** (`Permissions.RoleManage`) تُفحص كذلك في الخدمة **اتساقاً** مع فحص النطاق، فالاثنان يقعان في نداء واحد ولا يُشتّتان بين طبقتين.
- **`SetFallbackPolicy` يبقى إلزامياً**: هو يضمن أن كل نقطة نهاية تتطلب هوية موثَّقة. الفرق أن التفويض التفصيلي بعده يقع في الخدمة.
- **يُمنع** تكرار أي من هذين الفحصين في الكنترولر أو في `AuthorizationHandler` — تكرارُه يخلق نسخة تنحرف بصمت.

*الحارس:* الاختبار `G06` — القراءة داخل النطاق تنجح وخارجه تُرفض بـ 403، على تقرير يمرّ باستعلام مباشر خارج EF. أي انتقال لهذا الفحص إلى طبقة Policy يُسقطه.

---

## 14. رفع الملفات

خدمة موحّدة `IFileService` (`SaveAsync`, `DeleteAsync`) تُستخدم من أي Service يحتاج رفع ملفات. ممنوع تكرار منطق الحفظ داخل كل Service.

قواعد إلزامية:

1. **حد أقصى للحجم** يُقرأ من الإعدادات (افتراضي 5 ميغابايت للصور)، ويُطبَّق أيضاً على مستوى الخادم (`RequestSizeLimit`).
2. **قائمة امتدادات مسموحة (Whitelist)** — لا Blacklist أبداً: `.jpg`, `.jpeg`, `.png`, `.webp`.
3. **التحقق من المحتوى الفعلي** (magic bytes / file signature)، لا من `ContentType` ولا من الامتداد — كلاهما يُزوَّر بسهولة.
4. **اسم الملف يُولَّد من الخادم**: `Guid` جديد + الامتداد. ممنوع استخدام `file.FileName` ولو بعد "تنظيفه" (خطر Path Traversal).
5. **يُرجَع URL نسبي فقط**، لا مسار قرص.
6. **`wwwroot/uploads/` عام للجميع بلا صلاحيات**. الملفات الحساسة (وثائق هوية، فواتير، مرفقات خاصة) تُحفَظ **خارج `wwwroot`** وتُقدَّم عبر Endpoint محمي يتحقق من الصلاحية ثم يُرجع `FileStreamResult`.
7. الملفات العامة تُحفظ تحت `wwwroot/uploads/{feature}/`.
8. عند فشل العملية بعد رفع الملف، الـ Service مسؤول عن حذفه (بند 7).

---

## 15. تسجيل الاعتماديات

كل التسجيلات تُجمَّع في Extension Methods **دائماً**، لا عند تجاوز عدد معيّن — قاعدة بلا استثناء تعني بلا تردد:

```csharp
// Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryService, CategoryService>();
        return services;
    }

    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration config) { /* ... */ return services; }
    public static IServiceCollection AddAppPersistence(this IServiceCollection services, IConfiguration config) { /* ... */ return services; }
}
```

```csharp
// Program.cs
builder.Services.AddAppPersistence(builder.Configuration);
builder.Services.AddAppAuthentication(builder.Configuration);
builder.Services.AddAppServices();
```

`Program.cs` يبقى قابلاً للقراءة في شاشة واحدة.

---

## 16. الترحيلات (Migrations)

- الاسم يصف التغيير بالإنجليزية وبصيغة PascalCase تصف الفعل: `AddVendorCoverImage`, `FixVendorTextColumns`. ممنوع `Update1` أو `Fix`.
- **راجع ملف الـ Migration المولَّد قبل تطبيقه** — خصوصاً `DropColumn` و `AlterColumn` على بيانات موجودة.
- **ممنوع `Database.Migrate()` تلقائياً عند الإقلاع في بيئة الإنتاج**؛ الترحيل خطوة نشر صريحة.
- إعدادات الـ Entity في `Data/Configurations/` عبر `IEntityTypeConfiguration<T>`، لا تكديس في `OnModelCreating`.
- كل حقل نصي له `HasMaxLength` صريح. ممنوع `nvarchar(max)` بلا سبب.
- الحقول المستخدمة في البحث أو الفلترة لها فهارس صريحة، والحقول الفريدة لها `IsUnique()`.

### 16.1 الدقة العددية — إلزامي

**كل حقل `decimal` له `HasPrecision` صريح في `IEntityTypeConfiguration<T>`.** ممنوع الاعتماد على الافتراضي.

السبب: افتراضي EF Core لأي `decimal` هو `decimal(18,2)`، ويُطبَّق **بصمت وبلا تحذير**. سعر صرف يُقصد به `(28,12)` يصبح `1320.00`، وأي سعر أقل من واحد يصبح صفراً، وتكلفة مخزون تُقطع قبل أوانها فتتضخم عند الضرب في الكميات.

| النوع | الدقة |
|---|---|
| المبالغ والتكاليف | `HasPrecision(19, 4)` |
| أسعار الصرف | `HasPrecision(28, 12)` |
| الكميات | `HasPrecision(19, 6)` |

- ممنوع `float` / `real` / `double` في أي حقل مالي أو كمي، بلا استثناء.
- **اختبار إلزامي** يمرّ على النموذج كله ويفشل إن وُجد `decimal` بلا `HasPrecision`:

```csharp
[Fact]
public void AllDecimalProperties_MustHaveExplicitPrecision()
{
    var offenders = _context.Model.GetEntityTypes()
        .SelectMany(e => e.GetProperties())
        .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
        .Where(p => p.GetPrecision() is null)
        .Select(p => $"{p.DeclaringType.ShortName()}.{p.Name}")
        .ToList();

    Assert.True(offenders.Count == 0, string.Join(", ", offenders));
}
```

### 16.2 التريجرات (Triggers)

القيود المالية التي لا يجوز الالتفاف عليها — توازن القيد، قفل عملة الأساس، منع الترحيل في فترة مقفلة — تُفرض في **قاعدة البيانات** عبر TRIGGER أو CHECK، لا في طبقة الكود وحدها. السبب: أي تعديل مباشر على القاعدة يجب أن يُمنع أيضاً.

**تحذير تقني:** منذ EF Core 7، أي جدول عليه TRIGGER يفشل في `SaveChangesAsync` ما لم يُعلَن صراحة:

```csharp
builder.ToTable("JournalEntries", t => t.HasTrigger("TR_JournalEntry_EnforceBalance"));
```

الإعلان إلزامي في `IEntityTypeConfiguration<T>` لكل جدول عليه تريجر. نسيانه يعطّل الحفظ برسالة غامضة.

---

## 17. الاختبارات

هذا البند هو مبرر وجود الـ Interfaces في بند 4. بدونه، الـ Interfaces مخالفة لبند 2.2.

- **Unit Tests** للـ Services: كل قاعدة عمل لها اختبار للحالة الناجحة وللحالة التي ترمي Exception. الـ Repositories تُستبدل بـ Mocks.
- **Integration Tests** للـ Endpoints الحرجة عبر `WebApplicationFactory` مع قاعدة بيانات اختبارية.
- الاختبار يُسمّى: `MethodName_Scenario_ExpectedResult` (مثال: `CreateAsync_WhenNameExists_ThrowsConflictException`).
- ممنوع اختبار الـ Repositories بـ InMemory Provider — سلوكه يختلف عن SQL Server ويعطي ثقة زائفة.

---

## 18. الأمان والإعدادات

- **ممنوع** وضع `ConnectionString` أو مفتاح JWT أو أي سرّ في `appsettings.json` المرفوع للـ Git. استخدم **User Secrets** محلياً و**متغيرات البيئة** أو Key Vault في الإنتاج. `appsettings.json` يحتوي المفاتيح بقيم فارغة كتوثيق فقط.
- مفتاح JWT لا يقل عن 32 بايت عشوائي.
- **CORS**: سياسة مسمّاة بنطاقات محدّدة. ممنوع `AllowAnyOrigin()` مع `AllowCredentials()`.
- **Rate Limiting** إلزامي على endpoints المصادقة (تسجيل دخول، استعادة كلمة مرور) عبر `builder.Services.AddRateLimiter(...)`.
- في الإنتاج: `UseHsts()` و `UseHttpsRedirection()`، وتعطيل صفحات الخطأ التفصيلية و Swagger (أو حمايته خلف مصادقة).
- ممنوع تسجيل بيانات حساسة في الـ Logs (كلمات مرور، Tokens، أرقام بطاقات). `EnableSensitiveDataLogging()` للتطوير فقط.
- تحقّق دائماً من ملكية المورد قبل تعديله أو حذفه، حتى لو كان المستخدم يملك الصلاحية العامة (منع IDOR).

---

## 19. قائمة تحقق عند إضافة Feature جديد

1. `Core/Models/{Feature}.cs` — الـ Entity (يطبّق `IAuditableEntity`) + `DbSet` في `AppDbContext` + `Data/Configurations/{Feature}Configuration.cs`.
2. `Core/DTO/{Feature}/` — `CreateDto`, `UpdateDto`, `ResponseDto` مع `DataAnnotations`.
3. `Repositories/I{Feature}Repository.cs` + `{Feature}Repository.cs` — بلا `SaveChanges`، بلا منطق عمل.
4. `Services/{Feature}/` — المنطق، التحقق من قواعد العمل، رمي Custom Exceptions، `SaveChangesAsync` مرة واحدة، تحويل إلى DTO.
5. `Controllers/{Feature}Controller.cs` — Endpoints رقيقة، `ApiResponse<T>`، رموز حالة صحيحة، بلا `try/catch`.
6. تسجيل الـ Repository والـ Service في `AddAppServices()`.
7. Migration باسم واضح + مراجعة الملف المولَّد.
8. التأكد من الصلاحيات (`[Authorize]` / `[AllowAnonymous]`) على كل Endpoint.
9. اختبارات الـ Service لقواعد العمل الأساسية.

---

## 20. ما يجب تجنّبه دائماً

- `try/catch` داخل الـ Controller، أو رمي `Exception` عام غير مُصنَّف من الـ Service.
- بناء استجابة عبر `new { success = ..., data = ... }` — استخدم `ApiResponse<T>`.
- إرجاع Entity من الـ Service أو استقباله في Action.
- منطق عمل داخل الـ Controller أو الـ Repository.
- استدعاء `SaveChangesAsync` داخل الـ Repository، أو أكثر من مرة في عملية واحدة بلا معاملة.
- تسريب `IQueryable` أو `DbContext` خارج طبقة البيانات.
- `Guid.NewGuid()` كمفتاح Clustered، أو ضبط `CreatedAt` يدوياً.
- استعلام بلا `OrderBy` قبل `Skip`/`Take`، أو `GetAll()` بلا حد.
- الاعتماد على `ContentType` أو الامتداد للتحقق من الملفات، أو استخدام `file.FileName`.
- أسرار في `appsettings.json`، أو `AllowAnyOrigin` مع `AllowCredentials`.
- مسافات أو رموز غريبة في أسماء الملفات والمجلدات.
- تسجيل يدوي متكرر في `Program.cs` بدل Extension Method.
- إدخال مكتبة أو نمط تصميم جديد لمشروع واحد دون تبرير — أي إضافة معمارية يجب أن تنعكس على بقية المشاريع لاحقاً لتبقى المعايير موحّدة.