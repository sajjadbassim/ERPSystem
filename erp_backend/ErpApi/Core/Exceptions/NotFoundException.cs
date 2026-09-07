namespace ErpApi.Core.Exceptions;

// 404 — المورد غير موجود. لا يُستعمل لإخفاء نقص صلاحية: ذلك يبقى 403،
// لأن اختيار الرمز بحسب الملكية يكشف أي المعرّفات حقيقي
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message)
    {
    }
}
