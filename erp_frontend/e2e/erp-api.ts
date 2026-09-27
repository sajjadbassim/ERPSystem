import { request } from "@playwright/test";
import type { APIRequestContext } from "@playwright/test";

import type { components } from "../api-types/schema";

// ‏عنوان الـAPI الحيّ — نفس الهدف الذي يوكّل إليه `vite.config.ts` حرفاً بحرف.
// ‏ولا يُشتق من إعداد Vite برمجياً: ملف الإعداد يُقيَّم بمحمّل Vite لا بـNode، وقراءته
// من هنا كانت ستستدعي المحمّل كاملاً لأجل سلسلة واحدة.
export const API_BASE = "https://localhost:7175";

// ‏بيانات دخول التطوير من `ErpApi.DevSeedTool/README.md` — محلية معلَنة، لا سرّ.
// ‏وهي في ملف متعقَّب عن قصد: الأداة نفسها تطبعها في وثيقتها، وإخفاؤها هنا كان
// سيوهم بأنها اعتماد إنتاج.
export const DEV_USER = "devadmin";
export const DEV_PASSWORD = "Dev@Local!2026";

type TokenEnvelope = components["schemas"]["ApiResponseOfTokenPairDto"];
type AccountsEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfAccountResponseDto"];
type AccountEnvelope = components["schemas"]["ApiResponseOfAccountResponseDto"];
type CurrenciesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfCurrencyResponseDto"];
type CompaniesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfCompanyResponseDto"];
type CurrencyEnvelope = components["schemas"]["ApiResponseOfCurrencyResponseDto"];
type BranchesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfBranchResponseDto"];
type FiscalYearsEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfFiscalYearResponseDto"];
type FiscalYearEnvelope = components["schemas"]["ApiResponseOfFiscalYearResponseDto"];
type EntriesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfJournalEntryListItemDto"];
type EntryEnvelope = components["schemas"]["ApiResponseOfJournalEntryResponseDto"];

type Account = NonNullable<NonNullable<AccountEnvelope["data"]>>;

export type PostedEntry = NonNullable<EntryEnvelope["data"]>;

// ‏`ignoreHTTPSErrors` **هنا وحده، ولا يناقض قرار المتصفح**: شهادة التطوير موقَّعة
// ذاتياً، وهذا نداء من Node إلى منفذ الـAPI المشفَّر مباشرةً — نفس ما يفعله الوكيل
// بـ`secure: false`. أمّا المتصفح في `BR01` فيكلّم Vite على `http://localhost:5173`
// ولا يرى الشهادة أصلاً، فيبقى **بلا `ignoreHTTPSErrors`** كما قُرّر. وخلطهما كان
// سيُخفي خطأ شهادة حقيقياً في مسار المتصفح يوماً.
export async function newApiContext(): Promise<APIRequestContext> {
  return request.newContext({ baseURL: API_BASE, ignoreHTTPSErrors: true });
}

export async function login(api: APIRequestContext): Promise<string> {
  const response = await api.post("/api/auth/login", {
    data: { userName: DEV_USER, password: DEV_PASSWORD }
  });

  if (!response.ok()) {
    throw new Error(
      `‏فشل دخول \`${DEV_USER}\` بالحالة ${response.status()}.\n`
      + "‏شغّل بذرة التطوير أولاً:\n"
      + "  ASPNETCORE_ENVIRONMENT=Development dotnet run --project ErpApi.DevSeedTool");
  }

  const body = (await response.json()) as TokenEnvelope;
  const accessToken = body.data?.accessToken;

  if (accessToken === undefined) {
    throw new Error("‏استجابة الدخول بلا `accessToken` — العقد تغيّر أو الخادم غير متوقَّع.");
  }

  return accessToken;
}

function authHeaders(accessToken: string): Record<string, string> {
  return { Authorization: `Bearer ${accessToken}` };
}

