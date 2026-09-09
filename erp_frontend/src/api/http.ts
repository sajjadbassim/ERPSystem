import type { components } from "../../api-types/schema";
import { clearTokens, getTokens, setTokens } from "../auth/token-store";

export const LOGIN_PATH = "/api/auth/login";

export const REFRESH_PATH = "/api/auth/refresh";

type TokenEnvelope = components["schemas"]["ApiResponseOfTokenPairDto"];

// ‏تجديد واحد مشترك بين كل الطلبات المتزامنة. والسبب مقيس من العقد: `/api/auth/refresh`
// يُرجع **زوجاً كاملاً** لا رمز وصول وحده — أي أنه **يُدوِّر رمز التجديد**. فتجديدان
// متزامنان يعني أن الثاني يُبطل الأول، ويسقط أحد الطلبين بلا سبب ظاهر. يحرسه `L10`
let refreshInFlight: Promise<boolean> | null = null;

// ‏مسارا المصادقة لا يُجدَّد عليهما: 401 من `/login` بيانات خاطئة لا جلسة منتهية،
// و401 من `/refresh` هو فشل التجديد نفسه. وبلا هذا الاستثناء تدور الطبقة على نفسها
function isAuthPath(path: string): boolean {
  return path.startsWith(LOGIN_PATH) || path.startsWith(REFRESH_PATH);
}

function withAuthorization(init: RequestInit | undefined, accessToken: string): RequestInit {
  // ‏`Headers` لا كائن عاديّ: المستدعي قد يمرّر ترويساته بأي من الأشكال الثلاثة
  // التي يقبلها `RequestInit`، ودمجها يدوياً كان سيُسقط اثنين منها
  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${accessToken}`);

  return { ...init, headers };
}

async function performRefresh(): Promise<boolean> {
  const tokens = getTokens();

  if (tokens === null) {
    return false;
  }

  const response = await fetch(REFRESH_PATH, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: tokens.refreshToken })
  });

  if (!response.ok) {
    // ‏الإسقاط عند الفشل لا الإبقاء: رمزٌ ميت في المخزن يجعل كل طلب لاحق يعيد
    // الدورة نفسها بلا أمل، والواجهة لا تعرف أنها خارج الجلسة. يحرسه `L06`
    clearTokens();
    return false;
  }

  const body = (await response.json()) as TokenEnvelope;
  const pair = body.data ?? null;

  if (pair === null) {
    clearTokens();
    return false;
  }

  setTokens(pair);

  return true;
}

async function refreshOnce(): Promise<boolean> {
  refreshInFlight ??= performRefresh().finally(() => {
    refreshInFlight = null;
  });

  return await refreshInFlight;
}

// ‏المنفذ الوحيد لكل نداء إلى الـAPI. الترويسة تُضاف هنا لا في كل مستدعٍ — ونسيانها
// مرة واحدة كان سيُنتج 401 يبدو عطباً في الشاشة لا في الطبقة
export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  const tokens = getTokens();

  const response = await fetch(
    path,
    tokens === null ? (init ?? {}) : withAuthorization(init, tokens.accessToken));

  if (response.status !== 401 || isAuthPath(path) || getTokens() === null) {
    return response;
  }

  const refreshed = await refreshOnce();

  if (!refreshed) {
    return response;
  }

  const renewed = getTokens();

  if (renewed === null) {
    return response;
  }

  // ‏إعادة **واحدة** لا حلقة: 401 بعد التجديد يُعاد كما هو. والاستدعاء هنا لـ`fetch`
  // مباشرةً لا لـ`apiFetch`، فالتكرار الذاتي كان سيقصف الخادم. يحرسه `L07`
  return await fetch(path, withAuthorization(init, renewed.accessToken));
}
