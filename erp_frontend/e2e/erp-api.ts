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

type Account = NonNullable<NonNullable<AccountEnvelope["data"]>>;

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

  if (existing !== undefined) {
    const actual = existing.currencyId ?? null;

    if (actual !== spec.currencyId) {
      throw new Error(
        `‏الحساب \`${spec.code}\` موجود بربط عملة مخالف: `
        + `المنتظَر ${spec.currencyId ?? "بلا عملة"} والموجود ${actual ?? "بلا عملة"}.\n`
        + "‏والبذر لا يُعدّل صفاً قائماً. صحّح الصف يدوياً أو احذف القاعدة وأعد البذر.");
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
      isPostable: true,
      currencyId: spec.currencyId
    }
  });

  if (response.status() !== 201 && !response.ok()) {
    throw new Error(
      `‏تعذّر إنشاء الحساب \`${spec.code}\` بالحالة ${response.status()}: `
      + (await response.text()));
  }
}
