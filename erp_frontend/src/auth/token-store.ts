import type { components } from "../../api-types/schema";

// ‏R-API-04: النوع مشتق من العقد لا مكتوب بيد
export type TokenPair = components["schemas"]["TokenPairDto"];

// ‏**في الذاكرة وحدها — قرار لا حلّ مؤقت.**
//
// الخادم يُصدر الرمزين في **جسم JSON** لا كعيّنة `httpOnly` (مقيس من `TokenPairDto`)،
// فالخيار الأأمن غير متاح بلا تعديل خلفية. ويبقى بديلان: `localStorage` — ويعني
// رمز تجديد عمره **14 يوماً** مقروءاً بأي XSS — أو الذاكرة.
//
// **الثمن معلن: إعادة تحميل الصفحة = إعادة دخول.** وليس هذا نقصاً يُستدرك لاحقاً بل
// رفضٌ لتثبيت خطر في الأساس: اختيار `localStorage` اليوم يجعل إزالته غداً «تراجعاً
// عن ميزة». والجلسة الدائمة تحتاج تدفّق عيّنة `httpOnly` — **جولة خلفية مسجَّلة ديناً**.
//
// ومخزن مفرد على مستوى الوحدة لا سياق React: غلاف `fetch` يقرأه وهو خارج شجرة
// المكوّنات، ولا يملك هوكاً. وثمنه أن الاختبارات تُفرغه بين الحالات صراحةً.
let current: TokenPair | null = null;

export function getTokens(): TokenPair | null {
  return current;
}

export function setTokens(pair: TokenPair): void {
  current = pair;
}

export function clearTokens(): void {
  current = null;
}
