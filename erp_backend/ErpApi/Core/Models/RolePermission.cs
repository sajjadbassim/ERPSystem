using ErpApi.Core.Interfaces;

namespace ErpApi.Core.Models;

// PermissionCode نص حر بلا CHECK وبلا مفتاح أجنبي: لا يوجد جدول Permission عمداً،
// والكتالوج ثوابت في Permissions.cs. الرمز غير المعروف يُقبل تخزينه ولا يمنح شيئاً —
// الفشل المغلق يقع في طبقة التخويل لا في القاعدة.
public class RolePermission : ICreationAuditable
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Role Role { get; set; } = null!;
}
