import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { AuthProvider } from "../auth/AuthProvider";
import { clearTokens, setTokens } from "../auth/token-store";
import { MoneyDisplay } from "../components/money/MoneyDisplay";
import { CurrencySourceProvider } from "./currency-source";

// ‏مصفوفة `C` — الدَّين ٢: سجل العملات يصير له **مصدر**، ومعه **حالة ثالثة**.
//
// ‏**القرار المعتمَد (2026-09-10):** «لم يُحمَّل بعد» حالة مستقلة عن «عملة مجهولة».
// وبدونها كان كل مبلغ على الشاشة يومض بإنذار R-RPT-06 أثناء التحميل — فيصير
// الإنذار الذي وُجد ليكون استثناءً **مشهداً معتاداً**، ويفقد معناه عند المستخدم.
//
// ‏والبديل المرفوض: بوابة على مستوى الشاشة تمنع الرندر قبل الجهوز. تعمل، لكنها
// ‏**تفشل مفتوحة**: كل شاشة مالية جديدة يجب أن تتذكّرها، ونسيانها يعيد الإنذارات
// الكاذبة بلا أن يكسر شيئاً. والحالة الثالثة تفشل **مغلقة** بنيوياً.

const IQD = { id: "0199a1f0-0000-7000-8000-000000000001", code: "IQD", name: "دينار عراقي", symbol: "د.ع", decimalPlaces: 0, isActive: true };
const USD = { id: "0199a1f0-0000-7000-8000-000000000002", code: "USD", name: "دولار", symbol: "$", decimalPlaces: 2, isActive: true };

const TOKENS = {
  accessToken: "access-token-1",
  refreshToken: "refresh-token-1",
  accessTokenExpiresAt: "2026-09-10T10:00:00.0000000Z"
};

const ALERT = /عملته غير معروفة/u;

function page(items: unknown[], hasNextPage = false) {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: null,
      traceId: null,
      data: { data: items, totalCount: items.length, pageNumber: 1, pageSize: 100, totalPages: 1, hasNextPage }
    })
  };
}

// ‏وعد معلَّق بيدنا: بلا تعليقه ينتهي التحميل قبل أن تُقاس حالة «لم يُحمَّل بعد»
function deferred() {
  let release: () => void = () => {};
  const promise = new Promise<void>((resolve) => {
    release = resolve;
  });

  return { promise, release: () => release() };
}

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return (
    <QueryClientProvider client={client}>
      <AuthProvider>
        <CurrencySourceProvider>{children}</CurrencySourceProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  clearTokens();
  setTokens(TOKENS);
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("C — مصدر سجل العملات وحالته الثالثة", () => {
  it("C01: أثناء التحميل ⟵ لا رقم **ولا إنذار عملة مجهولة**", async () => {
    const gate = deferred();
    vi.stubGlobal("fetch", vi.fn(async () => {
      await gate.promise;
      return page([IQD]);
    }));

    render(<MoneyDisplay amount="1000.0000" currencyId={IQD.id} />, { wrapper });

    // ‏الادعاء الموجب: الإنذار **غائب** — وهو ما يميّز هذه الجولة كلها
    expect(screen.queryByText(ALERT)).toBeNull();

    // ‏والسالب معه: لا رقم بلا عملته (R-RPT-06 قائم في حالة التحميل أيضاً)
    expect(document.body.textContent).not.toMatch(/1[,.]?0{3}/u);

    gate.release();

    await waitFor(() => expect(screen.getByText(/د\.ع/u)).not.toBeNull());
  });

  it("C02: بعد الجهوز، عملة غير موجودة ⟵ الإنذار يعود كما كان", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => page([IQD])));

    render(<MoneyDisplay amount="1000.0000" currencyId={USD.id} />, { wrapper });

    // ‏الحالة الثالثة **لا تُلغي** R-RPT-06 بل تمنع إطلاقه في غير موضعه
    expect(await screen.findByText(ALERT)).not.toBeNull();
  });

  it("C03: بعد الجهوز، عملة موجودة ⟵ المبلغ برمزها وخاناتها", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => page([IQD, USD])));

    render(<MoneyDisplay amount="1000.1250" currencyId={USD.id} />, { wrapper });

    // ‏الخانات من السجل المُحمَّل لا من ثابت — دولار بخانتين
    expect(await screen.findByText(/1,000\.13 \$/u)).not.toBeNull();
  });

  it("C04: المصدر يحمّل **كل الصفحات** بترويسة Bearer", async () => {
    const fetchMock = vi.fn(async (input: unknown, init?: RequestInit) => {
      void init;
      return String(input).includes("PageNumber=1") ? page([IQD], true) : page([USD]);
    });

    vi.stubGlobal("fetch", fetchMock);

    render(<MoneyDisplay amount="1000.1250" currencyId={USD.id} />, { wrapper });

    // ‏عملة الصفحة الثانية تُعرض — فالحلقة لم تتوقف عند الأولى
    expect(await screen.findByText(/1,000\.13 \$/u)).not.toBeNull();

    expect(fetchMock.mock.calls).toHaveLength(2);

    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).get("Authorization")).toBe(
      `Bearer ${TOKENS.accessToken}`);
  });

  it("C05: فشل المصدر ⟵ **لا إنذار «عملة مجهولة»** — لا يُنسب عطل الشبكة إلى البيانات", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => ({ ok: false, status: 500, json: async () => ({}) })));

    render(<MoneyDisplay amount="1000.0000" currencyId={IQD.id} />, { wrapper });

    // ‏**مهلة صريحة لا `waitFor` على النفي.** التأكيد السالب صادق عند اللحظة صفر
    // (لم يفشل الاستعلام بعد)، فـ`waitFor` كان يمرّ **فوراً وبالفراغ** قبل أن يستقر
    // الاستعلام أصلاً — وقد قِيس ذلك بعطل `C-2` الذي لم يُرسب شيئاً. فخّ `F04` نفسه
    await new Promise((resolve) => {
      setTimeout(resolve, 50);
    });

    // ‏«تعذّر تحميل العملات» ≠ «هذه العملة غير معروفة». والثانية تتهم البيانات
    // بما هو عطل في الشبكة، فيبحث المستخدم عن عملة مفقودة لا وجود لمشكلتها
    expect(screen.queryByText(ALERT)).toBeNull();

    expect(document.body.textContent).not.toMatch(/1[,.]?0{3}/u);
  });

  it("C06: بلا رمز ⟵ المصدر لا يجلب أصلاً", async () => {
    clearTokens();

    const fetchMock = vi.fn(async () => page([IQD]));
    vi.stubGlobal("fetch", fetchMock);

    render(<MoneyDisplay amount="1000.0000" currencyId={IQD.id} />, { wrapper });

    // ‏البوابة نفسها التي يحرسها `L08`: موجة 401 عند الإقلاع تُمنع من المصدر أيضاً
    await waitFor(() => expect(fetchMock).not.toHaveBeenCalled());
  });
});
