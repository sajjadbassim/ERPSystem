import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { JournalEntryScreen } from "./JournalEntryScreen";
import type { BranchItem } from "../api/useAllBranches";
import type { CompanyItem } from "../api/useCompany";
import { AuthProvider } from "../auth/AuthProvider";
import { clearTokens, setTokens } from "../auth/token-store";

// ‏مصفوفة الحالات `JE` (رأس شاشة القيد، ومعه `useCompany`).
//
// ‏البادئة `JE` لملف جديد (بادئة ↔ ملف). و`useCompany` بلا ملف اختبار خاصّ: مستهلكه
// الوحيد هذه الشاشة، وما يُدّعى عنه — **متى** يُنادى وبأيّ معرّف — سلوك الشاشة لا الهوك.
//
// ‏🔒 لا تأكيد سالب بلا شرط موجب يسبقه (قاعدة `B`).

const COMPANY_A = "0199a1f0-0000-7000-8000-00000000c0a1";
const COMPANY_B = "0199a1f0-0000-7000-8000-00000000c0b2";

// ‏فرعان في شركتين: فتنفيذٌ ينادي شركة **أول فرع** لا المختار يُكشف باختيار الثاني
const BRANCHES: BranchItem[] = [
  { id: "0199a1f0-0000-7000-8000-0000000000b1", companyId: COMPANY_A, code: "BR-01", name: "الفرع الرئيسي", isActive: true },
  { id: "0199a1f0-0000-7000-8000-0000000000b2", companyId: COMPANY_B, code: "BR-02", name: "فرع أربيل", isActive: true }
];

const LABELS = BRANCHES.map((branch) => `${branch.code} — ${branch.name}`);

const COMPANIES: Record<string, CompanyItem> = {
  [COMPANY_A]: {
    id: COMPANY_A,
    code: "CO-A",
    name: "الشركة أ",
    baseCurrencyId: "0199a1f0-0000-7000-8000-0000000000e1",
    baseCurrencyCode: "IQD",
    fxRoundingToleranceBase: "1.0000",
    isBaseCurrencyLocked: true,
    isActive: true
  },
  [COMPANY_B]: {
    id: COMPANY_B,
    code: "CO-B",
    name: "الشركة ب",
    baseCurrencyId: "0199a1f0-0000-7000-8000-0000000000e2",
    baseCurrencyCode: "USD",
    fxRoundingToleranceBase: "0.0100",
    isBaseCurrencyLocked: true,
    isActive: true
  }
};

const TRACE = "0HNOF2DK5ILLO:00000002";
const SERVER_MESSAGE = "الخدمة غير متاحة مؤقتاً.";

function stubApi(options: { companyFault?: boolean } = {}) {
  const fetchMock = vi.fn(async (input: unknown) => {
    const url = String(input);
    const company = /\/api\/companies\/([^/?]+)/u.exec(url);

    if (company !== null) {
      if (options.companyFault === true) {
        return {
          ok: false,
          status: 503,
          json: async () => ({ success: false, message: SERVER_MESSAGE, data: null, traceId: TRACE })
        };
      }

      return { ok: true, status: 200, json: async () => ({ success: true, data: COMPANIES[company[1]!] ?? null }) };
    }

    return { ok: true, status: 200, json: async () => ({ success: true, data: { data: BRANCHES, hasNextPage: false } }) };
  });

  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

function companyCalls(fetchMock: ReturnType<typeof stubApi>): string[] {
  return fetchMock.mock.calls.map((call) => String(call[0])).filter((url) => url.includes("/api/companies"));
}

function renderScreen() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <JournalEntryScreen />
      </AuthProvider>
    </QueryClientProvider>
  );
}

function branchInput() {
  return screen.getByLabelText("الفرع");
}

function openPopup(input: HTMLElement) {
  fireEvent.mouseDown(input);
  fireEvent.click(input);
}

async function openAndReadOptions(): Promise<string[]> {
  openPopup(branchInput());

  return (await screen.findAllByRole("option")).map((option) => option.textContent ?? "");
}

async function chooseBranch(label: string) {
  openPopup(branchInput());
  fireEvent.click(await screen.findByRole("option", { name: label }));
}

