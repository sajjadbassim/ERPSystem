import { skipToken, useQuery } from "@tanstack/react-query";

import type { components } from "../../api-types/schema";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, readFailure } from "./api-error";
import { apiFetch } from "./http";

// ‏النوع من العقد المولَّد وحده (R-API-04) — لا نسخة يدوية تنحرف عن `TrialBalanceResponseDto`
export type TrialBalance = components["schemas"]["TrialBalanceResponseDto"];

type TrialBalanceEnvelope = components["schemas"]["ApiResponseOfTrialBalanceResponseDto"];

export type UseTrialBalanceResult = {
  status: "pending" | "success" | "error";
  trialBalance: TrialBalance | null;
  error: Error | null;
};

async function fetchTrialBalance(branchId: string): Promise<TrialBalance> {
  const response = await apiFetch(`/api/reports/trial-balance?branchId=${encodeURIComponent(branchId)}`);

  if (!response.ok) {
    throw new ApiError(await readFailure(response, `تعذّر جلب ميزان المراجعة (${response.status}).`));
  }

  const body = (await response.json()) as TrialBalanceEnvelope;

  if (body.data === null || body.data === undefined) {
    throw new Error("استجابة الميزان بلا بيانات.");
  }

  return body.data;
}

// ‏لا نداء قبل رمز صالح (`L08`) **ولا قبل اختيار فرع** (`skipToken`): فرعٌ فارغ ليس فرعاً.
//
// ‏**‏`staleTime: 0` صراحةً — لا `MASTER_DATA_STALE_TIME` ولا `Infinity`:** أرقام الميزان
// تتغيّر مع كل ترحيل، فدخول الشاشة ثانيةً يجلب من جديد (`TB09`). والصفر هو افتراض
// ‏`query-config` أصلاً، ويُكتب هنا **إعلاناً** كي لا يُرفع يوماً بلا أن يُرى أنه قرار.
// ‏ولا إعادة محاولة مضافة: الإعادة الواحدة للأعطال العابرة من الإعداد العام كما هي
export function useTrialBalance(branchId: string | null): UseTrialBalanceResult {
  const { status } = useAuth();

  const query = useQuery({
    queryKey: ["trial-balance", branchId],
    queryFn: branchId === null ? skipToken : () => fetchTrialBalance(branchId),
    enabled: status === "authenticated",
    staleTime: 0
  });

  return {
    status: query.status,
    trialBalance: query.data ?? null,
    error: query.error
  };
}
