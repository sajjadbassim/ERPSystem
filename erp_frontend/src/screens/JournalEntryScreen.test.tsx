import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { JournalEntryScreen } from "./JournalEntryScreen";
import type { AccountItem } from "../api/useAccounts";
import type { BranchItem } from "../api/useAllBranches";
import type { CompanyItem } from "../api/useCompany";
import { AuthProvider } from "../auth/AuthProvider";
import { clearTokens, setTokens } from "../auth/token-store";
import { CurrencySourceProvider } from "../currency/currency-source";
import type { RegisteredCurrency } from "../currency/currency-registry";

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

// ‏عملة دفاتر الشركة أ **هي** `IQD` هنا — بمعرّفها في `COMPANIES` — فسطرٌ بها مقفل
const IQD: RegisteredCurrency = { id: COMPANIES[COMPANY_A]!.baseCurrencyId, code: "IQD", name: "دينار عراقي", symbol: "د.ع", decimalPlaces: 0, isActive: true };
const USD: RegisteredCurrency = { id: COMPANIES[COMPANY_B]!.baseCurrencyId, code: "USD", name: "دولار", symbol: "$", decimalPlaces: 2, isActive: true };

const CURRENCY_LABELS = { IQD: "د.ع — دينار عراقي", USD: "$ — دولار" } as const;

const ACCOUNTS: AccountItem[] = ["1110 — الصندوق", "1120 — المصرف", "2110 — المورّدون"].map((label, index) => {
  const [code, name] = label.split(" — ");

  return {
    id: `0199a1f0-0000-7000-8000-0000000000a${index + 1}`,
    companyId: COMPANY_A,
    code: code!,
    name: name!,
    accountType: 1,
    normalBalance: 0,
    isPostable: true,
    isActive: true,
    currencyId: null
  };
});

const ACCOUNT_LABELS = ACCOUNTS.map((account) => `${account.code} — ${account.name}`);

function page(data: unknown[]) {
  return { success: true, data: { data, hasNextPage: false } };
}

type StubResponse = { ok: boolean; status: number; json: () => Promise<unknown> };

// ‏نصّان **لا تؤلّفهما الواجهة** — رسالة الإجراء في 50001 بحرفها، ورسالة المتحكّم عند
// النجاح بحرفها (`JournalEntriesController.cs:43`). فظهورهما على الشاشة لا يكون إلا نقلاً
const REJECTION = "القيد غير متوازن. كل سطوره بعملة الدفاتر فلا مجال لباقي تقريب.";
const POSTED = "تم ترحيل القيد بنجاح";
const DOCUMENT_NUMBER = "JV-BR01-2026-000017";

// ‏201 لا 200: المتحكّم يُرجع `CreatedAtAction` — و`openapi.json` يُعلن 200 (انحراف عقد
// مسجَّل). فالتثبيت على الواقع لا على العقد، والواجهة تفحص `ok` لا رمزاً بعينه
function postedResponse(): StubResponse {
  return {
    ok: true,
    status: 201,
    json: async () => ({ success: true, message: POSTED, data: { id: "0199a1f0-0000-7000-8000-00000000f001", documentNumber: DOCUMENT_NUMBER }, traceId: null })
  };
}

function rejectedResponse(): StubResponse {
  return { ok: false, status: 400, json: async () => ({ success: false, message: REJECTION, data: null, traceId: TRACE }) };
}

