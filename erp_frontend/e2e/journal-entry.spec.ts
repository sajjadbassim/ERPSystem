import { expect, test } from "@playwright/test";
import type { APIRequestContext, Locator, Page } from "@playwright/test";

import {
  DEV_PASSWORD,
  DEV_USER,
  countEntries,
  ensureAccount,
  ensureCurrency,
  ensureFiscalYearCovering,
  findAccountId,
  findBranchId,
  findCompanyId,
  findCurrencyId,
  findPostedEntry,
  login,
  newApiContext
} from "./erp-api";

// ‏‏`BJE` — شاشة القيد **حيّةً**: Chromium ⟵ Vite ⟵ وكيل ⟵ ErpApi ⟵ `ErpApi_E2E`.
//
// ‏البادئة: `B` للمتصفح كما في `BR01`، و`JE` بادئة الشاشة نفسها في اختبارات الوحدة.
// ملف جديد فبادئة جديدة (بادئة ↔ ملف)، ولا تصادم مع `BR` ولا مع `JE`.
//
// ‏⛔ **يكتب قيوداً مرحَّلة دائمة** (R-GL-03)، ويقفل عملة الأساس عند أول ترحيل
// ‏(R-BASE-02). فلا يُشغَّل إلا على `ErpApi_E2E` — القاعدة القابلة للتخلّص — **وحارسان
// يسبقان أي كتابة**: متغيّر بيئة يعلن الهدف، وحساب علامة لا يوجد إلا فيها. تشغيله
// الدقيق في `e2e/README.md`.

const E2E_DATABASE = "ErpApi_E2E";
const MARKER_CODE = "E2E-MARKER";

const BRANCH_LABEL = "DEV-01 — الفرع الرئيسي (تطوير)";

const ACCOUNTS = {
  iqdCash: { code: "1110", name: "صندوق الدينار", label: "1110 — صندوق الدينار" },
  cash: { code: "1100", name: "النقدية بالصندوق", label: "1100 — النقدية بالصندوق" },
  summary: { code: "1200", name: "الأصول المتداولة", label: "1200 — الأصول المتداولة" }
} as const;

const USD = { code: "USD", name: "دولار أمريكي", symbol: "$", decimalPlaces: 2 };
const CURRENCY_LABELS = { IQD: "د.ع — دينار عراقي", USD: "$ — دولار أمريكي" } as const;

// ‏رسالة الإجراء المخزَّن **بحرفها** (`AddJournalPostingProcedures.cs:245`) —
// والشاشة تنقلها ولا تؤلّفها
const UNBALANCED = "القيد غير متوازن. كل سطوره بعملة الدفاتر فلا مجال لباقي تقريب.";
const POSTED = "تم ترحيل القيد بنجاح";

