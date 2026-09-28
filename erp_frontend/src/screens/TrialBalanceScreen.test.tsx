import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { TrialBalanceScreen } from "./TrialBalanceScreen";
import type { TrialBalance } from "../api/useTrialBalance";
import type { BranchItem } from "../api/useAllBranches";
import { AuthProvider } from "../auth/AuthProvider";
import { clearTokens, setTokens } from "../auth/token-store";
import { CurrencySourceProvider } from "../currency/currency-source";
import type { RegisteredCurrency } from "../currency/currency-registry";

// ‏مصفوفة الحالات `TB` (شاشة ميزان المراجعة).
//
// ‏البادئة `TB` بالكنس: `T` لـ`api-error`، و`TB` لا تتصادم معها (نظير `CUR` بجانب `C`).
//
// ‏**عملة الدفاتر هنا الدولار لا الدينار بقصد:** رمزٌ مكتوب ثابتاً في الكود (`IQD`) يُكشف
// بها، ولا يمرّ بمطابقة الحالة الشائعة.
//
// ‏🔒 لا تأكيد سالب بلا شرط موجب يسبقه (قاعدة `B`).

const COMPANY = "0199a1f0-0000-7000-8000-00000000c0a1";

const BRANCH: BranchItem = { id: "0199a1f0-0000-7000-8000-0000000000b1", companyId: COMPANY, code: "BR-01", name: "الفرع الرئيسي", isActive: true };
const BRANCH_LABEL = `${BRANCH.code} — ${BRANCH.name}`;

const USD: RegisteredCurrency = { id: "0199a1f0-0000-7000-8000-0000000000e2", code: "USD", name: "دولار", symbol: "$", decimalPlaces: 2, isActive: true };
const IQD: RegisteredCurrency = { id: "0199a1f0-0000-7000-8000-0000000000e1", code: "IQD", name: "دينار عراقي", symbol: "د.ع", decimalPlaces: 0, isActive: true };

function money(amountBase: string) {
  return { amountBase, currencyId: USD.id, currencyCode: USD.code };
}

// ‏**المجموعان يخالفان مجموع الصفوف عمداً** (100 + 50 ≠ 999): الشاشة تعرض ما أرسله الخادم،
// فأي جمعٍ في المتصفح يُكشف هنا (R-API-01)
const BALANCE: TrialBalance = {
  basis: "BaseCurrency",
  branchId: BRANCH.id,
  baseCurrencyId: USD.id,
  baseCurrencyCode: "USD",
  rows: [
    { accountId: "0199a1f0-0000-7000-8000-0000000000a1", accountCode: "2010", accountName: "المورّدون", debitBalance: money("0.0000"), creditBalance: money("50.0000") },
    { accountId: "0199a1f0-0000-7000-8000-0000000000a2", accountCode: "1010", accountName: "الصندوق", debitBalance: money("100.0000"), creditBalance: money("0.0000") }
  ],
  totals: { totalDebit: money("999.0000"), totalCredit: money("888.0000") }
};

const EMPTY: TrialBalance = { ...BALANCE, rows: [], totals: { totalDebit: money("0.0000"), totalCredit: money("0.0000") } };

const SERVER_MESSAGE = "الخدمة غير متاحة مؤقتاً.";
const FORBIDDEN_MESSAGE = "لا تملك صلاحية الوصول إلى هذا الفرع.";

type Reply = { ok: boolean; status: number; json: () => Promise<unknown> };

function ok(data: unknown): Reply {
  return { ok: true, status: 200, json: async () => ({ success: true, data }) };
}

function page(data: unknown[]): Reply {
  return ok({ data, hasNextPage: false });
}