// ‏حلقة الصفحات لا صفحة واحدة: `MaxPageSize = 100` سقفٌ يُقلَّص إليه الطلب صامتاً،
// فقاعدة فيها 101 حساباً كانت ستُخفي المطلوب ويُنشئه البذر مكرَّراً.
async function fetchAllAccounts(api: APIRequestContext, accessToken: string): Promise<Account[]> {
  const accounts: Account[] = [];

  for (let pageNumber = 1; pageNumber <= 100; pageNumber += 1) {
    const response = await api.get(
      `/api/accounts?PageNumber=${pageNumber}&PageSize=100`,
      { headers: authHeaders(accessToken) });

    if (!response.ok()) {
      throw new Error(`‏تعذّر جلب الحسابات بالحالة ${response.status()}.`);
    }

    const page = ((await response.json()) as AccountsEnvelope).data;
    accounts.push(...(page?.data ?? []));

    if (page?.hasNextPage !== true) {
      return accounts;
    }
  }

  throw new Error("‏تجاوز جلب الحسابات مئة صفحة.");
}

export async function findCurrencyId(
  api: APIRequestContext, accessToken: string, code: string): Promise<string> {
  const response = await api.get("/api/currencies?PageNumber=1&PageSize=100",
    { headers: authHeaders(accessToken) });

  if (!response.ok()) {
    throw new Error(`‏تعذّر جلب العملات بالحالة ${response.status()}.`);
  }

  const currencies = ((await response.json()) as CurrenciesEnvelope).data?.data ?? [];
  const match = currencies.find((currency) => currency.code === code);

  if (match === undefined) {
    throw new Error(
      `‏لا عملة برمز \`${code}\` في قاعدة التطوير.\n`
      + "‏شغّل `ErpApi.DevSeedTool` — هو الذي يبذر IQD والشركة والفرع والمستخدم.");
  }

  return match.id;
}

export async function findCompanyId(
  api: APIRequestContext, accessToken: string): Promise<string> {
  const response = await api.get("/api/companies?PageNumber=1&PageSize=100",
    { headers: authHeaders(accessToken) });

  if (!response.ok()) {
    throw new Error(`‏تعذّر جلب الشركات بالحالة ${response.status()}.`);
  }

  const companies = ((await response.json()) as CompaniesEnvelope).data?.data ?? [];
  const first = companies[0];

  if (first === undefined) {
    throw new Error("‏لا شركة في قاعدة التطوير — شغّل `ErpApi.DevSeedTool`.");
  }

  return first.id;
}

export type AccountSpec = {
  code: string;
  name: string;
  currencyId: string | null;

  // ‏اختياري، والافتراض `true` كما كان — فلا يتغيّر سلوك `BR01`. ووُجد لحاجة واحدة:
  // حساب تجميعي لـ`BJE04` (50009)
  isPostable?: boolean;
};

// ‏**إدراجيّ حصراً، بالمفتاح الطبيعي** — نفس عقد `ErpApi.DevSeedTool` نصّاً: يبحث
// بالرمز ويُنشئ إن غاب، **ولا يُعدّل صفاً قائماً أبداً**.
//
// ‏وحين يوجد الصف بربط عملة مخالف للمتوقَّع **يُرمى الخطأ ولا يُصلَح**: التعديل
// الصامت كان سيجعل الاختبار يمرّ على قاعدة حالتها غير التي يصفها، وهو الفشل المفتوح
// بعينه. والرسالة تقول ما وُجد وما كان متوقَّعاً، فيقرّر الإنسان.
export async function ensureAccount(
  api: APIRequestContext,
  accessToken: string,
  companyId: string,
  spec: AccountSpec): Promise<void> {
  const existing = (await fetchAllAccounts(api, accessToken))
    .find((account) => account.code === spec.code);

  const isPostable = spec.isPostable ?? true;

  if (existing !== undefined) {
    const actual = existing.currencyId ?? null;

    if (actual !== spec.currencyId) {
      throw new Error(
        `‏الحساب \`${spec.code}\` موجود بربط عملة مخالف: `
        + `المنتظَر ${spec.currencyId ?? "بلا عملة"} والموجود ${actual ?? "بلا عملة"}.\n`
        + "‏والبذر لا يُعدّل صفاً قائماً. صحّح الصف يدوياً أو احذف القاعدة وأعد البذر.");
    }

    if (existing.isPostable !== isPostable) {
      throw new Error(
        `‏الحساب \`${spec.code}\` موجود بقابلية ترحيل مخالفة: المنتظَر ${String(isPostable)} `
        + `والموجود ${String(existing.isPostable)}. والبذر لا يُعدّل صفاً قائماً.`);
    }

    return;
  }

  // ‏`accountType: 1` (أصل) و`normalBalance: 0` (مدين) — والخدمة ترفض تناقضهما،
  // فالقيمتان ليستا اعتباطيتين بل الزوج الوحيد الصالح لحساب صندوق.
  const response = await api.post("/api/accounts", {
    headers: authHeaders(accessToken),
    data: {
      companyId,
      code: spec.code,
      name: spec.name,
      accountType: 1,
      normalBalance: 0,
      isPostable,
      currencyId: spec.currencyId
    }
  });

  if (response.status() !== 201 && !response.ok()) {
    throw new Error(
      `‏تعذّر إنشاء الحساب \`${spec.code}\` بالحالة ${response.status()}: `
      + (await response.text()));
  }
}

