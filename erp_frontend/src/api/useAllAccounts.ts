import { useQuery } from "@tanstack/react-query";

import type { components } from "../../api-types/schema";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, readFailure } from "./api-error";
import { apiFetch } from "./http";
import { MASTER_DATA_STALE_TIME } from "./query-config";
import type { AccountItem } from "./useAccounts";

type AccountsEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfAccountResponseDto"];

export type UseAllAccountsResult = {
  status: "pending" | "success" | "error";

  // ‏قائمة لا `undefined` أبداً — النهج نفسه المعتمَد في `useAccounts`
  accounts: AccountItem[];
};

// ‏`MaxPageSize = 100` في قواعد الباك-إند (بند 10)، فطلب أكثر منه يُقلَّص صامتاً
const PAGE_SIZE = 100;

// ‏سقف أمان: خادم يُرجع `hasNextPage: true` أبداً كان سيعلّق المتصفح في حلقة شبكة
// لا تنتهي. والسقف مشتقّ لا اعتباطيّ: 100 صفحة × 100 = عشرة آلاف حساب، وعندها يكون
// قرار «التحميل الكامل» (الدَّين ٣) قد سقط أصلاً بنصّه.
// ‏⚠ **فرع بلا حارس** — مسجَّل في دفتر غير المُثبَت، والحالة المقترَحة `S10`
const MAX_PAGES = 100;

// ‏هوك **منفصل** لا توسيع لـ`useAccounts`: الأخير يجلب صفحة واحدة وهو مختبَر بـ
// ‏`Q01..Q06`، وتحويله إلى «كل الصفحات» يغيّر عقداً ملتزَماً بلا اختبار يطلب ذلك.
async function fetchAllAccounts(): Promise<AccountItem[]> {
  const accounts: AccountItem[] = [];

  for (let pageNumber = 1; pageNumber <= MAX_PAGES; pageNumber += 1) {
    // ‏`PageNumber`/`PageSize` بحرفهما الكبير كما في العقد — مقيس من معاملات
    // ‏`GET /api/accounts`، ولا يقبل الخادم غيرهما
    // ‏`apiFetch` لا `fetch`: الترويسة والتجديد يقعان في المنفذ الواحد لا هنا
    const response = await apiFetch(
      `/api/accounts?PageNumber=${pageNumber}&PageSize=${PAGE_SIZE}`);

    if (!response.ok) {
      // ‏رسالة الخادم ورقم تتبّعه يصلان المستدعي (الدَّين ٦)
      throw new ApiError(await readFailure(response, `تعذّر جلب الحسابات (${response.status}).`));
    }

    const body = (await response.json()) as AccountsEnvelope;
    const page = body.data ?? undefined;

    accounts.push(...(page?.data ?? []));

    // ‏`!== true` لا `=== false`: الحقل اختياري في العقد، وغيابه يعني **لا مزيد**
    if (page?.hasNextPage !== true) {
      return accounts;
    }
  }

  throw new Error(`تجاوز تحميل الحسابات ${MAX_PAGES} صفحة.`);
}

export function useAllAccounts(): UseAllAccountsResult {
  // ‏لا استعلام قبل رمز صالح (`L08`). وعبر `useAuth()` لا بقراءة المخزن مباشرةً:
  // المخزن وحدة مفردة **غير تفاعلية**، فقراءته كانت تُبقي الاستعلام معطَّلاً بعد
  // تسجيل الدخول حتى يقع تصيير لسبب آخر. والسياق يشترك فيه المستهلك فيتفاعل.
  //
  // ‏⚠ و`useAccounts` (صفحة واحدة) **غير مبوَّب**: لا مستهلك له، وتبويبه إضافةُ
  // سلوك لا يطلبه اختبار. مسجَّل ديناً مقترَحاً `L11`
  const { status } = useAuth();

  const query = useQuery({
    queryKey: ["accounts", "all"],
    queryFn: fetchAllAccounts,
    enabled: status === "authenticated",

    // ‏**البيات يُعلَن ولا يُورَث** (قرار `QC`): الافتراض العام `staleTime: 0` لأن
    // الطزاجة هي الفشل المغلق للمال. وشجرة الحسابات بيانات مرجعية بطيئة التغيّر،
    // وثمن إعادة جلبها **حلقة صفحات** لا نداء واحد — فتُعلن بياتها صراحةً. `QC02`
    staleTime: MASTER_DATA_STALE_TIME
  });

  return {
    status: query.status,
    accounts: query.data ?? []
  };
}
