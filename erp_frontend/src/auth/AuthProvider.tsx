import { createContext, useCallback, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";

import type { components } from "../../api-types/schema";
import { LOGIN_PATH } from "../api/http";
import { clearTokens, getTokens, setTokens } from "./token-store";
import type { TokenPair } from "./token-store";

export type AuthStatus = "anonymous" | "authenticated";

export type LoginCredentials = {
  // ‏الشكل من `LoginRequestDto`: الإلزاميان وحدهما. و`companyCode` اختياري في العقد
  // ولا يلزم لمستخدم واحد في شركة واحدة — يُضاف حين يظهر التباس فعلي
  userName: string;
  password: string;
};

export type AuthState = {
  status: AuthStatus;

  // ‏رسالة ظاهرة لا صمت. ⚠ وكونها **رسالة الخادم** بعينها بند مستقل — الدَّين ٦
  error: string | null;

  login: (credentials: LoginCredentials) => Promise<void>;
  logout: () => void;
};

type TokenEnvelope = components["schemas"]["ApiResponseOfTokenPairDto"];

// ‏رسالة **من تأليفنا بالضرورة**: لا استجابة من الخادم أصلاً فلا رسالة له تُنقل.
// وهي متميّزة عن رسالة الاعتماد الخاطئ عمداً — من يُقال له إن بياناته خاطئة وهي
// صحيحة يعيد إدخالها بلا نهاية بدل أن يبلّغ عن عطل. يحرسه `LGN03` و`LGN04`
const TRANSPORT_ERROR = "تعذّر الاتصال بالخادم. تحقّق من الاتصال ثم أعد المحاولة.";

const AuthContext = createContext<AuthState | null>(null);

// ‏غياب المزوّد يعني «غير مسجَّل» **قطعاً** لا رمياً ولا افتراضَ دخول. والاتجاه
// مقصود: لو أُرجع «مسجَّل» افتراضياً لَمرّت الاختبارات القائمة بلا تعديل، ولَسقطت
// بوابة الاستعلامات بصمت في الإنتاج. الفشل هنا **مغلق**
const ANONYMOUS: AuthState = {
  status: "anonymous",
  error: null,
  login: async () => {},
  logout: () => {}
};

export type AuthProviderProps = {
  children: ReactNode;
};

export function AuthProvider(props: AuthProviderProps) {
  // ‏الحالة الابتدائية **مشتقّة من المخزن**: هو مصدر الحقيقة الوحيد داخل الجلسة،
  // ومزوّدٌ يتجاهله كان سيفتح باب تعارض بين حالتين لنفس الشيء
  const [pair, setPair] = useState<TokenPair | null>(() => getTokens());
  const [error, setError] = useState<string | null>(null);

  const login = useCallback(async (credentials: LoginCredentials) => {
    setError(null);

    let response: Response;
    let body: TokenEnvelope;

    try {
      // ‏`fetch` مباشرةً لا `apiFetch`: الدخول لا يحمل ترويسة ولا يُجدَّد عليه
      response = await fetch(LOGIN_PATH, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(credentials)
      });

      body = (await response.json()) as TokenEnvelope;
    } catch {
      // ‏**الطبقتان تُلتقطان معاً بقصد:** `fetch` يرفض (لا شبكة، أو الوكيل ساقط)،
      // و`json()` يرمي (وصل ردّ لا يُفهم — HTML من وسيط مثلاً). وكلاهما «لم يصلنا
      // جواب مفهوم»، ولا يملك أحدهما ما يقوله للمستخدم أكثر من الآخر.
      //
      // ‏وقبل هذا الفرع كان الوعد **يُرفض بلا معالِج**: الشاشة تبقى كما هي والمستخدم
      // لا يعرف أن شيئاً وقع. يحرسه `LGN04`
      clearTokens();
      setPair(null);
      setError(TRANSPORT_ERROR);

      return;
    }

    const issued = body.data ?? null;

    if (!response.ok || issued === null) {
      // ‏الإسقاط صريح: استجابة فاشلة لا تترك رمزاً قديماً يوهم بجلسة قائمة
      clearTokens();
      setPair(null);
      setError(body.message ?? "تعذّر تسجيل الدخول.");

      return;
    }

    setTokens(issued);
    setPair(issued);
  }, []);

  const logout = useCallback(() => {
    clearTokens();
    setPair(null);
    setError(null);
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      status: pair === null ? "anonymous" : "authenticated",
      error,
      login,
      logout
    }),
    [pair, error, login, logout]);

  return <AuthContext.Provider value={value}>{props.children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  return useContext(AuthContext) ?? ANONYMOUS;
}
