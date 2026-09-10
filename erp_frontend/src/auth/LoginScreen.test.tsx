import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

import { AppRoot } from "../AppRoot";
import { App } from "../App";
import { clearTokens, getTokens, setTokens } from "./token-store";

// ‏مصفوفة الحالات `LGN` — شاشة الدخول وبوابتها.
//
// **الاختبار على التركيبة الحقيقية لا على المكوّن معزولاً:** كل حالة ترندر
// ‏`<AppRoot><App /></AppRoot>` — أي نفس ما يركّبه `main.tsx` بعد هذه الجولة.
// والسبب درسٌ مدفوع الثمن: `L09` أثبت أن `AppRoot` يوفّر المزوّدات، **ولم يثبت أن
// نقطة الدخول تستعمله** — فبقي الدَّين ٧ مفتوحاً وشُخِّص خطأً في تلخيص شفهيّ.
// فاختبارٌ على `<LoginScreen>` وحدها كان سيخضرّ والتطبيق لا يعرض شاشة دخول أصلاً.
//
// ⚠ **وما لا تثبته هذه المصفوفة كلها:** أن الخادم يقبل الرمز أو يردّ 200. كلها على
// ‏`fetch` مثبَّت. القياس الحيّ وحده يثبت ذلك، وهو مسجَّل في `FRONTEND-STATE.md`.

const TOKENS = {
  accessToken: "access-token-1",
  refreshToken: "refresh-token-1",
  accessTokenExpiresAt: "2026-09-10T10:00:00.0000000Z"
};

// ‏النصّان **منقولان من الخادم لا مؤلَّفان**:
//   `AuthMessages.InvalidCredentials` (`Core/Constants/AuthMessages.cs:8`)
//   ورسالة `ExceptionHandlingMiddleware.cs:41` بالصيغة نفسها ورقم تتبّع.
// ‏والاختبار لا يفرضهما على الخادم — يفرض أن الشاشة تعرض **ما وصلها** وأن
// النصّين لا يختلط أحدهما بالآخر. فتغيّر الصياغة في الخلفية لا يكسر شيئاً هنا
const CREDENTIAL_MESSAGE = "بيانات الدخول غير صحيحة.";
const SERVER_MESSAGE = "حدث خطأ غير متوقع في الخادم. الرجاء ذكر الرقم التالي عند التواصل مع الدعم: trace-1";

const CREDENTIALS = { userName: "devadmin", password: "Dev@Local!2026" };

type StubbedCall = { url: string; init: RequestInit | undefined };

