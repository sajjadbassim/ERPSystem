import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, renderHook, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { useTheme } from "@mui/material/styles";
import type { ReactNode } from "react";

import { AuthProvider, useAuth } from "./AuthProvider";
import { clearTokens, getTokens, setTokens } from "./token-store";
import { apiFetch } from "../api/http";
import { AppRoot } from "../AppRoot";
import { useAllAccounts } from "../api/useAllAccounts";

// ‏مصفوفة الحالات L (المصادقة وتركيب الجذر).
//
// ⚠ **درس سلسلة `S` مطبَّق في التصميم لا في الاعتذار:** هناك سقطت ثماني حالات على
// سبب واحد مشترك (فتح القائمة) فبدت الحمرة أوسع مما تقيس. وهنا فُصلت الطبقات عمداً:
//
//   ‏`L01`,`L02`      ⟵ المزوّد وحده. لا تمسّ `apiFetch`
//   ‏`L03`..`L07`,`L10` ⟵ غلاف الطلب وحده، **والرمز يُبذر في المخزن مباشرةً**
//                        لا عبر تسجيل دخول — فلا تتوقف على نجاح `L01`
//   ‏`L08`,`L09`      ⟵ التركيب وحده
//
// فلا حالةٌ تنتظر أخرى، وكلٌّ ترسب على ادعائها.
//
// و`token-store` **منفَّذ فعلاً لا مبذور**: هو أداة البذر في الاختبار، ولو كان جسماً
// فارغاً لصارت حمرة `L03` تعني «لا رمز» لا «الترويسة لم تُضف».

const TOKENS = {
  accessToken: "access-token-1",
  refreshToken: "refresh-token-1",
  accessTokenExpiresAt: "2026-09-09T10:00:00.0000000Z"
};

const ROTATED = {
  accessToken: "access-token-2",
  refreshToken: "refresh-token-2",
  accessTokenExpiresAt: "2026-09-09T10:15:00.0000000Z"
};

type StubbedCall = { url: string; init: RequestInit | undefined };

