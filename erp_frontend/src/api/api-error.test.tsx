import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

import { AppRoot } from "../AppRoot";
import { App } from "../App";
import { clearTokens, setTokens } from "../auth/token-store";
import { isServerFault, readFailure } from "./api-error";

// ‏مصفوفة `T` — الدَّين ٦: رسالة الخادم و`traceId` لا يُهدران.
//
// ‏**القرار المعتمَد (2026-09-10):** الطبقة تحمل الاثنين **دائماً**، والعرض
// ‏**مشروط**: `traceId` يظهر في أعطال الخادم وحدها (5xx وفشل النقل)، لا في الأخطاء
// التي يصحّحها المستخدم بنفسه (401، 400، 403، 409).
//
// ‏والسبب مقيس من بند 6.1 في قواعد الباك-إند: `TraceId` وُجد «ليُربط بلاغ المستخدم
// بسجلات الخادم». فمن لا بلاغ له — من أخطأ كلمة مروره — رقمُ التتبّع بجواره ضجيج
// يُعلِّم المستخدم أن يتجاهله، فيضيع حين يلزم.
//
// ⚠ **وحقيقة مقيسة تُشكّل هذه المصفوفة:** غلاف `{message, traceId}` **ليس شكل كل
// خطأ**. كنسة على `ErpApi` أثبتت أن لا `OnChallenge` مخصَّصاً، فـ401 الصادر عن
// ‏`SetFallbackPolicy` يعود **بجسم فارغ** بلا غلاف ولا `traceId` — وهو المسار الأشيع
// في كل استعلام محميّ. يحرسه `T03`.

const TOKENS = {
  accessToken: "access-token-1",
  refreshToken: "refresh-token-1",
  accessTokenExpiresAt: "2026-09-10T10:00:00.0000000Z"
};

const TRACE = "0HNOF2DK5ILLO:00000001";

const SERVER_MESSAGE = "حدث خطأ غير متوقع في الخادم. الرجاء ذكر الرقم التالي عند التواصل مع الدعم: " + TRACE;
const CREDENTIAL_MESSAGE = "بيانات الدخول غير صحيحة.";

