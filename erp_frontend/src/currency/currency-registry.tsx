import { createContext, useContext, useMemo } from "react";
import type { ReactNode } from "react";

import type { components } from "../../api-types/schema";

// ‏R-API-04 [صارم]: النموذج **مشتق من العقد** لا مكتوب بيد. فمتى تغيّر
// ‏`CurrencyResponseDto` في الخلفية وأُعيد التوليد، تسقط الترجمة هنا وفي بيانات
// الاختبار معاً — بدل أن ينحرف نموذجان متطابقان يدوياً بصمت.
export type RegisteredCurrency = components["schemas"]["CurrencyResponseDto"];

// ‏**الحالة الثالثة (2026-09-10، الدَّين ٢).** «لم يُحمَّل بعد» ليست «عملة مجهولة»:
// الأولى تقول «لا أعرف بعد»، والثانية تتّهم البيانات. وقبل هذا التمييز كان كل مبلغ
// على الشاشة يومض بإنذار R-RPT-06 ريثما يصل السجل — فيصير الإنذار الذي وُجد ليكون
// استثناءً **مشهداً معتاداً**، ويتعلّم المستخدم تجاهله فيضيع حين يلزم فعلاً.
export type RegistryStatus = "loading" | "ready";

type RegistryValue = {
  status: RegistryStatus;
  currencies: readonly RegisteredCurrency[];
};

const CurrencyRegistryContext = createContext<RegistryValue | null>(null);

// ‏غياب المزوّد = **جاهز وفارغ** لا «قيد التحميل». والاتجاه مقصود: لو أُرجع
// «قيد التحميل» لصار كل مبلغ بلا مزوّد يعرض نقاطاً إلى الأبد بدل أن يُنذر —
// أي **فشل مفتوح**. والسلوك القائم (إنذار عملة مجهولة) هو الفشل المغلق، ويحرسه `U03`.
const ABSENT: RegistryValue = { status: "ready", currencies: [] };

export type CurrencyRegistryProviderProps = {
  currencies: readonly RegisteredCurrency[];
  children: ReactNode;
};

// ‏المزوّد الصريح: قائمة معلومة ⟵ **جاهز** دائماً. وهو ما تستعمله `U01`–`U05`،
// فعقدها لم يتغيّر بحرف رغم إضافة الحالة الثالثة
export function CurrencyRegistryProvider(props: CurrencyRegistryProviderProps) {
  const value = useMemo<RegistryValue>(
    () => ({ status: "ready", currencies: props.currencies }),
    [props.currencies]);

  return (
    <CurrencyRegistryContext.Provider value={value}>{props.children}</CurrencyRegistryContext.Provider>
  );
}

export type CurrencyRegistryStateProviderProps = {
  status: RegistryStatus;
  currencies: readonly RegisteredCurrency[];
  children: ReactNode;
};

// ‏المزوّد المُحمَّل: يستعمله `<CurrencySourceProvider>` وحده، وهو الوحيد الذي
// يستطيع أن يعلن «قيد التحميل»
export function CurrencyRegistryStateProvider(props: CurrencyRegistryStateProviderProps) {
  const value = useMemo<RegistryValue>(
    () => ({ status: props.status, currencies: props.currencies }),
    [props.status, props.currencies]);

  return (
    <CurrencyRegistryContext.Provider value={value}>{props.children}</CurrencyRegistryContext.Provider>
  );
}

export type CurrencyLookup = {
  isReady: boolean;
  currency: RegisteredCurrency | undefined;
};

// ‏يُرجع الحالة **والعملة** معاً: `undefined` وحدها لا تفرّق بين «غير معروفة»
// و«لم تصل بعد»، والقرار للمستدعي لأن R-RPT-06 يوجب عرض خطأ لا رقماً
export function useCurrencyLookup(currencyId: string): CurrencyLookup {
  const registry = useContext(CurrencyRegistryContext) ?? ABSENT;

  return {
    isReady: registry.status === "ready",
    currency: registry.currencies.find((currency) => currency.id === currencyId)
  };
}

// ‏القائمة كلها — يحتاجها مَن يعرض خيارات لا مَن يعرض مبلغاً واحداً
export function useCurrencies(): readonly RegisteredCurrency[] {
  return (useContext(CurrencyRegistryContext) ?? ABSENT).currencies;
}
