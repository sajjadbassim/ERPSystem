import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

import { AppRoot } from "../AppRoot";
import { clearTokens, setTokens } from "../auth/token-store";
import { AccountsScreen } from "./AccountsScreen";

// ‏مصفوفة `A` — شاشة شجرة الحسابات: **التكامل وحده**.
//
// ‏ولا تُعاد هنا حراسة ما أُثبت في مواضعه: `G01`–`G06` تحرس `<DataGrid>` على بيانات
// مُمرَّرة، و`Q01`–`Q06` تحرس فكّ الغلاف، و`T01`–`T07` تحرس رسالة الخادم ورقم تتبّعها.
// **والجديد هنا هو الوصل بينها** — وما لا وجود له قبل هذه الشاشة (جدول الترجمة).

const TOKENS = {
  accessToken: "access-token-1",
  refreshToken: "refresh-token-1",
  accessTokenExpiresAt: "2026-09-10T10:00:00.0000000Z"
};

const TRACE = "0HNOF2DK5ILLO:00000001";
const SERVER_MESSAGE = "الخدمة غير متاحة مؤقتاً.";

// ‏الأربعة تعكس تنوّع الأعمدة كما بُذرت حيّاً: تجميعي، وتشغيلي، وطبيعة دائنة،
// ومعطَّل. فلو عرض عمودٌ قيمة واحدة لكل الصفوف لَسقطت حالة منها
const ACCOUNTS = [
  { id: "a1", companyId: "c1", code: "1200", name: "الأصول المتداولة", accountType: 1, normalBalance: 0, isPostable: false, isActive: true },
  { id: "a2", companyId: "c1", code: "1100", name: "النقدية بالصندوق", accountType: 1, normalBalance: 0, isPostable: true, isActive: true },
  { id: "a3", companyId: "c1", code: "4100", name: "إيرادات المبيعات", accountType: 4, normalBalance: 1, isPostable: true, isActive: true },
  { id: "a4", companyId: "c1", code: "5100", name: "مصروف الإيجار", accountType: 5, normalBalance: 0, isPostable: true, isActive: false }
];

function accountsPage(items: unknown[]) {
  return {
    ok: true,
    status: 200,
    json: async () => ({
      success: true,
      message: null,
      traceId: null,
      data: { data: items, totalCount: items.length, pageNumber: 1, pageSize: 100, totalPages: 1, hasNextPage: false }
    })
  };
}

function deferred() {
  let release: () => void = () => {};
  const promise = new Promise<void>((resolve) => {
    release = resolve;
  });

  return { promise, release: () => release() };
}

function renderScreen() {
  return render(
    <AppRoot>
      <AccountsScreen />
    </AppRoot>);
}

// ‏نصوص الصفوف كما يقرؤها المستخدم — بلا رأس الجدول
function bodyRowTexts() {
  return Array.from(document.querySelectorAll("tbody tr")).map((row) =>
    Array.from(row.querySelectorAll("td")).map((cell) => cell.textContent ?? ""));
}

