import { afterEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { useAccounts } from "./useAccounts";
import type { AccountItem } from "./useAccounts";

// ‏مصفوفة الحالات Q (استهلاك الـAPI عبر TanStack Query).
//
// **الخطر الذي تحرسه هذه المصفوفة مقيس لا متخيَّل.** في `openapi.json`:
//   ApiResponseOfPagedResponseOfAccountResponseDto → required: null
//     data: oneOf [ null, PagedResponseOfAccountResponseDto ]
//       PagedResponseOfAccountResponseDto          → required: null
//         totalCount/pageNumber/... : ["integer","string"]
//
// أي أن **لا حقل إلزامياً في الغلافين**، و`data` قابلة للعدم، والعدّادات قد تصل نصّاً.
// فـ`response.data.data.length` ينفجر على استجابة **صالحة تماماً بحسب العقد**.
//
// و`fetch` مثبَّت هنا بقرار معلن (انظر «قرار تجاوز مؤقت» في `FRONTEND-STATE.md`):
// المقيس تحته فكّ التعشيش والتطبيع، لا دلالات HTTP.

const ACCOUNT: AccountItem = {
  id: "0199a1f0-0000-7000-8000-0000000000a1",
  companyId: "0199a1f0-0000-7000-8000-000000000001",
  code: "1010",
  name: "الصندوق",
  accountType: 1,
  normalBalance: 0,
  isPostable: true,
  isActive: true
};

const ACCOUNT_TWO: AccountItem = { ...ACCOUNT, id: "0199a1f0-0000-7000-8000-0000000000a2", code: "1020", name: "المصرف" };

function createWrapper() {
  // ‏`retry: false` إلزامي في الاختبار: الإعادة الافتراضية ثلاث مرات تجعل `Q06`
  // بطيئة وتخفي لحظة الخطأ خلف محاولات
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

function stubFetch(body: unknown, options: { ok?: boolean; status?: number } = {}) {
  const fetchMock = vi.fn(async () => ({
    ok: options.ok ?? true,
    status: options.status ?? 200,
    json: async () => body
  }));

  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("useAccounts — Q (استهلاك GET /api/accounts)", () => {
  it("Q01: استجابة كاملة ⟵ العناصر تصل كما هي", async () => {
    stubFetch({ success: true, data: { data: [ACCOUNT, ACCOUNT_TWO], totalCount: 2 } });

    const { result } = renderHook(() => useAccounts(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.status).toBe("success"));

    expect(result.current.items.map((item) => item.code)).toEqual(["1010", "1020"]);
    expect(result.current.totalCount).toBe(2);
  });

  it("Q02: `data: null` في الغلاف ⟵ نجاح بقائمة فارغة لا انهيار", async () => {
    stubFetch({ success: true, message: null, data: null, traceId: null });

    const { result } = renderHook(() => useAccounts(), { wrapper: createWrapper() });

    // ‏التأكيد على **النجاح** قبل الفراغ مقصود: بدونه يصير «القائمة فارغة» صادقاً
    // بالفراغ لو لم يُنفَّذ الاستعلام أصلاً — حارسٌ يمرّ لأن شيئاً لم يحدث
    await waitFor(() => expect(result.current.status).toBe("success"));

    expect(result.current.items).toEqual([]);
  });

  it("Q03: غلاف بلا أي حقل ⟵ نجاح بقائمة فارغة", async () => {
    stubFetch({});

    const { result } = renderHook(() => useAccounts(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.status).toBe("success"));

    expect(result.current.items).toEqual([]);
  });

  it("Q04: صفحة حاضرة ومصفوفتها غائبة ⟵ نجاح بقائمة فارغة", async () => {
    stubFetch({ success: true, data: { totalCount: 0 } });

    const { result } = renderHook(() => useAccounts(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.status).toBe("success"));

    expect(result.current.items).toEqual([]);
  });

  it("Q05: عدّاد يصل نصّاً ⟵ يُطبَّع إلى رقم (العقد: integer|string)", async () => {
    stubFetch({ success: true, data: { data: [], totalCount: "137" } });

    const { result } = renderHook(() => useAccounts(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.status).toBe("success"));

    expect(result.current.totalCount).toBe(137);
  });

  it("Q06: استجابة 401 ⟵ خطأ معلَن لا «لا توجد حسابات»", async () => {
    // ‏هذه الحالة وُجدت من دَين المصادقة (الفجوة ٥) بعينه: بلا رمز يردّ الخادم 401،
    // وابتلاعه كقائمة فارغة يجعل **المصادقة الغائبة تبدو شجرة حسابات فارغة** —
    // رقم صحيح المعنى خاطئ السبب، وهو الفشل المفتوح الذي يمنعه المشروع كله
    stubFetch({ success: false, message: "غير مصرَّح.", data: null }, { ok: false, status: 401 });

    const { result } = renderHook(() => useAccounts(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.status).toBe("error"));

    expect(result.current.items).toEqual([]);
  });
});
