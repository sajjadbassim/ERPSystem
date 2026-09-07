namespace ErpApi.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    // يُملأ في الفشل وحده، ليُربط بلاغ المستخدم بسجلات الخادم
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, string? traceId = null) =>
        new() { Success = false, Message = message, TraceId = traceId };
}