beforeEach(() => {
  clearTokens();
  setTokens(TOKENS);
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("A — شاشة شجرة الحسابات", () => {
  it("A01: حسابات المصدر تصل الشبكة صفاً صفاً بترتيبها", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => accountsPage(ACCOUNTS)));

    renderScreen();

    await waitFor(() => expect(bodyRowTexts()).toHaveLength(ACCOUNTS.length));

    // ‏الوصل هو الادعاء: `useAllAccounts` ⟵ `<DataGrid>`. و`G01` تحرس الشبكة على
    // بيانات مُمرَّرة، ولا تقول شيئاً عن أن **هذه الشاشة** تمرّر ما جلبته
    expect(bodyRowTexts().map((cells) => cells[0])).toEqual(["1200", "1100", "4100", "5100"]);

    expect(bodyRowTexts().map((cells) => cells[1])).toEqual(
      ACCOUNTS.map((account) => account.name));
  });

  it("A02: النوع والطبيعة **بالعربية لا بالرقم**", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => accountsPage(ACCOUNTS)));

    renderScreen();

    await waitFor(() => expect(bodyRowTexts()).toHaveLength(ACCOUNTS.length));

    const rows = bodyRowTexts();

    // ‏`4100` إيرادات دائنة، و`1100` أصول مدينة — فلو عرض العمودان الرقم الخام
    // ‏أو قيمة واحدة مكرَّرة لسقط الادعاء
    const revenue = rows.find((cells) => cells[0] === "4100") ?? [];
    const cash = rows.find((cells) => cells[0] === "1100") ?? [];

    expect(revenue[2]).toBe("إيرادات");
    expect(revenue[3]).toBe("دائن");

    expect(cash[2]).toBe("أصول");
    expect(cash[3]).toBe("مدين");

    // ‏ولا رقم خام في أي خلية من العمودين
    expect(rows.map((cells) => cells[2]).join("")).not.toMatch(/\d/u);
    expect(rows.map((cells) => cells[3]).join("")).not.toMatch(/\d/u);
  });

  it("A03: التجميعي يظهر **مميَّزاً بصرياً** عن التشغيلي", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => accountsPage(ACCOUNTS)));

    renderScreen();

    await waitFor(() => expect(bodyRowTexts()).toHaveLength(ACCOUNTS.length));

    const rows = bodyRowTexts();
    const aggregate = rows.find((cells) => cells[0] === "1200") ?? [];
    const postable = rows.find((cells) => cells[0] === "1100") ?? [];

    // ‏الادعاء أن الفرق **مرئيّ** لا مخزَّن: نصّان مختلفان لا خلية فارغة مقابل فارغة
    expect(aggregate[4]).not.toBe(postable[4]);

    expect(aggregate[4]).not.toBe("");
    expect(postable[4]).not.toBe("");

    // ‏ومثله عمود «نشط»: `5100` معطَّل والبقية نشطة
    const inactive = rows.find((cells) => cells[0] === "5100") ?? [];

    expect(inactive[5]).not.toBe(postable[5]);
  });

  it("A04: أثناء التحميل ⟵ **لا رسالة «لا حسابات»**", async () => {
    const gate = deferred();

    vi.stubGlobal("fetch", vi.fn(async () => {
      await gate.promise;
      return accountsPage(ACCOUNTS);
    }));

    renderScreen();

    // ‏الفراغ الكاذب: شبكة فارغة أثناء التحميل تقول «لا حسابات» وهي لم تسأل بعد —
    // ‏نمط `Q06` و`C01` نفسه. والرسالة تخصّ `<DataGrid>` (`G03`) فلا يُعرض جسمها أصلاً
    expect(screen.queryByText(/لا حسابات|لا توجد/u)).toBeNull();

    gate.release();

    await waitFor(() => expect(bodyRowTexts()).toHaveLength(ACCOUNTS.length));
  });

  it("A05: فشل الجلب (5xx) ⟵ رسالة الخادم ورقمه، **لا شبكة فارغة**", async () => {
    // ⚠ **5xx حصراً** — لا 401 ولا أي 4xx. وهو ما يوافق قرار الدَّين ٦: الرقم
    // يُعرض في أعطال الخادم وحدها. وحجبه في 4xx مُثبَت في `T02` فلا يُقاس هنا ثانيةً
    vi.stubGlobal("fetch", vi.fn(async () => ({
      ok: false,
      status: 503,
      json: async () => ({ success: false, message: SERVER_MESSAGE, data: null, traceId: TRACE })
    })));

    renderScreen();

    // ‏المهلة أوسع بأثر قرار `QC`: الخمسمئة تُعاد مرة واحدة فيستقرّ الخطأ بعد تأخير
    expect(await screen.findByText(SERVER_MESSAGE, {}, { timeout: 5000 })).not.toBeNull();

    expect(screen.getByText(new RegExp(TRACE, "u"))).not.toBeNull();

    // ‏والادعاء الثاني: الخطأ **لا يُبتلع** في «لا حسابات» — وهو الفشل المفتوح
    // الذي يجعل انقطاع الخادم يبدو شجرة حسابات فارغة
    expect(screen.queryByText(/لا حسابات|لا توجد/u)).toBeNull();

    expect(bodyRowTexts()).toHaveLength(0);
  });
});
