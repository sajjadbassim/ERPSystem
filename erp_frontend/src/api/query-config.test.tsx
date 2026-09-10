import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

import { AppRoot } from "../AppRoot";
import { clearTokens, setTokens } from "../auth/token-store";
import { useAllAccounts } from "./useAllAccounts";

// ‏مصفوفة `QC` — إعدادات `QueryClient` **مقيسة على السلوك لا على قراءة الكائن**.
//
// ‏فحص `client.getDefaultOptions().retry === 0` كان سيمرّ على إعداد مكتوب **ولا
// يُطبَّق** (لو أنشأ `AppRoot` عميلاً آخر، أو تجاوزه استعلام بقيمته). والحالتان
// أدناه تقيسان **عدد النداءات الفعلي** — وهو الأثر الذي يهمّ.
//
// ⚠ **والتركيب `<AppRoot>` لا عميل اختبار.** بقية الملفات تُنشئ عميلها بـ
// ‏`retry: false` محلياً — وهي **قيمة اختبار لا قرار إنتاج**. فلو قيست هنا لَقاست
// نفسها. هذه المصفوفة وحدها تقيس عميل التطبيق كما يُنشأ في المتصفح.

const TOKENS = {
  accessToken: "access-token-1",
  refreshToken: "refresh-token-1",
  accessTokenExpiresAt: "2026-09-10T10:00:00.0000000Z"
};

function accountsEnvelope() {
  return {
    success: true,
    message: null,
    traceId: null,
    data: {
      data: [{ id: "0199a1f0-0000-7000-8000-000000000001", companyId: "0199a1f0-0000-7000-8000-000000000002", code: "1010", name: "الصندوق", accountType: 1, normalBalance: 0, isPostable: true, isActive: true }],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 100,
      totalPages: 1,
      hasNextPage: false
    }
  };
}

function AccountsProbe() {
  const { status, accounts } = useAllAccounts();

  return <span>{`${status}:${accounts.length}`}</span>;
}

function accountCalls(fetchMock: ReturnType<typeof vi.fn>) {
  return fetchMock.mock.calls.filter((call) => String(call[0]).includes("/api/accounts"));
}

beforeEach(() => {
  clearTokens();
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("QC — إعدادات QueryClient المطبَّقة", () => {
  it("QC01: استعلام فاشل ⟵ نداء واحد لا أربعة (retry = 0)", async () => {
    setTokens(TOKENS);

    const fetchMock = vi.fn(async () => ({ ok: false, status: 500, json: async () => ({}) }));
    vi.stubGlobal("fetch", fetchMock);

    render(
      <AppRoot>
        <AccountsProbe />
      </AppRoot>);

    await waitFor(() => expect(screen.getByText(/^(pending|success|error):/u).textContent).toBe("error:0"));

    // ‏افتراض المكتبة **ثلاث إعادات** ⟵ أربعة نداءات. والقرار صفر ⟵ نداء واحد.
    // ‏والفرق ليس رقماً: الإعادة هنا تعيد **حلقة الصفحات كلها** من أولها
    expect(accountCalls(fetchMock)).toHaveLength(1);
  });

  it("QC02: بيانات مرجعية ⟵ تركيب ثانٍ لا يُعيد الجلب (staleTime معلَن)", async () => {
    setTokens(TOKENS);

    const fetchMock = vi.fn(async () => ({ ok: true, status: 200, json: async () => accountsEnvelope() }));
    vi.stubGlobal("fetch", fetchMock);

    // ‏العميل يعيش في `AppRoot`، فالتركيبان **داخل شجرة واحدة** لا في تركيبين
    // منفصلين: عميلان مختلفان لا يتشاركان ذاكرة، فكان الحارس سيمرّ بالفراغ
    function TwoMounts({ second }: { second: boolean }) {
      return (
        <>
          {second ? null : <AccountsProbe />}
          {second ? <AccountsProbe /> : null}
        </>
      );
    }

    const view = render(
      <AppRoot>
        <TwoMounts second={false} />
      </AppRoot>);

    await waitFor(() => expect(screen.getByText(/^(pending|success|error):/u).textContent).toBe("success:1"));

    expect(accountCalls(fetchMock)).toHaveLength(1);

    // ‏فكّ وإعادة تركيب المستهلك: مع `staleTime: 0` (افتراض المكتبة) يُعاد الجلب
    view.rerender(
      <AppRoot>
        <TwoMounts second />
      </AppRoot>);

    await waitFor(() => expect(screen.getByText(/^(pending|success|error):/u).textContent).toBe("success:1"));

    // ‏والبيانات المرجعية تُعلن بياتها صراحةً، فالنداء لا يتكرّر
    expect(accountCalls(fetchMock)).toHaveLength(1);
  });
});
