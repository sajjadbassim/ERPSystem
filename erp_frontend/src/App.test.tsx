import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import { AppRoot } from "./AppRoot";
import { App } from "./App";
import { clearTokens, setTokens } from "./auth/token-store";

// ‏مصفوفة الحالات `NAV` — حالة التنقّل الداخلية بين الوجهات المحمية.
//
// ‏البادئة `NAV` لا `N`: `N-` محجوزة لأعطال `<MoneyInput>`، والملف جديد فيأخذ بادئة
// جديدة (بادئة ↔ ملف). والتركيبة الحقيقية `<AppRoot><App /></AppRoot>` لا المكوّن
// معزولاً — درس `LGN` نفسه.
//
// ‏⚠ **لا حالة لتسرّب الوجهة بين جلستين:** لا زرّ خروج في الواجهة، و`logout` بلا
// مستهلك. وبناء زرّ لأجل حالة اختبار اختراعُ مستهلك — مسجَّل في دفتر ما لم يُثبَت.

const TOKENS = {
  accessToken: "access-token-1",
  refreshToken: "refresh-token-1",
  accessTokenExpiresAt: "2026-09-10T10:00:00.0000000Z"
};

function pageEnvelope(data: unknown[]) {
  return {
    success: true,
    message: null,
    traceId: null,
    data: { data, totalCount: data.length, pageNumber: 1, pageSize: 100, totalPages: 1, hasNextPage: false }
  };
}

function stubApi() {
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: true, status: 200, json: async () => pageEnvelope([]) })));
}

function renderApp() {
  return render(
    <AppRoot>
      <App />
    </AppRoot>);
}

function navButton(name: string) {
  return screen.getByRole("button", { name });
}

// ‏‏`jsdom` يُبقي الـURL بين حالات الملف الواحد. فبلا إعادته يرث `NAV05` ما دفعته حالة
// سابقة، ويصير تأكيد `href` صادقاً بالفراغ — وقد قيس ذلك بالعطل `NAV-5` قبل هذا السطر
beforeEach(() => {
  clearTokens();
  window.history.replaceState(null, "", "/");
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("NAV — حالة التنقّل الداخلية", () => {
  it("NAV01: جلسة قائمة ⟵ شجرة الحسابات افتراضياً، وشريط التنقّل ظاهر", () => {
    setTokens(TOKENS);
    stubApi();

    renderApp();

    expect(screen.getByRole("heading", { name: "شجرة الحسابات" })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: "التنقّل" })).toBeInTheDocument();
  });

  // ‏«وحدها» هو الادعاء: شاشتان مركَّبتان وإحداهما مخفية بالـCSS تُبقيان استعلامات
  // الأولى حيّة، فالغياب من الشجرة يُقاس لا الإخفاء
  it("NAV02: «قيد يومية» ⟵ شاشة القيد وحدها", () => {
    setTokens(TOKENS);
    stubApi();

    renderApp();
    fireEvent.click(navButton("قيد يومية"));

    expect(screen.getByRole("heading", { name: "قيد يومية" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "شجرة الحسابات", hidden: true })).toBeNull();
  });

  it("NAV03: الرجوع إلى شجرة الحسابات", () => {
    setTokens(TOKENS);
    stubApi();

    renderApp();
    fireEvent.click(navButton("قيد يومية"));
    fireEvent.click(navButton("شجرة الحسابات"));

    expect(screen.getByRole("heading", { name: "شجرة الحسابات" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "قيد يومية", hidden: true })).toBeNull();
  });

  it("NAV04: زرّ واحد يحمل aria-current، وهو زرّ الوجهة المعروضة", () => {
    setTokens(TOKENS);
    stubApi();

    renderApp();
    fireEvent.click(navButton("قيد يومية"));

    const current = screen.getAllByRole("button").filter((button) => button.getAttribute("aria-current") === "page");

    expect(current).toEqual([navButton("قيد يومية")]);
  });

  // ‏قرار 2026-09-12: لا موجّه ولا URL. والـURL مصدر حقيقة ثانٍ للحالة، فتسلّله
  // يُقاس هنا لا يُفترض غيابه
  it("NAV05: التنقّل لا يمسّ الـURL ولا history", () => {
    setTokens(TOKENS);
    stubApi();

    const href = window.location.href;
    const length = window.history.length;

    renderApp();
    fireEvent.click(navButton("قيد يومية"));

    expect(screen.getByRole("heading", { name: "قيد يومية" })).toBeInTheDocument();
    expect(window.location.href).toBe(href);
    expect(window.history.length).toBe(length);
  });

  // ‏الوجهة الثالثة (2026-09-28). و`NAV01`–`NAV06` بلا تعديل: لا شيء فيها يفترض عدد الأزرار
  it("NAV07: «ميزان المراجعة» ⟵ شاشة الميزان وحدها", () => {
    setTokens(TOKENS);
    stubApi();

    renderApp();
    fireEvent.click(navButton("ميزان المراجعة"));

    expect(screen.getByRole("heading", { name: "ميزان المراجعة" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "شجرة الحسابات", hidden: true })).toBeNull();
    expect(screen.queryByRole("heading", { name: "قيد يومية", hidden: true })).toBeNull();
  });

  it("NAV08: من الميزان رجوعاً إلى شجرة الحسابات", () => {
    setTokens(TOKENS);
    stubApi();

    renderApp();
    fireEvent.click(navButton("ميزان المراجعة"));
    fireEvent.click(navButton("شجرة الحسابات"));

    expect(screen.getByRole("heading", { name: "شجرة الحسابات" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "ميزان المراجعة", hidden: true })).toBeNull();
  });

  it("NAV09: زرّ واحد من ثلاثة يحمل aria-current، وهو زرّ الميزان", () => {
    setTokens(TOKENS);
    stubApi();

    renderApp();
    fireEvent.click(navButton("ميزان المراجعة"));

    const nav = screen.getByRole("navigation", { name: "التنقّل" });

    // ‏الشرط الموجب: الأزرار ثلاثة فعلاً — فلا يمرّ «واحد يحمله» على شريط من زرّ واحد
    expect(nav.querySelectorAll("button")).toHaveLength(3);
    expect(Array.from(nav.querySelectorAll("button")).filter((button) => button.getAttribute("aria-current") === "page"))
      .toEqual([navButton("ميزان المراجعة")]);
  });

  it("NAV06: بلا جلسة ⟵ لا شريط تنقّل", () => {
    stubApi();

    renderApp();

    expect(screen.getByRole("button", { name: "تسجيل الدخول" })).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "التنقّل" })).toBeNull();
  });
});