function stubApi(options: { companyFault?: boolean; post?: () => Promise<StubResponse>; accounts?: AccountItem[] } = {}) {
  const fetchMock = vi.fn(async (input: unknown, init?: RequestInit) => {
    const url = String(input);
    const company = /\/api\/companies\/([^/?]+)/u.exec(url);

    if (init?.method === "POST") {
      return await (options.post ?? (async () => postedResponse()))();
    }

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

    if (url.includes("/api/accounts")) {
      return { ok: true, status: 200, json: async () => page(options.accounts ?? ACCOUNTS) };
    }

    if (url.includes("/api/currencies")) {
      return { ok: true, status: 200, json: async () => page([IQD, USD]) };
    }

    return { ok: true, status: 200, json: async () => page(BRANCHES) };
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
        <CurrencySourceProvider>
          <JournalEntryScreen />
        </CurrencySourceProvider>
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

// ‏‏من **قائمة المنتقي** لا من الشاشة كلها: `<select>` الجانب في كل سطر يحمل
// ‏`<option>` بدور `option` أيضاً — عين ما أسقط `JE03` عرضاً تحت `JE-5`
async function openAndReadOptions(input: HTMLElement = branchInput()): Promise<string[]> {
  openPopup(input);

  return within(await screen.findByRole("listbox")).getAllByRole("option").map((option) => option.textContent ?? "");
}

async function choose(input: HTMLElement, label: string) {
  openPopup(input);
  fireEvent.click(within(await screen.findByRole("listbox")).getByRole("option", { name: label }));
}

async function chooseBranch(label: string) {
  await choose(branchInput(), label);
}

function lineRows(): HTMLElement[] {
  return within(screen.getByRole("region", { name: "سطور القيد" })).getAllByRole("row").slice(1);
}

function line(index: number) {
  return within(lineRows()[index]!);
}

async function chooseLine(index: number, field: "الحساب" | "العملة", label: string) {
  await choose(line(index).getByLabelText(field), label);
  await waitFor(() => expect(line(index).getByLabelText(field)).toHaveValue(label));
}

function postCalls(fetchMock: ReturnType<typeof stubApi>) {
  return fetchMock.mock.calls.filter((call) => call[1]?.method === "POST");
}

function postButton() {
  return screen.getByRole("button", { name: "ترحيل القيد" });
}

// ‏قيد صالح كاملاً. **والسطر الأول يحمل سعراً بائتاً بقصد:** يُدخَل بالدولار وسعره 1320،
// ثم تُبدَّل عملته إلى عملة الدفاتر آخراً — فيعرض 1 ويبقى في القيم 1320 (`JL07` على الشاشة)
async function fillValidEntry() {
  await chooseBranch(LABELS[0]!);
  await screen.findByText("عملة الدفاتر: IQD");

  fireEvent.change(screen.getByLabelText("تاريخ الترحيل"), { target: { value: "2026-09-28" } });
  fireEvent.change(screen.getByLabelText("البيان"), { target: { value: "قيد تسوية شهري" } });

  for (const [index, amount] of ["1000", "0.7576"].entries()) {
    await chooseLine(index, "الحساب", ACCOUNT_LABELS[index]!);
    await chooseLine(index, "العملة", CURRENCY_LABELS.USD);

    fireEvent.change(line(index).getByLabelText("المبلغ"), { target: { value: amount } });
    fireEvent.change(line(index).getByLabelText("سعر الصرف"), { target: { value: "1320" } });
    fireEvent.change(line(index).getByLabelText("تاريخ سعر الصرف"), { target: { value: "2026-09-28" } });
  }

  await chooseLine(0, "العملة", CURRENCY_LABELS.IQD);
  expect(line(0).getByLabelText("سعر الصرف")).toHaveValue("1");
}

const EXPECTED_PAYLOAD = {
  branchId: BRANCHES[0]!.id,
  postingDate: "2026-09-28",
  documentDate: null,
  description: "قيد تسوية شهري",
  sourceModule: 1,
  lines: [
    { accountId: ACCOUNTS[0]!.id, currencyId: IQD.id, exchangeRate: "1", exchangeRateDate: "2026-09-28", description: null, debitFC: "1000" },
    { accountId: ACCOUNTS[1]!.id, currencyId: USD.id, exchangeRate: "1320", exchangeRateDate: "2026-09-28", description: null, creditFC: "0.7576" }
  ]
};

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
  // الإدخال والحقول الأربعة هي الادعاء — لا بحثٌ عن تسمية بعينها يفوته حقلٌ بلا تسمية.
  //
  // ‏**تضييق 2026-09-27 على قسم الرأس:** كانت على الشاشة كلها، وصار للسطور عناصرها.
  // والادعاء نفسه على السطور تحمله `JE08` بالمساواة التامة كذلك
  it("JE02: لا عنصر إدخال في الرأس غير الحقول الأربعة — لا منتقي لمصدر القيد", () => {
    stubApi();
    renderScreen();

    const header = screen.getByRole("region", { name: "رأس القيد" });

    expect(Array.from(header.querySelectorAll("input, select, textarea"))).toEqual([
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

describe("JournalEntryScreen — JE (سطور القيد)", () => {
  // ‏الحدّ الأدنى سطران (50017)، فتبدأ الشاشة بهما. وحذفٌ ينزل تحتهما قيدٌ يرفضه
  // الخادم حتماً — فالحذف معطَّل عند الحدّ، للتجربة وحدها (R-API-05)
  it("JE07: سطران افتراضياً والحذف معطَّل عندهما، والإضافة تُمكّنه", () => {
    stubApi();
    renderScreen();

    expect(lineRows()).toHaveLength(2);

    for (const index of [0, 1]) {
      expect(line(index).getByRole("button", { name: "حذف السطر" })).toBeDisabled();
    }

    fireEvent.click(screen.getByRole("button", { name: "إضافة سطر" }));

    expect(lineRows()).toHaveLength(3);
    expect(line(1).getByRole("button", { name: "حذف السطر" })).toBeEnabled();
  });

  // ‏امتداد `JE02` إلى السطور، بالمساواة التامة: حقول السطر السبعة لا غير — فلا مصدر
  // قيد ولا مبلغ ثانٍ (عمود واحد لـ`<MoneyInput>`، والجانب داخله)
  it("JE08: عناصر إدخال السطر هي حقوله السبعة بترتيبها — ومبلغ واحد لا اثنان", () => {
    stubApi();
    renderScreen();

    const row = lineRows()[0]!;
    const cells = within(row);

    expect(Array.from(row.querySelectorAll("input, select, textarea"))).toEqual([
      cells.getByLabelText("الحساب"),
      cells.getByLabelText("العملة"),
      cells.getByLabelText("المبلغ"),
      cells.getByLabelText("الجانب"),
      cells.getByLabelText("سعر الصرف"),
      cells.getByLabelText("تاريخ سعر الصرف"),
      cells.getByLabelText("بيان السطر")
    ]);
  });

  // ‏**الادعاء: قيم السطر تتبعه لا موضعَه** — البيان (`register`) والحساب (متحكَّم به).
  // ‏⚠ **لا تحرس مفتاح الفهرس** (مقيس: `JE-8` لا يُسقطها) — تلك تحرسها `JE13`. وتحرس
  // حذف السطر الصحيح ونزوح القيم في النموذج (`JE-10`)
  it("JE09: حذف سطر من المنتصف لا يُزيح بيانه ولا حسابه إلى سطر آخر", async () => {
    stubApi();
    renderScreen();

    fireEvent.click(screen.getByRole("button", { name: "إضافة سطر" }));

    for (const [index, text] of ["للأول", "للثاني", "للثالث"].entries()) {
      fireEvent.change(line(index).getByLabelText("بيان السطر"), { target: { value: text } });
      await chooseLine(index, "الحساب", ACCOUNT_LABELS[index]!);
    }

    fireEvent.click(line(1).getByRole("button", { name: "حذف السطر" }));

    // ‏الشرط الموجب: سطران لا ثلاثة — الحذف وقع فعلاً
    expect(lineRows()).toHaveLength(2);

    expect(lineRows().map((row) => within(row).getByLabelText("بيان السطر"))).toEqual([
      expect.objectContaining({ value: "للأول" }),
      expect.objectContaining({ value: "للثالث" })
    ]);

    expect(lineRows().map((row) => (within(row).getByLabelText("الحساب") as HTMLInputElement).value)).toEqual([
      ACCOUNT_LABELS[0],
      ACCOUNT_LABELS[2]
    ]);
  });

  // ‏**ما يثبته `getRowId` على الشاشة — مقيس 2026-09-27:** نزعُه **لا يُسقط `JE09`**،
  // لأن حالة السطر كلها في النموذج: المنتقيات و`<MoneyInput>` متحكَّم بها، والبيان
  // تعيد RHF كتابته بالاسم. فلا شيء ينزح بمفتاح الفهرس اليوم — وهو أثر قرار «لا حالة
  // موازية» لا عيب. **وما يبقى لـ`getRowId` هو الهوية**: عناصر السطر تتبعه، فأيّ حالة
  // يحملها مكوّنٌ في خليةٍ غداً (تركيز، نصّ بحث لم يُختر، قائمة مفتوحة) تتبعه أيضاً.
  // ‏⚠ أُضيفت بعد الخضرة، ورُئيت حمراء تحت العطل `JE-8` لا على هيكل
  it("JE13: حذف سطر من المنتصف ⟵ عناصر ما بعده هي نفسها لا نسخ جديدة", async () => {
    stubApi();
    renderScreen();

    fireEvent.click(screen.getByRole("button", { name: "إضافة سطر" }));

    const third = {
      account: line(2).getByLabelText("الحساب"),
      amount: line(2).getByLabelText("المبلغ"),
      description: line(2).getByLabelText("بيان السطر")
    };

    fireEvent.click(line(1).getByRole("button", { name: "حذف السطر" }));

    expect(lineRows()).toHaveLength(2);
    expect(line(1).getByLabelText("الحساب")).toBe(third.account);
    expect(line(1).getByLabelText("المبلغ")).toBe(third.amount);
    expect(line(1).getByLabelText("بيان السطر")).toBe(third.description);
  });

  // ‏قرار V08 معاً في سطرين: القفل **لكل سطر** من مقارنة عملته بعملة الدفاتر، لا حالةٌ
  // واحدة للشاشة. وعملة الدفاتر من الفرع المختار (`useCompany`) لا من الرمز
  it("JE10: سطر بعملة الدفاتر مقفل على 1، وسطر أجنبيّ بجانبه حرّ", async () => {
    stubApi();
    renderScreen();

    await chooseBranch(LABELS[0]!);
    await screen.findByText("عملة الدفاتر: IQD");

    await chooseLine(0, "العملة", CURRENCY_LABELS.IQD);
    await chooseLine(1, "العملة", CURRENCY_LABELS.USD);

    expect(line(0).getByLabelText("سعر الصرف")).toBeDisabled();
    expect(line(0).getByLabelText("سعر الصرف")).toHaveValue("1");

    expect(line(1).getByLabelText("سعر الصرف")).toBeEnabled();

    // ‏الحرّ يقبل الكتابة فعلاً، لا مُمكَّنٌ شكلاً
    fireEvent.change(line(1).getByLabelText("سعر الصرف"), { target: { value: "1320" } });
    expect(line(1).getByLabelText("سعر الصرف")).toHaveValue("1320");
  });

  it("JE11: قبل معرفة عملة الدفاتر لا قفل — ولو اختيرت عملةٌ هي عملة الدفاتر فعلاً", async () => {
    stubApi();
    renderScreen();

    await chooseLine(0, "العملة", CURRENCY_LABELS.IQD);

    // ‏الشرط الموجب: العملة بلغت `<MoneyInput>` فعلاً — رمزها بجوار المبلغ
    expect(line(0).getByLabelText("المبلغ")).toHaveAccessibleDescription("د.ع");
    expect(line(0).getByLabelText("سعر الصرف")).toBeEnabled();
  });

  // ‏الوصل لا المنتقي: `<AccountPicker>` يرشّح بـ`forPosting` (`S12`–`S14`)، وهذه تحرس
  // أن سطور القيد **تمرّره**. والشرط الموجب أولاً: القابلة للترحيل حاضرة كلها
  it("JE20: منتقي حساب السطر لا يعرض الحساب التجميعي", async () => {
    const summary: AccountItem = { ...ACCOUNTS[0]!, id: "0199a1f0-0000-7000-8000-0000000000a9", code: "1000", name: "الأصول", isPostable: false };

    stubApi({ accounts: [...ACCOUNTS, summary] });
    renderScreen();

    expect(await openAndReadOptions(line(0).getByLabelText("الحساب"))).toEqual(ACCOUNT_LABELS);
  });

  // ‏قرار 2026-09-27: لا فرز في شبكة السطور — ترتيب السطور ترتيب القيد
  it("JE12: النقر على رؤوس الأعمدة لا يفرز السطور", () => {
    stubApi();
    renderScreen();

    fireEvent.change(line(0).getByLabelText("بيان السطر"), { target: { value: "ب" } });
    fireEvent.change(line(1).getByLabelText("بيان السطر"), { target: { value: "أ" } });

    const grid = within(screen.getByRole("region", { name: "سطور القيد" }));

    for (const header of grid.getAllByRole("columnheader")) {
      fireEvent.click(header);
      expect(header).not.toHaveAttribute("aria-sort");
    }

    expect(lineRows().map((row) => (within(row).getByLabelText("بيان السطر") as HTMLInputElement).value)).toEqual(["ب", "أ"]);
  });
});

describe("JournalEntryScreen — JE (الترحيل)", () => {
  // ‏طلب **واحد** يحمل الرأس والسطور (R-API-06)، وحمولته بالمساواة التامة: `sourceModule`
  // ‏1، والجانب مفتاحٌ لا صفر، والسطر المقفل بسعر 1 رغم السعر البائت في قيمه
  it("JE14: قيد صالح ⟵ POST واحد بالرأس والسطور معاً وبحمولة العقد حرفياً", async () => {
    const fetchMock = stubApi();
    renderScreen();

    await fillValidEntry();
    fireEvent.click(postButton());

    await waitFor(() => expect(postCalls(fetchMock)).toHaveLength(1));

    const [url, init] = postCalls(fetchMock)[0]!;

    expect(String(url)).toBe("/api/journal-entries");
    expect(JSON.parse(String(init?.body))).toEqual(EXPECTED_PAYLOAD);
  });

  // ‏**R-API-01 حرفياً:** لا توازن حيّ ولا مجموع في المتصفح — الخادم يرفض، ورسالته
  // تُعرض **بحرفها**. والقيم تبقى: الرفض لا يمحو ما أدخله المستخدم ليصحّحه
  it("JE15: رفض الخادم ⟵ رسالته بحرفها أعلى الشاشة، والقيد باقٍ كما أُدخل", async () => {
    stubApi({ post: async () => rejectedResponse() });
    renderScreen();

    await fillValidEntry();
    fireEvent.click(postButton());

    expect(await screen.findByText(REJECTION)).toBeInTheDocument();

    expect(screen.getByLabelText("البيان")).toHaveValue("قيد تسوية شهري");
    expect(line(0).getByLabelText("الحساب")).toHaveValue(ACCOUNT_LABELS[0]);
    expect(lineRows()).toHaveLength(2);
  });

  // ‏قرار 2026-09-28: رسالة الخادم ورقم المستند، ثم **تفريغ النموذج**. فالنموذج الممتلئ
  // بعد النجاح كان يدعو إلى نقرة ثانية تُرحّل القيد نفسه برقم مستند ثانٍ
  it("JE16: النجاح ⟵ رسالة الخادم ورقم المستند، والنموذج مفرَّغ إلى سطرين فارغين", async () => {
    stubApi();
    renderScreen();

    await fillValidEntry();
    fireEvent.click(postButton());

    const status = await screen.findByRole("status");

    expect(status).toHaveTextContent(POSTED);
    expect(status).toHaveTextContent(DOCUMENT_NUMBER);

    expect(screen.getByLabelText("البيان")).toHaveValue("");
    expect(branchInput()).toHaveValue("");
    expect(lineRows()).toHaveLength(2);
    expect(line(0).getByLabelText("الحساب")).toHaveValue("");
  });

  it("JE17: نقرتان متتاليتان أثناء الإرسال ⟵ طلب ترحيل واحد", async () => {
    let release: () => void = () => {};

    const fetchMock = stubApi({
      post: async () =>
        await new Promise<StubResponse>((resolve) => {
          release = () => resolve(postedResponse());
        })
    });

    renderScreen();

    await fillValidEntry();
    fireEvent.click(postButton());

    await waitFor(() => expect(postButton()).toBeDisabled());
    fireEvent.click(postButton());

    // ‏قيدٌ ثانٍ برقم مستند ثانٍ — لا يكشفه أي حارس في القاعدة، لأن كليهما سليم منفرداً
    expect(postCalls(fetchMock)).toHaveLength(1);

    release();
    await screen.findByRole("status");
  });

  // ‏قرار الطفرات الدائم (`query-config.ts`): فشل الشبكة لا يميّز «لم يصل» عن «وصل
  // ونُفِّذ وضاع الرد» — والإعادة في الثانية قيدٌ مكرَّر. فالإعادة قرار المستخدم
  it("JE18: فشل الشبكة ⟵ خطأ معروض وطلب واحد، بلا إعادة تلقائية", async () => {
    const fetchMock = stubApi({ post: async () => await Promise.reject(new TypeError("Failed to fetch")) });
    renderScreen();

    await fillValidEntry();
    fireEvent.click(postButton());

    expect(await screen.findByText("Failed to fetch")).toBeInTheDocument();
    expect(postCalls(fetchMock)).toHaveLength(1);
  });

  // ‏أخطاء الرأس تظهر على الشاشة أول مرة — كانت محروسة في المخطط وحده بلا زرّ
  it("JE19: نموذج فارغ ⟵ صفر طلب ترحيل، وأخطاء الرأس معروضة", async () => {
    const fetchMock = stubApi();
    renderScreen();

    fireEvent.click(postButton());

    expect(await screen.findByText("الفرع مطلوب.")).toBeInTheDocument();
    expect(screen.getByText("القيد اليدوي يتطلب وصفاً لا يقل عن خمسة أحرف.")).toBeInTheDocument();

    expect(postCalls(fetchMock)).toHaveLength(0);
  });
});
