import type { components } from "../../api-types/schema";

// ‏**عرض محليّ مربوط بالعقد بنيوياً — لا تكرار له.**
//
// ‏العقد يصف `accountType` بـ`1 | 2 | 3 | 4 | 5` و`normalBalance` بـ`0 | 1`
// (`schema.d.ts:1358, 1687`) — **أرقاماً بلا تسمية**. والتسمية العربية قرار عرض
// تملكه الواجهة وحدها: الخلفية لا تُرسل نصّاً، ولا يُطلب منها أن تفعل.
//
// ‏**والربط بنيويّ لا بالتسمية المتوازية:** المفتاح مشتقّ من نوع العقد نفسه، فلو
// أضافت الخلفية `AccountType = 6` وأُعيد التوليد لصار الاتحاد `1..6` **ونقص مفتاح**
// فيسقط `tsc` بـ`TS2739`. ولو حُذفت قيمة لصار مفتاحنا خارج الاتحاد فيسقط أيضاً.
//
// ‏والفرق عن كتابة `Record<number, string>`: الأخير يقبل أي رقم فيبقى أخضر أبداً،
// وتنحرف التسميات عن العقد **بصمت** — وهو ما تمنعه R-API-04 نصّاً. وحارس هذا الملف
// ‏`npm run typecheck` داخل `npm run build`، لا `npm test` (نظير `satisfies` في
// ‏`branch-schema.ts`).
type AccountTypeValue = components["schemas"]["AccountType"];
type NormalBalanceValue = components["schemas"]["NormalBalance"];

export const ACCOUNT_TYPE_LABELS: Record<AccountTypeValue, string> = {
  1: "أصول",
  2: "التزامات",
  3: "حقوق ملكية",
  4: "إيرادات",
  5: "مصروفات"
};

// ‏`0 = Debit` و`1 = Credit` — مقيس من `AccountType.cs` و`NormalBalance.cs`، لا
// مستنتَج من ترتيب شائع. والخادم يفرض التناسق (الأصول والمصروفات مدينة)، فالتسمية
// هنا **عرضٌ لما قرّره** لا قاعدة ثانية تنافسه (R-API-05)
export const NORMAL_BALANCE_LABELS: Record<NormalBalanceValue, string> = {
  0: "مدين",
  1: "دائن"
};
