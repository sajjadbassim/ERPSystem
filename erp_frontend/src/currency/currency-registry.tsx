import { createContext, useContext } from "react";
import type { ReactNode } from "react";

import type { components } from "../../api-types/schema";

// ‏R-API-04 [صارم]: النموذج **مشتق من العقد** لا مكتوب بيد. فمتى تغيّر
// ‏`CurrencyResponseDto` في الخلفية وأُعيد التوليد، تسقط الترجمة هنا وفي بيانات
// الاختبار معاً — بدل أن ينحرف نموذجان متطابقان يدوياً بصمت.
export type RegisteredCurrency = components["schemas"]["CurrencyResponseDto"];

const CurrencyRegistryContext = createContext<readonly RegisteredCurrency[] | null>(null);

export type CurrencyRegistryProviderProps = {
  currencies: readonly RegisteredCurrency[];
  children: ReactNode;
};

export function CurrencyRegistryProvider(props: CurrencyRegistryProviderProps) {
  return (
    <CurrencyRegistryContext.Provider value={props.currencies}>
      {props.children}
    </CurrencyRegistryContext.Provider>
  );
}

// ‏يُرجع `undefined` حين لا تُعرف العملة، ولا يرمي ولا يُرجع بديلاً: **القرار
// للمستدعي** لأن R-RPT-06 يوجب عرض خطأ لا رقماً، وبديلٌ صامت هنا كان سيُنتج
// الرقم المجرد بعينه الذي تمنعه القاعدة.
//
// وغياب المزوّد كلّياً يسلك المسلك نفسه: كلاهما «عملة لا تُعرف خاناتها ورمزها»،
// وتمييزهما يزيد فرعاً لا يحرسه اختبار.
export function useCurrency(currencyId: string): RegisteredCurrency | undefined {
  const currencies = useContext(CurrencyRegistryContext);

  return currencies?.find((currency) => currency.id === currencyId);
}

// ‏القائمة كلها — يحتاجها مَن يعرض خيارات لا مَن يعرض مبلغاً واحداً
const EMPTY: readonly RegisteredCurrency[] = [];

export function useCurrencies(): readonly RegisteredCurrency[] {
  return useContext(CurrencyRegistryContext) ?? EMPTY;
}