function stubApi(trialBalance: () => Promise<Reply>) {
  const fetchMock = vi.fn(async (input: unknown) => {
    const url = String(input);

    if (url.includes("/api/reports/trial-balance")) {
      return await trialBalance();
    }

    if (url.includes("/api/currencies")) {
      return page([USD, IQD]);
    }

    return page([BRANCH]);
  });

  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

function trialBalanceCalls(fetchMock: ReturnType<typeof stubApi>): string[] {
  return fetchMock.mock.calls.map((call) => String(call[0])).filter((url) => url.includes("/api/reports/trial-balance"));
}

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderScreen(client = newClient()) {
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <CurrencySourceProvider>
          <TrialBalanceScreen />
        </CurrencySourceProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}

async function chooseBranch() {
  const input = screen.getByLabelText("الفرع");

  fireEvent.mouseDown(input);
  fireEvent.click(input);
  fireEvent.click(within(await screen.findByRole("listbox")).getByRole("option", { name: BRANCH_LABEL }));
}

function bodyRows(): HTMLElement[] {
  return screen.getAllByRole("row").slice(1);
}

beforeEach(() => {
  setTokens({ accessToken: "tb-access", refreshToken: "tb-refresh", accessTokenExpiresAt: "2026-09-28T10:00:00.0000000Z" });
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("TrialBalanceScreen — TB (ميزان المراجعة)", () => {
  it("TB01: قبل اختيار فرع ⟵ «اختر فرعاً» ولا نداء للميزان إطلاقاً", async () => {
    const fetchMock = stubApi(async () => ok(BALANCE));
    renderScreen();

    // ‏الشرط الموجب: الفروع وصلت فعلاً — فلا يمرّ النفي على شاشة لم تجلب شيئاً بعد
    const input = screen.getByLabelText("الفرع");
    fireEvent.mouseDown(input);
    fireEvent.click(input);
    await screen.findByRole("option", { name: BRANCH_LABEL });

    expect(screen.getByText("اختر فرعاً لعرض ميزان المراجعة.")).toBeInTheDocument();
    expect(trialBalanceCalls(fetchMock)).toEqual([]);
  });

  // ‏الرأس (R-RPT-02) والرمز من الاستجابة (R-RPT-04)، وكل مبلغ عبر `<MoneyDisplay>` بعملته
  // من كائنه (R-UI-02) — والتنسيق `100.00 $` لا يُنتجه إلا المكوّن مع السجل
  it("TB02: الرأس يعلن عملة الدفاتر برمزها من الاستجابة، والمبالغ منسَّقة بعملتها", async () => {
    const fetchMock = stubApi(async () => ok(BALANCE));
    renderScreen();

    await chooseBranch();

    expect(await screen.findByText("الأساس: عملة الدفاتر (USD)")).toBeInTheDocument();
    expect(trialBalanceCalls(fetchMock)).toEqual([`/api/reports/trial-balance?branchId=${BRANCH.id}`]);

    // ‏بترتيب الخادم (`2010` قبل `1010`) — الشاشة لا تعيد الترتيب
    expect(bodyRows().map((row) => within(row).getAllByRole("cell").map((cell) => cell.textContent))).toEqual([
      ["2010", "المورّدون", "0.00 $", "50.00 $"],
      ["1010", "الصندوق", "100.00 $", "0.00 $"]
    ]);
  });

  it("TB03: المجموعان كما أرسلهما الخادم حرفياً — لا مجموع الصفوف", async () => {
    stubApi(async () => ok(BALANCE));
    renderScreen();

    await chooseBranch();

    const totals = await screen.findByRole("region", { name: "المجاميع" });

    expect(within(totals).getByText("إجمالي المدين:").parentElement).toHaveTextContent("إجمالي المدين: 999.00 $");
    expect(within(totals).getByText("إجمالي الدائن:").parentElement).toHaveTextContent("إجمالي الدائن: 888.00 $");
  });

  it("TB04: نجاح بلا صفوف ⟵ «لا حركة مرحَّلة لهذا الفرع» بلا تنبيه خطأ", async () => {
    stubApi(async () => ok(EMPTY));
    renderScreen();

    await chooseBranch();

    expect(await screen.findByText("لا حركة مرحَّلة لهذا الفرع.")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  // ‏نمط `B12`/`B13`: الفشل يُعلن بنصّ الخادم، و**لا يلبس ثوب الفراغ**
  it("TB05: فشل الجلب (5xx) ⟵ رسالة الخادم، ولا رسالة «لا حركة»", async () => {
    stubApi(async () => ({ ok: false, status: 503, json: async () => ({ success: false, message: SERVER_MESSAGE, data: null, traceId: null }) }));
    renderScreen();

    await chooseBranch();

    expect(await screen.findByText(SERVER_MESSAGE)).toBeInTheDocument();
    expect(screen.queryByText("لا حركة مرحَّلة لهذا الفرع.")).toBeNull();
  });

  it("TB06: رفض 403 ⟵ رسالة الخادم بحرفها", async () => {
    stubApi(async () => ({ ok: false, status: 403, json: async () => ({ success: false, message: FORBIDDEN_MESSAGE, data: null, traceId: null }) }));
    renderScreen();

    await chooseBranch();

    expect(await screen.findByText(FORBIDDEN_MESSAGE)).toBeInTheDocument();
  });

  it("TB07: أثناء التحميل ⟵ إعلان تحميل، ولا رسالة «لا حركة»", async () => {
    let release: () => void = () => {};
    stubApi(async () => await new Promise<Reply>((resolve) => { release = () => resolve(ok(EMPTY)); }));
    renderScreen();

    await chooseBranch();

    expect(await screen.findByText("جارٍ تحميل الميزان…")).toBeInTheDocument();
    expect(screen.queryByText("لا حركة مرحَّلة لهذا الفرع.")).toBeNull();

    release();
    expect(await screen.findByText("لا حركة مرحَّلة لهذا الفرع.")).toBeInTheDocument();
  });

  it("TB08: النقر على رؤوس الأعمدة لا يفرز", async () => {
    stubApi(async () => ok(BALANCE));
    renderScreen();

    await chooseBranch();
    await screen.findByText("الأساس: عملة الدفاتر (USD)");

    for (const header of screen.getAllByRole("columnheader")) {
      fireEvent.click(header);
      expect(header).not.toHaveAttribute("aria-sort");
    }

    expect(bodyRows().map((row) => within(row).getAllByRole("cell")[0]?.textContent)).toEqual(["2010", "1010"]);
  });

  // ‏بيانات الميزان تتغيّر مع كل ترحيل، فالدخول الثاني يجلب من جديد **ويعرض الجديد**
  // (`staleTime: 0`). والعميل نفسه بين الدخولين — كما في التطبيق
  it("TB09: دخول الشاشة ثانيةً ⟵ جلب جديد والأرقام الجديدة معروضة", async () => {
    let totalDebit = "999.0000";
    const fetchMock = stubApi(async () => ok({ ...BALANCE, totals: { ...BALANCE.totals, totalDebit: money(totalDebit) } }));
    const client = newClient();

    const first = renderScreen(client);
    await chooseBranch();
    expect(await screen.findByText("999.00 $")).toBeInTheDocument();
    first.unmount();

    totalDebit = "1234.0000";
    renderScreen(client);
    await chooseBranch();

    expect(await screen.findByText("1,234.00 $")).toBeInTheDocument();
    await waitFor(() => expect(trialBalanceCalls(fetchMock)).toHaveLength(2));
  });
});