// ‏‏══ مساعدات قاعدة E2E وحدها (`BJE`) ══════════════════════════════════════════
// ‏كلها **إدراجية بالمفتاح الطبيعي** على عقد `ensureAccount` نفسه، ولا تُستدعى إلا بعد
// ‏`assertE2eDatabase` في `journal-entry.spec.ts`.

export async function findAccountId(
  api: APIRequestContext, accessToken: string, code: string): Promise<string> {
  const match = (await fetchAllAccounts(api, accessToken)).find((account) => account.code === code);

  if (match === undefined) {
    throw new Error(`‏لا حساب برمز \`${code}\` بعد البذر.`);
  }

  return match.id;
}

export async function findBranchId(
  api: APIRequestContext, accessToken: string, code: string): Promise<string> {
  const response = await api.get("/api/branches?PageNumber=1&PageSize=100", { headers: authHeaders(accessToken) });

  if (!response.ok()) {
    throw new Error(`‏تعذّر جلب الفروع بالحالة ${response.status()}.`);
  }

  const match = (((await response.json()) as BranchesEnvelope).data?.data ?? []).find((branch) => branch.code === code);

  if (match === undefined) {
    throw new Error(`‏لا فرع برمز \`${code}\` — شغّل \`ErpApi.DevSeedTool\` على قاعدة E2E.`);
  }

  return match.id;
}

export type CurrencySpec = { code: string; name: string; symbol: string; decimalPlaces: number };

export async function ensureCurrency(
  api: APIRequestContext, accessToken: string, spec: CurrencySpec): Promise<string> {
  try {
    return await findCurrencyId(api, accessToken, spec.code);
  } catch {
    // ‏غيابها هو الفرع المتوقَّع أول مرة — فيُنشأ
  }

  const response = await api.post("/api/currencies", {
    headers: authHeaders(accessToken),
    data: spec
  });

  if (!response.ok()) {
    throw new Error(`‏تعذّر إنشاء العملة \`${spec.code}\` بالحالة ${response.status()}: ${await response.text()}`);
  }

  const id = ((await response.json()) as CurrencyEnvelope).data?.id;

  if (id === undefined) {
    throw new Error(`‏استجابة إنشاء العملة \`${spec.code}\` بلا معرّف.`);
  }

  return id;
}