function jsonResponse(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function tokenEnvelope(pair: typeof TOKENS) {
  return { success: true, message: null, data: pair, traceId: null };
}

// ‏موجّه بحسب المسار والمحاولة: يسمح لكل حالة أن تصف سيناريو الخادم بدقة بدل أن
// تعتمد على ترتيب النداءات
function stubRoutes(handler: (call: StubbedCall, index: number) => unknown) {
  let index = 0;

  const fetchMock = vi.fn(async (input: unknown, init?: RequestInit) =>
    handler({ url: String(input), init }, index++)
  );

  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

function authWrapper({ children }: { children: ReactNode }) {
  return <AuthProvider>{children}</AuthProvider>;
}

function queryAuthWrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return (
    <QueryClientProvider client={client}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );
}

// ‏`useQueryClient` داخل `try` مقصود: بلا مزوّد يرمي، والرمي أثناء التصيير كان
// سيُسقط الحالة قبل أي تأكيد. وترتيب الهوكات ثابت فلا يُخالف قواعدها
function RootProbe() {
  let hasQueryClient = false;

  try {
    useQueryClient();
    hasQueryClient = true;
  } catch {
    hasQueryClient = false;
  }

  const direction = useTheme().direction;

  return <span>{`${direction}:${hasQueryClient}`}</span>;
}

function AccountsProbe() {
  useAllAccounts();
  return null;
}

beforeEach(() => {
  clearTokens();
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("L — المصادقة وتركيب الجذر", () => {
  it("L01: نجاح الدخول يخزّن الرمزين ويصير المستخدم مسجَّلاً", async () => {
    stubRoutes(() => jsonResponse(tokenEnvelope(TOKENS)));

    const { result } = renderHook(() => useAuth(), { wrapper: authWrapper });

    await result.current.login({ userName: "devadmin", password: "Dev@Local!2026" });

    // ‏الرمزان معاً لا الوصول وحده: التجديد يحتاج الثاني، وتخزينه ناقصاً يجعل أول
    // انتهاء صلاحية خروجاً نهائياً
    await waitFor(() => expect(result.current.status).toBe("authenticated"));

    expect(getTokens()).toEqual(TOKENS);
  });

  it("L02: فشل الدخول (401) ⟵ خطأ ظاهر وصفر رمز مخزَّن", async () => {
    stubRoutes(() => jsonResponse({ success: false, message: "بيانات الدخول غير صحيحة.", data: null }, 401));

    const { result } = renderHook(() => useAuth(), { wrapper: authWrapper });

    await result.current.login({ userName: "devadmin", password: "خطأ" });

    // ‏الصمت هنا يجعل المستخدم يظن أنه دخل
    await waitFor(() => expect(result.current.error).not.toBeNull());

    expect(result.current.status).toBe("anonymous");

    // ‏والشقّ الثاني يمنع تخزيناً جزئياً من استجابة فاشلة
    expect(getTokens()).toBeNull();
  });

  it("L03: كل طلب لاحق يحمل ترويسة Bearer تلقائياً", async () => {
    setTokens(TOKENS);

    const fetchMock = stubRoutes(() => jsonResponse({ success: true, data: null }));

    await apiFetch("/api/accounts");

    const headers = new Headers(fetchMock.mock.calls[0]?.[1]?.headers);

    expect(headers.get("Authorization")).toBe(`Bearer ${TOKENS.accessToken}`);
  });

  it("L04: 401 من نقطة محمية ⟵ تجديد ثم إعادة الطلب فينجح", async () => {
    setTokens(TOKENS);

    // ‏`ClockSkew = Zero` و15 دقيقة يجعلان هذا **شرط استعمال لا تحسيناً**
    const fetchMock = stubRoutes((call) => {
      if (call.url.includes("/api/auth/refresh")) {
        return jsonResponse(tokenEnvelope(ROTATED));
      }

      const authorization = new Headers(call.init?.headers).get("Authorization");

      return authorization === `Bearer ${ROTATED.accessToken}`
        ? jsonResponse({ success: true, data: { data: [] } })
        : jsonResponse({ success: false, data: null }, 401);
    });

    const response = await apiFetch("/api/accounts");

    expect(response.status).toBe(200);

    // ‏والتجديد يُدوِّر الرمزين معاً (العقد يُرجع زوجاً كاملاً)، فالمخزن يحمل الجديد
    expect(getTokens()).toEqual(ROTATED);

    expect(fetchMock.mock.calls.map((call) => String(call[0]))).toEqual([
      expect.stringContaining("/api/accounts"),
      expect.stringContaining("/api/auth/refresh"),
      expect.stringContaining("/api/accounts")
    ]);
  });

  it("L05: 401 من /login نفسها لا يُطلق تجديداً", async () => {
    // ‏⚠ البذر **شرط أن تقيس الحالة ادعاءها**: بلا رمز يقصر `apiFetch` الدائرة على
    // «لا شيء أجدّده» فيمرّ التأكيد بالفراغ لا بالصحة. قِيس ذلك بعطل متعمَّد لم
    // يُرسبها، فأُصلح الإعداد — والادعاء نفسه لم يتغيّر
    setTokens(TOKENS);

    const fetchMock = stubRoutes(() => jsonResponse({ success: false, data: null }, 401));

    await apiFetch("/api/auth/login", { method: "POST" });

    // ‏بيانات خاطئة ليست جلسة منتهية. وبلا هذا الاستثناء تدور الطبقة على نفسها عند
    // كل محاولة دخول فاشلة
    expect(fetchMock.mock.calls.map((call) => String(call[0]))).toEqual([
      expect.stringContaining("/api/auth/login")
    ]);
  });

  it("L06: فشل التجديد ⟵ العودة إلى «غير مسجَّل» وإسقاط الرمزين", async () => {
    setTokens(TOKENS);

    stubRoutes(() => jsonResponse({ success: false, data: null }, 401));

    const response = await apiFetch("/api/accounts");

    expect(response.status).toBe(401);

    // ‏حلقة فشل صامتة أسوأ من خروج معلن: المخزن يُفرَّغ فتعرف الواجهة أنها خارج الجلسة
    expect(getTokens()).toBeNull();
  });

  it("L07: محاولة تجديد واحدة لا أكثر", async () => {
    setTokens(TOKENS);

    // ‏الخادم يقبل التجديد ثم يردّ 401 من جديد — الفخّ الذي يُنتج حلقة لانهائية
    const fetchMock = stubRoutes((call) =>
      call.url.includes("/api/auth/refresh")
        ? jsonResponse(tokenEnvelope(ROTATED))
        : jsonResponse({ success: false, data: null }, 401)
    );

    const response = await apiFetch("/api/accounts");

    expect(response.status).toBe(401);

    const refreshCalls = fetchMock.mock.calls.filter((call) =>
      String(call[0]).includes("/api/auth/refresh")
    );

    // ‏ادعاء منفصل عن `L04`: ذاك يثبت أن التجديد يقع، وهذا يثبت أن له **حدّاً**
    expect(refreshCalls).toHaveLength(1);
  });

  it("L08: الاستعلامات المحمية لا تُطلَق قبل وجود رمز", async () => {
    const fetchMock = stubRoutes(() => jsonResponse({ success: true, data: null }));

    render(<AccountsProbe />, { wrapper: queryAuthWrapper });

    // ‏يمنع موجة 401 عند الإقلاع، ويمنع أن تبدو «لا حسابات» بينما السبب عدم الدخول
    // — نمط الفشل نفسه الذي وُجدت `Q06` له
    await waitFor(() => expect(fetchMock).not.toHaveBeenCalled());
  });

  it("L09: AppRoot يوفّر عميل الاستعلام والسمة معاً", () => {
    render(
      <AppRoot>
        <RootProbe />
      </AppRoot>
    );

    // ‏الدَّين ٧ مقيساً: مزوّد الاستعلام مركَّب فعلاً، والاتجاه يبلغ الشجرة معه
    expect(screen.getByText(/^(rtl|ltr):(true|false)$/u).textContent).toBe("rtl:true");
  });

  it("L10: طلبان متزامنان يتلقيان 401 ⟵ تجديد واحد لا اثنان", async () => {
    setTokens(TOKENS);

    const fetchMock = stubRoutes((call) => {
      if (call.url.includes("/api/auth/refresh")) {
        return jsonResponse(tokenEnvelope(ROTATED));
      }

      const authorization = new Headers(call.init?.headers).get("Authorization");

      return authorization === `Bearer ${ROTATED.accessToken}`
        ? jsonResponse({ success: true, data: { data: [] } })
        : jsonResponse({ success: false, data: null }, 401);
    });

    await Promise.all([apiFetch("/api/accounts"), apiFetch("/api/branches")]);

    const refreshCalls = fetchMock.mock.calls.filter((call) =>
      String(call[0]).includes("/api/auth/refresh")
    );

    // ‏التجديد يُدوِّر رمز التجديد، فتجديدان متزامنان يعني أن الثاني يُبطل الأول
    // ويسقط أحد الطلبين بلا سبب ظاهر
    expect(refreshCalls).toHaveLength(1);
  });
});
