using ErpApi.Core.Constants;
using ErpApi.Core.Exceptions;

namespace ErpApi.Services.Identity;

// ‏مقارنة كيانٍ **مُحمَّل** بشركة الفاعل — القاعدة في موضع واحد، تستدعيها كل خدمة بعد
// ‏`GetCompanyScopeAsync`. ولا قاعدة بيانات هنا: الشركتان معروفتان قبل النداء.
//
// ‏**403 لا 404 (قرار 2026-09-28):** الكيان موجود فعلاً، ووجوده خارج نطاقك حكمٌ على الطلب
// لا غيابٌ للكيان — نمط `JournalEntryService.GetByIdAsync`. وغير الموجود 404 قبل بلوغ هذا
public static class CompanyScope
{
    public static void EnsureSame(Guid actorCompanyId, Guid entityCompanyId)
    {
        if (entityCompanyId != actorCompanyId)
        {
            throw new ForbiddenException(AuthMessages.CompanyOutOfScope);
        }
    }
}