function jsonResponse(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

// ‏استجابة بجسم غير قابل للتحليل — 401 من بوابة المصادقة، أو 502 من الوكيل
function opaqueResponse(status: number) {
  return {
    ok: false,
    status,
    json: async () => {
      throw new SyntaxError("Unexpected end of JSON input");
    }
  };
}

function stubRoutes(handler: (url: string) => unknown) {
  const fetchMock = vi.fn(async (input: unknown) => handler(String(input)));
  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

function renderApp() {
  return render(
    <AppRoot>
      <App />
    </AppRoot>);
}

function loginWith(password = "Dev@Local!2026") {
  fireEvent.change(screen.getByLabelText("اسم المستخدم"), { target: { value: "devadmin" } });
  fireEvent.change(screen.getByLabelText("كلمة المرور"), { target: { value: password } });
  fireEvent.click(screen.getByRole("button", { name: "تسجيل الدخول" }));
}

beforeEach(() => {
  clearTokens();
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("T — رسالة الخادم و traceId", () => {
  it("T01: عطل خادم (500) ⟵ رسالته **ورقم تتبّعه** معاً على الشاشة", async () => {
    stubRoutes(() => jsonResponse({ success: false, message: SERVER_MESSAGE, data: null, traceId: TRACE }, 500));

    renderApp();
    loginWith();

    expect(await screen.findByText(SERVER_MESSAGE)).not.toBeNull();

    // ‏الرقم **معروض** لا محفوظاً في الذاكرة وحدها: بلا عرضه تنقطع الحلقة التي بُني
    // لها — مستخدم يبلّغ عن عطل، ولا مفتاح يربط بلاغه بأثر الخادم.
    //
    // ‏و**مرة واحدة لا مرتين**: رسالة الخمسمئة تحمل الرقم في متنها أصلاً
    // (`ExceptionHandlingMiddleware.cs:41`)، فإلحاقه ثانيةً يعرضه مكرَّراً بصيغتين
    expect(screen.getAllByText(new RegExp(TRACE, "u"))).toHaveLength(1);
  });

  it("T02: اعتماد خاطئ (401) ⟵ الرسالة وحدها، **ولا رقم تتبّع** ولو أرسله الخادم", async () => {
    // ‏الخادم **يرسل** `traceId` هنا فعلاً (مقيس حيّاً في جولة القياس)، والقرار
    // ‏أن تحجبه الواجهة — فالحالة تقيس **حجباً** لا غياباً
    stubRoutes(() => jsonResponse({ success: false, message: CREDENTIAL_MESSAGE, data: null, traceId: TRACE }, 401));

    renderApp();
    loginWith("خطأ");

    expect(await screen.findByText(CREDENTIAL_MESSAGE)).not.toBeNull();

    expect(screen.queryByText(new RegExp(TRACE, "u"))).toBeNull();
  });

  it("T03: جسم غير قابل للتحليل (401 من بوابة المصادقة) ⟵ رسالة مفهومة لا انهيار", async () => {
    // ‏الحالة الأشيع فعلياً: لا `OnChallenge` في الخادم، فالجسم فارغ تماماً
    stubRoutes(() => opaqueResponse(401));

    renderApp();
    loginWith();

    // ‏رسالة **ما** تظهر — والمصدر هنا نحن بالضرورة، لا الخادم
    await waitFor(() => expect(screen.getByRole("alert").textContent).not.toBe(""));

    expect(screen.queryByText(new RegExp(TRACE, "u"))).toBeNull();
  });

  it("T04: استعلام محميّ يفشل (500) ⟵ رسالة الخادم ورقمه، لا نصّ من تأليفنا", async () => {
    setTokens(TOKENS);

    stubRoutes((url) =>
      url.includes("/api/branches")
        ? jsonResponse({ success: false, message: SERVER_MESSAGE, data: null, traceId: TRACE }, 500)
        : jsonResponse({ success: true, data: null }));

    renderApp();

    // ‏قبل هذا الدَّين كان المعروض `تعذّر جلب الفروع (500).` — رسالة من تأليفنا
    // بالرمز، بينما الاستجابة تحمل ما هو أدقّ منها
    expect(await screen.findByText(SERVER_MESSAGE)).not.toBeNull();

    expect(screen.getAllByText(new RegExp(TRACE, "u"))).toHaveLength(1);
  });

  it("T07: عطل خادم برسالة **لا تحمل** الرقم ⟵ الرقم يُلحَق فيظهر", async () => {
    // ‏ليست كل رسائل الخمسمئة من الوسيط: 503 من وسيط، أو رسالة خدمة لا تذكر الرقم.
    // ‏وحينها الإلحاق هو ما يجعله مرئياً أصلاً — وبدونه يُهدر كما كان قبل هذا الدَّين
    stubRoutes(() => jsonResponse({ success: false, message: "الخدمة غير متاحة مؤقتاً.", data: null, traceId: TRACE }, 503));

    renderApp();
    loginWith();

    expect(await screen.findByText(/الخدمة غير متاحة مؤقتاً/u)).not.toBeNull();

    expect(screen.getAllByText(new RegExp(TRACE, "u"))).toHaveLength(1);
  });

  it("T05: `readFailure` تفكّ الغلاف وتسقط إلى نصّنا عند غيابه", async () => {
    const withEnvelope = await readFailure(
      jsonResponse({ success: false, message: SERVER_MESSAGE, traceId: TRACE }, 500) as unknown as Response,
      "احتياطي");

    expect(withEnvelope).toEqual({ status: 500, message: SERVER_MESSAGE, traceId: TRACE });

    const opaque = await readFailure(opaqueResponse(401) as unknown as Response, "احتياطي");

    expect(opaque).toEqual({ status: 401, message: "احتياطي", traceId: null });
  });

  it("T06: `isServerFault` تفصل عطل الخادم عمّا يصحّحه المستخدم", () => {
    // ‏الحدّ عند 500: ما دونه جواب **مقصود** من الخادم عن طلب المستخدم،
    // وما فوقه إقرار بأن الخادم نفسه أخفق
    expect(isServerFault({ status: 500, message: "", traceId: null })).toBe(true);
    expect(isServerFault({ status: 502, message: "", traceId: null })).toBe(true);

    // ‏لا استجابة أصلاً — فشل نقل. عطل ليس من صنع المستخدم
    expect(isServerFault({ status: null, message: "", traceId: null })).toBe(true);

    expect(isServerFault({ status: 401, message: "", traceId: null })).toBe(false);
    expect(isServerFault({ status: 400, message: "", traceId: null })).toBe(false);
    expect(isServerFault({ status: 403, message: "", traceId: null })).toBe(false);
    expect(isServerFault({ status: 409, message: "", traceId: null })).toBe(false);
  });
});
