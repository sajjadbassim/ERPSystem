import { skipToken, useQuery } from "@tanstack/react-query";

import type { components } from "../../api-types/schema";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, readFailure } from "./api-error";
import { apiFetch } from "./http";

export type CompanyItem = components["schemas"]["CompanyResponseDto"];

type CompanyEnvelope = components["schemas"]["ApiResponseOfCompanyResponseDto"];

export type UseCompanyResult = {
  status: "pending" | "success" | "error";
  company: CompanyItem | null;
  error: Error | null;
};

// ‏المفتاح في موضع واحد: يقرؤه `useCompany` ويقرؤه حدّ الإرسال من الذاكرة نفسها،
// فيُرسَل القفل على ما تعرضه الشاشة لا على جلبٍ ثانٍ قد يسبقها
export function companyQueryKey(companyId: string | null) {
  return ["companies", companyId] as const;
}

async function fetchCompany(companyId: string): Promise<CompanyItem> {
  const response = await apiFetch(`/api/companies/${encodeURIComponent(companyId)}`);

  if (!response.ok) {
    throw new ApiError(await readFailure(response, `تعذّر جلب الشركة (${response.status}).`));
  }

  const body = (await response.json()) as CompanyEnvelope;

  if (body.data === null || body.data === undefined) {
    throw new Error("استجابة الشركة بلا بيانات.");
  }

  return body.data;
}

// ‏قرار V08 (2026-09-12): عملة الدفاتر تُبلَغ من **الفرع** — `branchId` ⟵ `companyId`
// ⟵ هذا الهوك — لا من مطالبة `erp:cid` في الرمز، التي لا يعتمد عليها الخادم نفسه.
//
// ‏و`companyId: null` يعني «لا فرع مختاراً»: فلا نداء أصلاً (`skipToken`)، لا نداءً
// بمعرّف مخمَّن. يحرسه `JE03`
export function useCompany(companyId: string | null): UseCompanyResult {
  const { status } = useAuth();

  const query = useQuery({
    queryKey: companyQueryKey(companyId),
    queryFn: companyId === null ? skipToken : () => fetchCompany(companyId),
    enabled: status === "authenticated",

    // ‏**بيات لا نهائيّ — استثناء معلَن من قاعدة الاتجاه، لا خرق لها:** عملة الأساس
    // تُقفل عند أول عملية مالية (R-BASE-01، R-BASE-02) ولا تتغيّر لمنشأة قائمة
    // ‏(R-BASE-05). فإعادة جلبها نداءٌ على سؤال جوابه ثابت بالقانون. يحرسه `JE05`
    staleTime: Infinity
  });

  return {
    status: query.status,
    company: query.data ?? null,
    error: query.error
  };
}