function jsonResponse(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function tokenEnvelope() {
  return { success: true, message: null, data: TOKENS, traceId: null };
}

function branchesEnvelope() {
  return {
    success: true,
    message: null,
    traceId: null,
    data: {
      data: [{ id: "0199a1f0-0000-7000-8000-000000000001", companyId: "0199a1f0-0000-7000-8000-000000000002", code: "DEV-01", name: "الفرع الرئيسي", isActive: true }],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 100,
      totalPages: 1,
      hasNextPage: false
    }
  };
}

function stubRoutes(handler: (call: StubbedCall, index: number) => unknown) {
  let index = 0;

  const fetchMock = vi.fn(async (input: unknown, init?: RequestInit) =>
    handler({ url: String(input), init }, index++));

  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

// ‏الطريق المعتاد: الدخول ينجح وقائمة الفروع تُرجع فرعاً واحداً
function stubHappyPath() {
  return stubRoutes((call) =>
    call.url.includes("/api/auth/login") ? jsonResponse(tokenEnvelope()) : jsonResponse(branchesEnvelope()));
}

function renderApp() {
  return render(
    <AppRoot>
      <App />
    </AppRoot>);
}

function fillCredentials() {
  fireEvent.change(screen.getByLabelText("اسم المستخدم"), { target: { value: CREDENTIALS.userName } });
  fireEvent.change(screen.getByLabelText("كلمة المرور"), { target: { value: CREDENTIALS.password } });
}

function submit() {
  fireEvent.click(screen.getByRole("button", { name: "تسجيل الدخول" }));
}

function loginCalls(fetchMock: ReturnType<typeof stubRoutes>) {
  return fetchMock.mock.calls.filter((call) => String(call[0]).includes("/api/auth/login"));
}

beforeEach(() => {
  clearTokens();
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("LGN — شاشة الدخول وبوابتها", () => {
  it("LGN01: دخول ناجح ⟵ الحقلان وحدهما يُرسلان، والرمز يُخزَّن، والشاشة المحمية تحلّ محلّ النموذج", async () => {
    const fetchMock = stubHappyPath();

    renderApp();
    fillCredentials();
    submit();

    // ‏زوال النموذج هو الشاهد على أن البوابة تقرأ حالة المزوّد لا حالة محلية
    await waitFor(() => expect(screen.queryByLabelText("اسم المستخدم")).toBeNull());

    expect(getTokens()).toEqual(TOKENS);

    // ‏والحمولة بالمساواة لا بالاحتواء: `companyCode: ""` يتسلّل بسهولة من نموذج
    // ‏فارغ، والعقد يصفه `null | string` — فيصير الدخول مقصوراً على شركة اسمها ""
    const body = JSON.parse(String(loginCalls(fetchMock)[0]?.[1]?.body));

    expect(body).toEqual(CREDENTIALS);
  });

  it("LGN02: اعتماد خاطئ (401) ⟵ رسالة الخادم ظاهرة، وصفر رمز مخزَّن", async () => {
    stubRoutes(() => jsonResponse({ success: false, message: CREDENTIAL_MESSAGE, data: null, traceId: null }, 401));

    renderApp();
    fillCredentials();
    submit();

    expect(await screen.findByText(CREDENTIAL_MESSAGE)).not.toBeNull();

    // ‏والنموذج باقٍ: رسالةٌ تظهر ثم تختفي الشاشة كانت ستوهم بدخول ناجح
    expect(screen.queryByLabelText("اسم المستخدم")).not.toBeNull();

    expect(getTokens()).toBeNull();
  });

  it("LGN03: خطأ خادم (500) ⟵ رسالته هو، **لا** رسالة الاعتماد الخاطئ", async () => {
    stubRoutes(() => jsonResponse({ success: false, message: SERVER_MESSAGE, data: null, traceId: "trace-1" }, 500));

    renderApp();
    fillCredentials();
    submit();

    expect(await screen.findByText(SERVER_MESSAGE)).not.toBeNull();

    // ‏الادعاء الحقيقي لهذه الحالة: **لا تُخلط الرسالتان**. من يُخبَر أن بياناته
    // خاطئة وهي صحيحة يعيد إدخالها بلا نهاية بدل أن يبلّغ عن عطل
    expect(screen.queryByText(CREDENTIAL_MESSAGE)).toBeNull();

    expect(getTokens()).toBeNull();
  });

  it("LGN04: تعطّل الشبكة ⟵ رسالة ظاهرة لا رمي غير معالَج", async () => {
    // ‏`fetch` يرفض ولا يردّ استجابة — الحالة التي لا تُنتج `Response` أصلاً
    stubRoutes(() => {
      throw new TypeError("Failed to fetch");
    });

    const unhandled = vi.fn();
    window.addEventListener("unhandledrejection", unhandled);

    renderApp();
    fillCredentials();
    submit();

    // ‏الشاشة تقول شيئاً — أيّاً كان نصّه — بدل أن تبتلع الفشل صامتة
    await waitFor(() => expect(screen.getByRole("alert").textContent).not.toBe(""));

    // ‏وليست رسالة الاعتماد: انقطاع الشبكة ليس كلمة مرور خاطئة
    expect(screen.queryByText(CREDENTIAL_MESSAGE)).toBeNull();

    expect(getTokens()).toBeNull();

    window.removeEventListener("unhandledrejection", unhandled);

    expect(unhandled).not.toHaveBeenCalled();
  });

  it("LGN05: أثناء الطلب ⟵ الزرّ معطَّل، ونقرتان تُنتجان نداءً واحداً", async () => {
    // ‏استجابة معلَّقة بيدنا: بلا تعليقها ينتهي الطلب قبل أن يُقاس التعطيل
    // ‏المُهيِّئ دالة فارغة لا `null`: مع `null` يضيّق TypeScript النوع إلى `null`
    // عند الاستدعاء (لا يرى الإسناد داخل مُنفِّذ الوعد) فيُخرج `TS2349`
    let release: () => void = () => {};
    const pending = new Promise<void>((resolve) => {
      release = resolve;
    });

    const fetchMock = stubRoutes(async (call) => {
      if (call.url.includes("/api/auth/login")) {
        await pending;
        return jsonResponse(tokenEnvelope());
      }

      return jsonResponse(branchesEnvelope());
    });

    renderApp();
    fillCredentials();
    submit();

    const button = screen.getByRole("button", { name: "تسجيل الدخول" });

    await waitFor(() => expect(button).toBeDisabled());

    // ‏النقرة الثانية أثناء التعليق: بلا حجب تصير محاولتَي دخول متزامنتين
    submit();

    expect(loginCalls(fetchMock)).toHaveLength(1);

    release();

    await waitFor(() => expect(screen.queryByLabelText("اسم المستخدم")).toBeNull());
  });

  it("LGN06: جلسة قائمة ⟵ شاشة الدخول لا تُعرض أصلاً", () => {
    setTokens(TOKENS);
    stubHappyPath();

    renderApp();

    // ‏لا تحويل مسار — لا موجّه في المشروع (مقيس: صفر تبعية توجيه في `package.json`).
    // فالبوابة **منع عرض** لا إعادة توجيه، وهذا ما يُقاس هنا لا ما يُفترض
    expect(screen.queryByLabelText("اسم المستخدم")).toBeNull();
    expect(screen.queryByRole("button", { name: "تسجيل الدخول" })).toBeNull();
  });

  it("LGN07: حقول فارغة ⟵ رسائل تحت حقولها", async () => {
    stubHappyPath();

    renderApp();
    submit();

    // ‏الإسناد إلى الحقل لا مجرد ظهور النصّ — درس `F02`
    await waitFor(() =>
      expect(screen.getByLabelText("اسم المستخدم")).toHaveAccessibleDescription(/اسم المستخدم/u));

    expect(screen.getByLabelText("كلمة المرور")).toHaveAccessibleDescription(/كلمة المرور/u);
  });

  it("LGN09: حقول فارغة ⟵ صفر نداء دخول", async () => {
    const fetchMock = stubHappyPath();

    renderApp();
    submit();

    // ‏**مهلة صريحة لا انتظار ظهور الرسالة.** ولو كان الانتظار على الرسالة لصار
    // هذا الحارس يرسب على **بوابته** لا على ادعائه متى عُطِّل التحقق — وهو فخّ
    // ‏`F04` بعينه، وقد وقعت فيه `LGN07` القديمة حين جمعت الادعاءين في حالة واحدة.
    // فالفصل إلى حالتين هو ما جعل هذا الادعاء **قابلاً للرسوب** أصلاً
    await new Promise((resolve) => {
      setTimeout(resolve, 50);
    });

    // ‏والحجب هو الادعاء: رسالة تظهر **ويُرسل الطلب** أسوأ من لا رسالة
    expect(loginCalls(fetchMock)).toHaveLength(0);
  });

  it("LGN08: بعد الدخول ⟵ التطبيق ينادي نقطة محمية بترويسة Bearer ويعرض ما عاد", async () => {
    const fetchMock = stubHappyPath();

    renderApp();
    fillCredentials();
    submit();

    // ‏الشاهد على أن سلسلة النقل كاملة: النموذج ⟵ الرمز ⟵ الترويسة ⟵ محتوى معروض
    expect(await screen.findByText(/DEV-01/u)).not.toBeNull();

    const branchCall = fetchMock.mock.calls.find((call) => String(call[0]).includes("/api/branches"));

    expect(new Headers(branchCall?.[1]?.headers).get("Authorization")).toBe(`Bearer ${TOKENS.accessToken}`);
  });
});
