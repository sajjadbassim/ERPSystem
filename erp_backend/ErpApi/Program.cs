using ErpApi.Common;
using ErpApi.Common.Json;
using ErpApi.Common.Validation;
using ErpApi.Extensions;
using ErpApi.Middleware;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// نقطة التسجيل المركزية الوحيدة لمحوّل المبالغ (R-API-03). أي DTO يحمل decimal
// يُحمى تلقائياً، فلا يعتمد الأمر على تذكّر كاتب الـ DTO.
// والفارض المسجَّل معه يكنس كل خاصية enum بالانعكاس، فتُحمى الخاصية الجديدة بحكم
// البناء لا بحكم التذكّر — والسمة على كل خاصية كانت تفشل مفتوحة
builder.Services.AddControllers(options => options.Filters.Add<EnumRangeValidationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new DecimalAsStringConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableDecimalAsStringConverter());
    });

// المحوّل أعلاه يجعل المبلغ نصاً على السلك، وهذا يجعله نصاً في العقد. الطرفان لازمان:
// مولّد المخطط يقرأ Http.Json.JsonOptions لا Mvc.JsonOptions، فلا يرى المحوّل أصلاً.
// والثاني نظيره على جانب الـenum: الفارض يمنع ما هو خارج المدى، وهذا يعلن المدى —
// وعقدٌ بلا إعلان يَعِد بقبول ما يرفضه الخادم
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer(new DecimalAsStringSchemaTransformer());
    options.AddSchemaTransformer(new EnumRangeSchemaTransformer());
});

// [ApiController] يُرجع ValidationProblemDetails بشكل مخالف لـ ApiResponse<T>، ولا يمرّ
// بالـ Middleware لأنه ليس استثناءً. تجاوزه إلزامي ليبقى شكل الاستجابة واحداً (بند 6.3)
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

builder.Services.AddAppPersistence(builder.Configuration);
builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddAppAuthentication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// أول شيء في الـ pipeline، وإلا لم يلتقط استثناءات ما قبله (بند 8.3)
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// يتيح لمشروع الاختبارات إقلاع التطبيق عبر WebApplicationFactory (بند 17)
public partial class Program;