// ‏تاريخ اليوم **محلياً** لا بـ`toISOString` (UTC): بعد منتصف الليل في بغداد يكون UTC
// ما زال في الأمس، فيقع الترحيل خارج اليوم المقصود
function localToday(): string {
  const now = new Date();
  const pad = (value: number) => String(value).padStart(2, "0");

  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

const TODAY = localToday();

// ‏‏`process` بلا أنواع Node في `tsconfig` المشروع، وإضافة `@types/node` تغيير تبعيات
// خارج `e2e/`. فيُقرأ عبر `globalThis` بنوع محلّي صريح — Playwright يعمل على Node فعلاً
function e2eDatabaseDeclared(): string | undefined {
  return (globalThis as { process?: { env: Record<string, string | undefined> } }).process?.env.ERP_E2E_DATABASE;
}

type Seeded = { api: APIRequestContext; token: string; branchId: string; iqdId: string; usdId: string };

let seeded: Seeded;

test.describe.configure({ mode: "serial" });

test.beforeAll(async () => {
  // ‏الحارس الأول — **إعلان**: من شغّل الخادم على قاعدة E2E يعلن ذلك هنا أيضاً
  if (e2eDatabaseDeclared() !== E2E_DATABASE) {
    throw new Error(
      `‏⛔ BJE يكتب قيوداً دائمة ولا يُشغَّل إلا على ${E2E_DATABASE}.\n`
      + `‏   اضبط ERP_E2E_DATABASE=${E2E_DATABASE} بعد تشغيل ErpApi عليها — الأمر في e2e/README.md.`);
  }

  const api = await newApiContext();
  const token = await login(api);

  // ‏الحارس الثاني — **بنيويّ**: حساب علامة أُنشئ يدوياً في `ErpApi_E2E` وحدها بعد قياس
  // اتصال الخادم بها بـSELECT. فإن كان الخادم على DEV رغم الإعلان، يرسب هنا **قبل أي كتابة**
  try {
    await findAccountId(api, token, MARKER_CODE);
  } catch {
    throw new Error(
      `‏⛔ لا حساب علامة \`${MARKER_CODE}\` في القاعدة التي يتّصل بها ErpApi — فهي ليست ${E2E_DATABASE}.\n`
      + "‏   لا كتابة. راجع سلسلة الاتصال في e2e/README.md.");
  }

  const companyId = await findCompanyId(api, token);
  const iqdId = await findCurrencyId(api, token, "IQD");
  const usdId = await ensureCurrency(api, token, USD);

  await ensureFiscalYearCovering(api, token, companyId, TODAY);

  await ensureAccount(api, token, companyId, { ...ACCOUNTS.iqdCash, currencyId: iqdId });
  await ensureAccount(api, token, companyId, { ...ACCOUNTS.cash, currencyId: null });
  await ensureAccount(api, token, companyId, { ...ACCOUNTS.summary, currencyId: null, isPostable: false });

  const branchId = await findBranchId(api, token, "DEV-01");

  seeded = { api, token, branchId, iqdId, usdId };
});

test.afterAll(async () => {
  await seeded?.api.dispose();
});

// ‏‏══ رحلة المتصفح ═══════════════════════════════════════════════════════════

async function openJournalEntry(page: Page) {
  await page.goto("/");
  await page.getByLabel("اسم المستخدم", { exact: true }).fill(DEV_USER);
  await page.getByLabel("كلمة المرور", { exact: true }).fill(DEV_PASSWORD);
  await page.getByRole("button", { name: "تسجيل الدخول" }).click();

  await page.getByRole("navigation", { name: "التنقّل" }).getByRole("button", { name: "قيد يومية" }).click();
  await expect(page.getByRole("heading", { name: "قيد يومية" })).toBeVisible();
}

// ‏‏`listbox` لا الصفحة: `<select>` الجانب في كل سطر يحمل `option` أيضاً
async function choose(page: Page, input: Locator, label: string) {
  await input.click();
  await page.getByRole("listbox").getByRole("option", { name: label, exact: true }).click();
  await expect(input).toHaveValue(label);
}

function line(page: Page, index: number): Locator {
  return page.getByRole("region", { name: "سطور القيد" }).getByRole("row").nth(index + 1);
}

type LineInput = { account: string; currency: string; amount: string; side: "debit" | "credit"; rate?: string };

async function fillLine(page: Page, index: number, input: LineInput) {
  const row = line(page, index);

  await choose(page, row.getByLabel("الحساب", { exact: true }), input.account);
  await choose(page, row.getByLabel("العملة", { exact: true }), input.currency);

  await row.getByLabel("المبلغ", { exact: true }).fill(input.amount);
  await row.getByLabel("الجانب", { exact: true }).selectOption(input.side);

  if (input.rate !== undefined) {
    await row.getByLabel("سعر الصرف", { exact: true }).fill(input.rate);
  }

  await row.getByLabel("تاريخ سعر الصرف", { exact: true }).fill(TODAY);
}

async function fillHeader(page: Page, description: string) {
  await choose(page, page.getByLabel("الفرع", { exact: true }), BRANCH_LABEL);
  await expect(page.getByText("عملة الدفاتر: IQD")).toBeVisible();

  await page.getByLabel("تاريخ الترحيل", { exact: true }).fill(TODAY);
  await page.getByLabel("البيان", { exact: true }).fill(description);
}

async function post(page: Page) {
  await page.getByRole("button", { name: "ترحيل القيد" }).click();
}

// ‏رقم المستند من رسالة النجاح — ثم يُطلب القيد به من الخادم، فلا يُصدَّق ادعاء الشاشة
async function postedDocumentNumber(page: Page): Promise<string> {
  const status = page.getByRole("status");

  await expect(status).toContainText(POSTED);

  const match = /رقم المستند: (\S+)/u.exec((await status.textContent()) ?? "");

  expect(match, "‏لا رقم مستند في رسالة النجاح").not.toBeNull();

  return match![1]!;
}

// ‏‏══ السيناريوهات ═══════════════════════════════════════════════════════════

test("BJE01 — ترحيل ناجح بعملة الدفاتر: V08 حيّاً، والسعر مقفل على 1، والقيد على الخادم بسطرين", async ({ page }) => {
  await openJournalEntry(page);

  // ‏V08 حيّاً: `GET /api/companies/{id}` يُنادى **عند اختيار الفرع**، ويُرجع عملة الدفاتر
  const company = page.waitForResponse(
    (response) => response.url().includes("/api/companies/") && response.request().method() === "GET");

  await fillHeader(page, "قيد اختبار حيّ BJE01");

  const companyBody = (await (await company).json()) as { data?: { baseCurrencyId?: string } };

  expect(companyBody.data?.baseCurrencyId).toBe(seeded.iqdId);

  await fillLine(page, 0, { account: ACCOUNTS.iqdCash.label, currency: CURRENCY_LABELS.IQD, amount: "1000", side: "debit" });
  await fillLine(page, 1, { account: ACCOUNTS.cash.label, currency: CURRENCY_LABELS.IQD, amount: "1000", side: "credit" });

  // ‏القفل في الواجهة: السطران بعملة الدفاتر، فسعرهما 1 ومعطَّل
  for (const index of [0, 1]) {
    await expect(line(page, index).getByLabel("سعر الصرف", { exact: true })).toHaveValue("1");
    await expect(line(page, index).getByLabel("سعر الصرف", { exact: true })).toBeDisabled();
  }

  await post(page);

  const entry = await findPostedEntry(seeded.api, seeded.token, seeded.branchId, await postedDocumentNumber(page));

  expect(entry.description).toBe("قيد اختبار حيّ BJE01");
  expect(entry.sourceModule).toBe(1);
  expect(entry.lines).toHaveLength(2);
  expect(entry.lines?.map((posted) => posted.currencyCode)).toEqual(["IQD", "IQD"]);

  // ‏النموذج فُرِّغ: القيد لا يبقى جاهزاً لنقرة ثانية
  await expect(page.getByLabel("البيان", { exact: true })).toHaveValue("");
  await expect(page.getByLabel("الفرع", { exact: true })).toHaveValue("");
});

test("BJE02 — قيد غير متوازن: رسالة 50001 بحرفها، والنموذج باقٍ، ولا قيد جديد", async ({ page }) => {
  const before = await countEntries(seeded.api, seeded.token, seeded.branchId);

  await openJournalEntry(page);
  await fillHeader(page, "قيد غير متوازن BJE02");

  await fillLine(page, 0, { account: ACCOUNTS.iqdCash.label, currency: CURRENCY_LABELS.IQD, amount: "1000", side: "debit" });
  await fillLine(page, 1, { account: ACCOUNTS.cash.label, currency: CURRENCY_LABELS.IQD, amount: "900", side: "credit" });

  await post(page);

  await expect(page.getByRole("alert").filter({ hasText: UNBALANCED })).toHaveText(UNBALANCED);
  await expect(page.getByLabel("البيان", { exact: true })).toHaveValue("قيد غير متوازن BJE02");

  expect(await countEntries(seeded.api, seeded.token, seeded.branchId)).toBe(before);
});

test("BJE03 — سطر بالدولار بسعر يدويّ وسطر بعملة الدفاتر", async ({ page }) => {
  // ‏‏**الحساب هنا بيد الاختبار لا بالواجهة (R-API-01):** 10.00 USD × 1320 = 13200 IQD
  // ‏بالضبط، فلا باقي تقريب — ولا حاجة لحساب فرق التقريب (50003 لا يُطلق إلا بباقٍ ≠ 0،
  // ‏`AddJournalPostingProcedures.cs:241`). والدولار على `1100` لأن `1110` مقيَّد بالدينار (50011)
  const usdAmount = "10.00";
  const rate = "1320";
  const iqdEquivalent = "13200";

  await openJournalEntry(page);
  await fillHeader(page, "قيد بالدولار BJE03");

  await fillLine(page, 0, { account: ACCOUNTS.cash.label, currency: CURRENCY_LABELS.USD, amount: usdAmount, side: "debit", rate });
  await fillLine(page, 1, { account: ACCOUNTS.iqdCash.label, currency: CURRENCY_LABELS.IQD, amount: iqdEquivalent, side: "credit" });

  await expect(line(page, 0).getByLabel("سعر الصرف", { exact: true })).toBeEnabled();
  await expect(line(page, 1).getByLabel("سعر الصرف", { exact: true })).toBeDisabled();

  await post(page);

  const entry = await findPostedEntry(seeded.api, seeded.token, seeded.branchId, await postedDocumentNumber(page));
  const [usdLine, iqdLine] = entry.lines ?? [];

  expect(usdLine?.currencyCode).toBe("USD");
  expect(Number(usdLine?.debitFC)).toBe(10);
  expect(Number(usdLine?.exchangeRate)).toBe(1320);

  // ‏المقابل بعملة الدفاتر **حسبه الخادم** — والاختبار يقارنه بحسابه اليدوي أعلاه
  expect(Number(usdLine?.debitBase)).toBe(13200);
  expect(Number(iqdLine?.creditBase)).toBe(13200);
});

// ‏**تغيّر ادعاؤها 2026-09-28** مع `forPosting`: كانت تقيس أن التجميعي **يظهر** ثم يرفضه
// الخادم بـ50009 — وقد قاست ذلك حيّاً. وصار التجميعي خارج منتقي السطر، **فمسار 50009 لم
// يعد مبلوغاً من الواجهة** — والخادم يبقى الحَكَم له (R-API-05)
test("BJE04 — الحساب التجميعي خارج منتقي سطر القيد، والقابلة للترحيل حاضرة كلها", async ({ page }) => {
  await openJournalEntry(page);

  await line(page, 0).getByLabel("الحساب", { exact: true }).click();

  const options = (await page.getByRole("listbox").getByRole("option").allTextContents()).sort();

  // ‏المساواة لا الغياب وحده: حضور القابلين للترحيل **وغياب** `1200` وحساب العلامة
  // التجميعي معاً — فلا تمرّ على قائمة فارغة
  expect(options).toEqual([ACCOUNTS.cash.label, ACCOUNTS.iqdCash.label].sort());
});
