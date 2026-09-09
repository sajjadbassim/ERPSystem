import { useQuery } from "@tanstack/react-query";

import type { components } from "../../api-types/schema";
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
    const response = await fetch(`/api/accounts?PageNumber=${pageNumber}&PageSize=${PAGE_SIZE}`);

    if (!response.ok) {
      throw new Error(`تعذّر جلب الحسابات (${response.status}).`);
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
  const query = useQuery({ queryKey: ["accounts", "all"], queryFn: fetchAllAccounts });

  return {
    status: query.status,
    accounts: query.data ?? []
  };
}
