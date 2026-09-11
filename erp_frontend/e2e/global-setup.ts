import {
  API_BASE,
  ensureAccount,
  findCompanyId,
  findCurrencyId,
  login,
  newApiContext
} from "./erp-api";

// ‏**رموز الحسابات التي يملكها `BR01`** — ولا يتّكل على ما في القاعدة بالصدفة.
//
// ‏السبب مقيس لا مبدئيّ: `ErpApi.DevSeedTool/README.md` يقول صراحةً إنه **لا يُنشئ
// حسابات** («فالدخول ينجح وشجرة الحسابات فارغة — وهو المتوقع لا عطب»). والحسابات
// الموجودة اليوم بقيّةُ مسبار `P06` الذي **حُذف ولم يُلتزَم**. فاختبارٌ يؤكّد على
// ‏`1110` بلا أن يملكه يخضرّ على هذا الجهاز وحده، ويخضرّ **بحادثة حالة يدوية**.
export const BOUND_CODE = "1110";
export const UNBOUND_CODE = "1100";

// ‏الخادم **لا يُشغَّل من مشغّل الاختبارات** بقرار: عملية ذات أسرار وشهادة وقاعدة
// بيانات، وتشغيلها هنا يجعل الأحمر غامضاً — «أعطبَ التطبيق أم لم يُقلع الخادم؟».
// فيُفحص وجوده ويُرسَب **برسالة عربية صريحة تقول ما يُفعل**، ولا يُتخطّى.
// ‏وهو نفس عقد `M04` المسجَّل: «يُرسبه برسالة عربية صريحة ولا يتخطّاه».
function apiDownMessage(reason: string): string {
  return [
    "",
    `‏⛔ لا يستجيب ErpApi على ${API_BASE}`,
    `‏   السبب: ${reason}`,
    "",
    "‏   شغّله في طرفية مستقلة ثم أعد المحاولة:",
    "‏     cd erp_backend",
    "‏     dotnet run --project ErpApi --launch-profile https",
    "",
    "‏   ‏BR01 اختبار تكامل حيّ: لا محاكاة فيه ولا خادم وهميّ،",
    "‏   فغياب الخادم يُرسِبه ولا يُتخطّى.",
    ""
  ].join("\n");
}

export default async function globalSetup(): Promise<void> {
  const api = await newApiContext();

  try {
    // ‏الفحص بنداء حقيقي لا بفتح منفذ: منفذ مفتوح لعملية تُقلع ولم تجهز بعد كان
    // سيمرّر التمهيد ثم يُرسب الاختبار بخطأ شبكة غامض.
    let accessToken: string;

    try {
      accessToken = await login(api);
    } catch (error) {
      const reason = error instanceof Error ? error.message : String(error);

      // ‏خطأ الشبكة يُلفّ برسالة التشغيل، وخطأ الدخول (رسالته من `login`) يُمرَّر
      // كما هو: الأول «لا خادم» والثاني «خادم بلا بذرة» — وخلطهما يضيّع التشخيص.
      throw reason.includes("فشل دخول") || reason.includes("accessToken")
        ? error
        : new Error(apiDownMessage(reason));
    }

    const iqdId = await findCurrencyId(api, accessToken, "IQD");
    const companyId = await findCompanyId(api, accessToken);

    await ensureAccount(api, accessToken, companyId, {
      code: BOUND_CODE,
      name: "صندوق الدينار",
      currencyId: iqdId
    });

    await ensureAccount(api, accessToken, companyId, {
      code: UNBOUND_CODE,
      name: "النقدية بالصندوق",
      currencyId: null
    });
  } finally {
    await api.dispose();
  }
}