// ‏سنة تغطي التاريخ **وفترة عادية واحدة تغطي السنة كلها**.
//
// ‏الفترة سنوية لا شهرية بقصد: لا نقطة نهاية تسرد فترات سنة (`FiscalPeriodsController`
// ‏يعرض `{id}` وحده)، فوجود الفترة لا يُقاس — فتُنشأ **مع سنتها في الخطوة نفسها** ولا
// يُحتاج إلى قياسها بعدها. ولو كانت شهرية لاحتاج كل شهر جديد فترة لا يُعرف وجودها.
// ‏⚠ **حدّ معلَن:** إن أُنشئت السنة وفشل إنشاء فترتها في تشغيلة سابقة، فالسنة موجودة
// والفترة غائبة، ولا يُكشف ذلك هنا — يُكشف بـ50023 عند أول ترحيل
export async function ensureFiscalYearCovering(
  api: APIRequestContext, accessToken: string, companyId: string, date: string): Promise<void> {
  const list = await api.get("/api/fiscal-years?PageNumber=1&PageSize=100", { headers: authHeaders(accessToken) });

  if (!list.ok()) {
    throw new Error(`‏تعذّر جلب السنوات المالية بالحالة ${list.status()}.`);
  }

  const years = ((await list.json()) as FiscalYearsEnvelope).data?.data ?? [];

  if (years.some((year) => year.startDate <= date && date <= year.endDate && !year.isClosed)) {
    return;
  }

  const calendarYear = date.slice(0, 4);
  const startDate = `${calendarYear}-01-01`;
  const endDate = `${calendarYear}-12-31`;

  const created = await api.post("/api/fiscal-years", {
    headers: authHeaders(accessToken),
    data: { companyId, code: `FY${calendarYear}`, startDate, endDate }
  });

  if (!created.ok()) {
    throw new Error(`‏تعذّر إنشاء السنة المالية بالحالة ${created.status()}: ${await created.text()}`);
  }

  const fiscalYearId = ((await created.json()) as FiscalYearEnvelope).data?.id;

  const period = await api.post("/api/fiscal-periods", {
    headers: authHeaders(accessToken),
    // ‏`periodType: 1` = `Regular` (`FiscalPeriodType.cs:6`) — 50023 يطلب فترة **عادية**
    data: { fiscalYearId, periodNumber: 1, periodType: 1, name: `السنة ${calendarYear}`, startDate, endDate }
  });

  if (!period.ok()) {
    throw new Error(`‏تعذّر إنشاء الفترة المالية بالحالة ${period.status()}: ${await period.text()}`);
  }
}

export async function countEntries(
  api: APIRequestContext, accessToken: string, branchId: string): Promise<number> {
  const response = await api.get(
    `/api/journal-entries?branchId=${branchId}&PageNumber=1&PageSize=1`, { headers: authHeaders(accessToken) });

  if (!response.ok()) {
    throw new Error(`‏تعذّر جلب القيود بالحالة ${response.status()}.`);
  }

  return Number(((await response.json()) as EntriesEnvelope).data?.totalCount ?? 0);
}

// ‏القيد **من الخادم** لا من شاشة الواجهة: رقم المستند من رسالة النجاح، والقيد وسطوره
// من `GET /api/journal-entries/{id}` — فيُقاس ما رُحِّل فعلاً لا ما ادّعته الشاشة
export async function findPostedEntry(
  api: APIRequestContext, accessToken: string, branchId: string, documentNumber: string): Promise<PostedEntry> {
  for (let pageNumber = 1; pageNumber <= 100; pageNumber += 1) {
    const response = await api.get(
      `/api/journal-entries?branchId=${branchId}&PageNumber=${pageNumber}&PageSize=100`,
      { headers: authHeaders(accessToken) });

    if (!response.ok()) {
      throw new Error(`‏تعذّر جلب القيود بالحالة ${response.status()}.`);
    }

    const page = ((await response.json()) as EntriesEnvelope).data;
    const match = (page?.data ?? []).find((entry) => entry.documentNumber === documentNumber);

    if (match !== undefined) {
      const detail = await api.get(`/api/journal-entries/${match.id}`, { headers: authHeaders(accessToken) });
      const entry = ((await detail.json()) as EntryEnvelope).data;

      if (entry === null || entry === undefined) {
        throw new Error(`‏القيد ${documentNumber} مسرود ولا يُقرأ بمعرّفه.`);
      }

      return entry;
    }

    if (page?.hasNextPage !== true) {
      break;
    }
  }

  throw new Error(`‏لا قيد برقم ${documentNumber} على الخادم.`);
}
