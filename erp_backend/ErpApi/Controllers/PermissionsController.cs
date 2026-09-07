using ErpApi.Common;
using ErpApi.Core.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ErpApi.Controllers;

// المصدر الوحيد لكتالوج الصلاحيات، تقرأه الواجهة بدل كتابة أي رمز يدوياً.
// يكفيه التوثّق: الكتالوج ليس بيانات شركة. الحارس H07 يفرض مطابقته للثوابت حرفياً
[ApiController]
[Route("api/permissions")]
public class PermissionsController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> GetAll() =>
        Ok(ApiResponse<IReadOnlyList<string>>.Ok(Permissions.All));
}
