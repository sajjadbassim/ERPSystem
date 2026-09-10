import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";

import type { components } from "../../api-types/schema";
import { ApiError, readFailure } from "../api/api-error";
import { apiFetch } from "../api/http";
import { MASTER_DATA_STALE_TIME } from "../api/query-config";
import { useAuth } from "../auth/AuthProvider";
import { CurrencyRegistryStateProvider } from "./currency-registry";
import type { RegisteredCurrency } from "./currency-registry";

type CurrenciesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfCurrencyResponseDto"];

// ‏`MaxPageSize = 100` في قواعد الباك-إند (بند 10)
const PAGE_SIZE = 100;

// ‏السقف نفسه ومنطقه نفسه كما في `useAllAccounts`: خادم يُرجع `hasNextPage: true`
// أبداً كان سيعلّق المتصفح. والعملات أقلّ من الحسابات بمراتب، فالسقف هنا احتياط
// بنيويّ لا حدّ متوقَّع
const MAX_PAGES = 100;

const EMPTY: readonly RegisteredCurrency[] = [];

// ‏حلقة صفحات لا نداء واحد — للسبب نفسه المكتوب في `useAllAccounts`: العقد يقبل
// ‏`PageNumber`/`PageSize` فقط، ولا سبيل لطلب «كل شيء» في نداء واحد.
//
// ⚠ **والتوقف عند الصفحة الأولى هنا أخطر منه في الحسابات:** عملة غائبة عن السجل
// لا تظهر «ناقصة» بل تُعرض **إنذار R-RPT-06** على كل مبلغ بها — أي تبدو بيانات
// فاسدة لا تحميلاً ناقصاً. يحرسه `C04`.
async function fetchAllCurrencies(): Promise<readonly RegisteredCurrency[]> {
  const currencies: RegisteredCurrency[] = [];

  for (let pageNumber = 1; pageNumber <= MAX_PAGES; pageNumber += 1) {
    const response = await apiFetch(
      `/api/currencies?PageNumber=${pageNumber}&PageSize=${PAGE_SIZE}`);

    if (!response.ok) {
      throw new ApiError(await readFailure(response, `تعذّر جلب العملات (${response.status}).`));
    }

    const body = (await response.json()) as CurrenciesEnvelope;
    const page = body.data ?? undefined;

    currencies.push(...(page?.data ?? []));

    // ‏`!== true` لا `=== false`: الحقل اختياري في العقد، وغيابه يعني **لا مزيد**
    if (page?.hasNextPage !== true) {
      return currencies;
    }
  }

  throw new ApiError({ status: null, message: `تجاوز تحميل العملات ${MAX_PAGES} صفحة.`, traceId: null });
}

export type CurrencySourceProviderProps = {
  children: ReactNode;
};

// ‏**يملأ السجل من `GET /api/currencies`** — وبه يزول وصف الدَّين ٢: «وعاء بلا مصدر».
export function CurrencySourceProvider(props: CurrencySourceProviderProps) {
  // ‏البوابة نفسها التي يحرسها `L08`: كل نقطة نهاية محمية، فالجلب بلا رمز
  // موجة 401 عند الإقلاع. يحرسه `C06`
  const { status } = useAuth();

  const query = useQuery({
    queryKey: ["currencies", "all"],
    queryFn: fetchAllCurrencies,
    enabled: status === "authenticated",

    // ‏بيانات مرجعية — والعملات أبطأ تغيّراً من الحسابات نفسها (قرار `QC`)
    staleTime: MASTER_DATA_STALE_TIME
  });

  // ‏**«جاهز» تعني النجاح وحده — لا الانتهاء.** والفشل يبقى «قيد التحميل» بقصد:
  // ‏لو أُعلن الجهوز على سجل فارغ لصار عطل الشبكة **إنذارَ «عملة مجهولة»** على كل
  // مبلغ — فيبحث المستخدم عن عملة مفقودة لا وجود لمشكلتها. يحرسه `C05`.
  //
  // ‏وثمنه معلَن: أثناء الفشل تبقى المبالغ نقاطاً. وعرض سبب الفشل شأن الشاشة
  // المستضيفة لا شأن السجل — ولا شاشة كهذه اليوم.
  return (
    <CurrencyRegistryStateProvider
      status={query.isSuccess ? "ready" : "loading"}
      currencies={query.data ?? EMPTY}>
      {props.children}
    </CurrencyRegistryStateProvider>
  );
}