beforeEach(() => {
  setTokens({
    accessToken: "je-access",
    refreshToken: "je-refresh",
    accessTokenExpiresAt: "2026-09-26T10:00:00.0000000Z"
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("JournalEntryScreen — JE (رأس القيد)", () => {
  it("JE01: الحقول الأربعة بتسمياتها، والتاريخان من نوع date", () => {
    stubApi();
    renderScreen();

    expect(branchInput()).toBeInTheDocument();
    expect(screen.getByLabelText("تاريخ الترحيل")).toHaveAttribute("type", "date");
    expect(screen.getByLabelText("تاريخ المستند")).toHaveAttribute("type", "date");
    expect(screen.getByLabelText("البيان")).toBeInTheDocument();
  });

  // ‏قرار 2026-09-26: `sourceModule` ثابتة ولا تُعرض. والمساواة التامة بين عناصر
  // الإدخال والحقول الأربعة هي الادعاء — لا بحثٌ عن تسمية بعينها يفوته حقلٌ بلا تسمية
  it("JE02: لا عنصر إدخال غير الحقول الأربعة — لا منتقي لمصدر القيد", () => {
    stubApi();
    const { container } = renderScreen();

    expect(Array.from(container.querySelectorAll("input, select, textarea"))).toEqual([
      branchInput(),
      screen.getByLabelText("تاريخ الترحيل"),
      screen.getByLabelText("تاريخ المستند"),
      screen.getByLabelText("البيان")
    ]);
  });

  it("JE03: قبل اختيار فرع ⟵ لا نداء لأي شركة، ولا عملة دفاتر معروضة", async () => {
    const fetchMock = stubApi();
    renderScreen();

    // ‏الشرط الموجب: الفروع وصلت فعلاً. فشركة **مشتقّة منها** كانت ستُنادى الآن لو
    // اشتُقّت قبل الاختيار — وبلا هذا يمرّ النفي على شاشة لم تجلب شيئاً بعد
    expect(await openAndReadOptions()).toEqual(LABELS);

    expect(companyCalls(fetchMock)).toEqual([]);
    expect(screen.queryByText(/عملة الدفاتر/u)).toBeNull();
  });

  it("JE04: اختيار فرع ⟵ نداء واحد لشركته هو، وعملة دفاترها معروضة", async () => {
    const fetchMock = stubApi();
    renderScreen();

    // ‏الفرع **الثاني**: شركته غير شركة الأول، فالتقاط شركة أول فرع يُكشف هنا
    await chooseBranch(LABELS[1]!);

    expect(await screen.findByText("عملة الدفاتر: USD")).toBeInTheDocument();
    expect(companyCalls(fetchMock)).toEqual([`/api/companies/${COMPANY_B}`]);
  });

  // ‏قرار V08: `staleTime: Infinity` لأن عملة الأساس مقفلة بالقانون (R-BASE-01).
  // ‏⚠ **حدّ معلَن:** هذه الحالة تميّز «بيات» عن «صفر» ولا تميّز اللانهاية عن خمس دقائق
  it("JE05: العودة إلى فرع شركةٍ سبق جلبها ⟵ لا نداء ثانٍ لها", async () => {
    const fetchMock = stubApi();
    renderScreen();

    await chooseBranch(LABELS[0]!);
    await screen.findByText("عملة الدفاتر: IQD");

    await chooseBranch(LABELS[1]!);
    await screen.findByText("عملة الدفاتر: USD");

    await chooseBranch(LABELS[0]!);
    await screen.findByText("عملة الدفاتر: IQD");

    expect(companyCalls(fetchMock)).toEqual([`/api/companies/${COMPANY_A}`, `/api/companies/${COMPANY_B}`]);
  });

  it("JE06: فشل جلب الشركة (5xx) ⟵ رسالة الخادم ورقمه، لا فراغ", async () => {
    stubApi({ companyFault: true });
    renderScreen();

    await chooseBranch(LABELS[0]!);

    // ‏R-RPT-06 على المرجع لا على المبلغ: عملة دفاتر مجهولة تُعلَن لا تُسكَت
    expect(await screen.findByText(SERVER_MESSAGE)).toBeInTheDocument();
    expect(screen.getByText(new RegExp(TRACE, "u"))).toBeInTheDocument();
    expect(screen.queryByText(/عملة الدفاتر/u)).toBeNull();
  });
});
