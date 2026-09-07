using ErpApi.Common;
using ErpApi.Core.Exceptions;

namespace ErpApi.Middleware;

// المكان الوحيد الذي يترجم الاستثناء إلى رمز حالة. وجوده هو ما يسمح للكنترولر
// بأن يخلو من try/catch تماماً (بند 20)
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
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                BusinessRuleException => (StatusCodes.Status400BadRequest, ex.Message),
                ConflictException => (StatusCodes.Status409Conflict, ex.Message),
                UnauthorizedException => (StatusCodes.Status401Unauthorized, ex.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
                _ => (StatusCodes.Status500InternalServerError,
                      $"حدث خطأ غير متوقع في الخادم. الرجاء ذكر الرقم التالي عند التواصل مع الدعم: {traceId}")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}, Path: {Path}",
                    traceId, context.Request.Path);
            }
            else
            {
                _logger.LogWarning("Handled exception {ExceptionType}. TraceId: {TraceId}, Message: {Message}",
                    ex.GetType().Name, traceId, ex.Message);
            }

            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message, traceId));
        }
    }
}
