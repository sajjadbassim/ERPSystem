import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { BranchPicker } from "./BranchPicker";
import type { BranchItem } from "../../api/useAllBranches";
import { AuthProvider } from "../../auth/AuthProvider";
import { clearTokens, setTokens } from "../../auth/token-store";

// ‏مصفوفة الحالات B (منتقي الفرع).
//
// ‏**البادئة `B` لا توسيعٌ لـ`S`:** الثابت المقيس في خمسة عشر ملفاً هو «بادئة ↔ ملف»
// بلا استثناء واحد. والسابقة الحاسمة `G` (الشبكة العامة، «أول مصدر: شجرة الحسابات»)
// مقابل `A` (مستهلكها الفعليّ) — **المستهلك يأخذ بادئة جديدة ولا يمتدّ في بادئة
// المكوّن العام**. ونظيرها `F` (النموذج العام) مقابل `Z`.
//
// ‏**‏`B11` محجوزة عمداً ولا تُكتب هنا:** هي نظير `S10` — سقف `MAX_PAGES` في
// ‏`useAllBranches`، فرعٌ بلا حارس يُسجَّل ولا يُبنى. **والترقيم لا يُعاد**، على عرف
// فجوة `V10` نفسه: رقمٌ سُمِّي أماماً يُترك فجوةً كي لا تشير إحالته إلى غير مقصودها
// بصمت. فالمصفوفة أربع عشرة حالة في خمس عشرة خانة.
//
// ‏**ولا هوك صفحةٍ واحدة لـ`Branch`:** نظير `useAccounts` وُجد لمصفوفة `Q` قبل
// ‏`useAllAccounts`، وبقي **بلا مستهلك** حتى صار الدَّين ١٠ (`L11`). فبناء نظيرٍ له
// هنا يُعيد إنتاج الدَّين نفسه بالاسم.
//
// ‏🔒 **قاعدة حاكمة في هذه المصفوفة: لا تأكيد سالب بلا شرط موجب يسبقه.**
// كل حالة تنفي شيئاً (`B06`, `B10`, `B13`, `B14`, `B15`) تؤكّد أولاً أن ما يجب أن
// يظهر قد ظهر فعلاً. بغير ذلك تمرّ **بالخواء لا بالحراسة** — وهو ما وقع في نصف
// ‏`K28` السالب وفي `L18` و`N06`، ولم يُكشف إلا بعطل متعمَّد لاحق.

const COMPANY_ID = "0199a1f0-0000-7000-8000-000000000001";

// ‏خمسة حقول لا أحد عشر: `BranchResponseDto` بلا `accountType` ولا `normalBalance`
// ولا `isPostable` ولا `parentAccountId` — مقيس من `openapi.json`
const BRANCHES: BranchItem[] = [
  { id: "0199a1f0-0000-7000-8000-0000000000b1", companyId: COMPANY_ID, code: "BR-01", name: "الفرع الرئيسي", isActive: true },
  { id: "0199a1f0-0000-7000-8000-0000000000b2", companyId: COMPANY_ID, code: "BR-02", name: "فرع الكرادة", isActive: true },
  { id: "0199a1f0-0000-7000-8000-0000000000b3", companyId: COMPANY_ID, code: "BR-03", name: "فرع البصرة", isActive: true }
];

// ‏اسمه يحمل كلمة **لا تطابق** أياً من الثلاثة النشطة، فـ`B15` تقيس خروجه من
// المجموعة لا تشابه نصوص
const INACTIVE: BranchItem = {
  id: "0199a1f0-0000-7000-8000-0000000000b9",
  companyId: COMPANY_ID,
  code: "BR-09",
  name: "فرع مغلق",
  isActive: false
};

const LABELS = BRANCHES.map((branch) => `${branch.code} — ${branch.name}`);

// ‏نفس ثابتَي `A05` حرفياً، ولسبب مقيس: `traceIdToShow` يُرجع `null` إن كانت الرسالة
// **تحوي** الرقم أصلاً (`api-error.ts:93`). فرسالة لا تحويه شرطٌ لظهوره
const TRACE = "0HNOF2DK5ILLO:00000001";
const SERVER_MESSAGE = "الخدمة غير متاحة مؤقتاً.";

function createWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <AuthProvider>{children}</AuthProvider>
      </QueryClientProvider>
    );
  };
}

type Page = { items: BranchItem[]; hasNextPage: boolean };

// ‏يردّ بحسب `PageNumber` في الرابط لا بحسب ترتيب النداء: فيثبت `B08` أن الحلقة
// تطلب الصفحة الثانية فعلاً، وأن اسم المعامل `PageNumber` بحرفه المقيس من العقد
function stubPages(pages: Page[]) {
  const fetchMock = vi.fn(async (input: unknown) => {
    const requested = /PageNumber=(\d+)/u.exec(String(input));
    const page = pages[requested === undefined || requested === null ? 0 : Number(requested[1]) - 1];

    return {
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        data: { data: page?.items ?? [], hasNextPage: page?.hasNextPage ?? false }
      })
    };
  });

  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

function stubPendingFetch() {
  let release: () => void = () => {};

  const fetchMock = vi.fn(
    async () =>
      await new Promise<{ ok: boolean; status: number; json: () => Promise<unknown> }>((resolve) => {
        release = () =>
          resolve({
            ok: true,
            status: 200,
            json: async () => ({ success: true, data: { data: BRANCHES, hasNextPage: false } })
          });
      })
  );

  vi.stubGlobal("fetch", fetchMock);

  return { release: () => release() };
}

// ‏**503 لا 401**: قرار الدَّين ٦ يقصر عرض رقم التتبّع على أعطال الخادم، وحجبه في
// ‏4xx محروس سلفاً في `T02` على طبقة `api-error` — فإعادة قياسه هنا تكرار يمنعه
// عرف «`N09` يُعدّد صيغ الفشل لا نقاط النهاية»
function stubServerFault() {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => ({
      ok: false,
      status: 503,
      json: async () => ({ success: false, message: SERVER_MESSAGE, data: null, traceId: TRACE })
    }))
  );
}

function renderPicker(value: string | null, onChange: (branchId: string | null) => void) {
  render(<BranchPicker value={value} onChange={onChange} />, { wrapper: createWrapper() });

  return screen.getByLabelText("الفرع");
}

// ‏`mouseDown` ثم `click`: Autocomplete يفتح على الأول، و`fireEvent.click` لا يُطلقه
function openPopup(input: HTMLElement) {
  fireEvent.mouseDown(input);
  fireEvent.click(input);
}

async function openAndReadOptions(input: HTMLElement): Promise<string[]> {
  openPopup(input);

  const options = await screen.findAllByRole("option");

  return options.map((option) => option.textContent ?? "");
}

// ‏البذر تهيئة لا ادعاء: `AuthProvider` يشتقّ حالته الابتدائية من المخزن، فرمزٌ
// مبذور يجعله «مسجَّلاً» عند التركيب — وبغيره يبقى الاستعلام مبوَّباً فلا يجلب
beforeEach(() => {
  setTokens({
    accessToken: "b-access",
    refreshToken: "b-refresh",
    accessTokenExpiresAt: "2026-09-23T10:00:00.0000000Z"
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("BranchPicker — B (منتقي الفرع، بمعزل عن أي نداء حيّ)", () => {
  it("B01: يعرض الفروع المُحمَّلة برمزها واسمها", async () => {
    stubPages([{ items: BRANCHES, hasNextPage: false }]);

    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);
  });

  it("B02: الكتابة تُصفّي المعروض بلا نداء ثانٍ", async () => {
    const fetchMock = stubPages([{ items: BRANCHES, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    await openAndReadOptions(input);
    const callsAfterLoad = fetchMock.mock.calls.length;

    fireEvent.change(input, { target: { value: "الكرادة" } });

    await waitFor(async () =>
      expect((await screen.findAllByRole("option")).map((o) => o.textContent)).toEqual([LABELS[1]])
    );

    // ‏هذا التأكيد هو ما يفرّق «فلترة محلية» عن «بحث خادم» — بلاه تمرّ الحالتان
    expect(fetchMock.mock.calls.length).toBe(callsAfterLoad);
  });

  it("B03: مسح نصّ البحث يعيد القائمة كاملة", async () => {
    stubPages([{ items: BRANCHES, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    await openAndReadOptions(input);

    fireEvent.change(input, { target: { value: "الكرادة" } });
    fireEvent.change(input, { target: { value: "" } });

    // ‏يثبت أن الفلترة **عرضٌ لا إتلاف**: تنفيذ يقلّص المصدر نفسه يمرّ من `B02` وحدها
    await waitFor(async () =>
      expect((await screen.findAllByRole("option")).map((o) => o.textContent)).toEqual(LABELS)
    );
  });

  it("B04: اختيار فرع يُصدر معرّفه نصّاً لا الكائن", async () => {
    stubPages([{ items: BRANCHES, hasNextPage: false }]);
    const onChange = vi.fn();
    const input = renderPicker(null, onChange);

    await openAndReadOptions(input);
    fireEvent.click(await screen.findByRole("option", { name: LABELS[1]! }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange.mock.calls[0]?.[0]).toBe(BRANCHES[1]!.id);
  });

  it("B05: onChange بوسيط واحد لا أكثر", async () => {
    stubPages([{ items: BRANCHES, hasNextPage: false }]);
    const onChange = vi.fn();
    const input = renderPicker(null, onChange);

    await openAndReadOptions(input);
    fireEvent.click(await screen.findByRole("option", { name: LABELS[1]! }));

    // ‏الخطر **افتراضيّ لا عَرَضيّ**: توقيع `onChange` في MUI رباعيّ، فتمريره مباشرةً
    // يسرّب حدث DOM وداخليات المكتبة إلى عقد المستهلك. درس `S05` معمَّماً
    expect(onChange.mock.calls[0]).toHaveLength(1);
  });

  it("B06: فلترة بلا مطابقة ⟵ «لا نتائج مطابقة»", async () => {
    stubPages([{ items: BRANCHES, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    // ‏الشرط الموجب أولاً: بغيره تمرّ الحالة **بالخواء** — قائمة فارغة تُظهر النصّ
    // نفسه، فيخضرّ التأكيد وهو لا يحرس شيئاً
    expect(await openAndReadOptions(input)).toEqual(LABELS);

    fireEvent.change(input, { target: { value: "لا يوجد فرع بهذا الاسم" } });

    await waitFor(() => expect(screen.queryByText("لا نتائج مطابقة.")).not.toBeNull());
  });

  it("B07: حالة تحميل صريحة أثناء الجلب الأول", async () => {
    const { release } = stubPendingFetch();
    const input = renderPicker(null, vi.fn());

    openPopup(input);

    // ‏بلا إعلان صريح يبدو المنتقي **فارغاً** فيستنتج المستخدم «لا فروع» — نمط
    // الفشل نفسه الذي وُجدت `Q06` له
    await waitFor(() => expect(screen.queryByText("جارٍ تحميل الفروع…")).not.toBeNull());

    release();

    await waitFor(() => expect(screen.queryByText("جارٍ تحميل الفروع…")).toBeNull());
  });

  it("B08: التحميل يجمع كل الصفحات لا الأولى وحدها", async () => {
    const fetchMock = stubPages([
      { items: [BRANCHES[0]!, BRANCHES[1]!], hasNextPage: true },
      { items: [BRANCHES[2]!], hasNextPage: false }
    ]);

    // ‏`MaxPageSize = 100`. بلا الحلقة يختفي الفرع رقم 101 **بلا أي خطأ**
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);

    expect(fetchMock.mock.calls.map((call) => String(call[0]))).toEqual([
      expect.stringContaining("PageNumber=1"),
      expect.stringContaining("PageNumber=2")
    ]);
  });

  it("B09: تفريغ الاختيار يُصدر null", async () => {
    stubPages([{ items: BRANCHES, hasNextPage: false }]);
    const onChange = vi.fn();

    const input = renderPicker(BRANCHES[1]!.id, onChange);

    // ‏زرّ المسح مخفيّ بـ`visibility` ما لم يكن الحقل مركَّزاً، و`getByRole` يستبعد
    // ما ليس في شجرة الإتاحة. فالتركيز جزء من الرحلة الحقيقية لا حيلة اختبار
    fireEvent.focus(input);

    fireEvent.click(await screen.findByRole("button", { name: "مسح الاختيار" }));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("B10: المنتقي لا يرسل مرشِّح شركة — النطاق يفرضه الخادم", async () => {
    const fetchMock = stubPages([{ items: BRANCHES, hasNextPage: false }]);

    // ‏الشرط الموجب: بلاه يخضرّ النفي على **صفر نداء** — وهو نجاح فارغ لا حراسة
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);
    expect(fetchMock.mock.calls.length).toBeGreaterThan(0);

    // ‏`companyId` حاضر في الاستجابة، والخادم يقيّد بالشركة بنيوياً منذ `00c84a4`
    // (الحارس `K26`). فترشيحٌ ثانٍ من المتصفح يُنشئ **مصدر حقيقة ثانياً** للنطاق —
    // تمنعه R-API-05 و R-CUR-03
    for (const call of fetchMock.mock.calls) {
      expect(String(call[0])).not.toMatch(/companyId/iu);
    }
  });

  // ‏🔒 `B11` محجوزة — نظير `S10` (سقف `MAX_PAGES`). لا تُكتب ولا يُعاد استعمال رقمها

  it("B12: فشل الجلب (5xx) ⟵ رسالة الخادم ورقمه، لا قائمة فارغة", async () => {
    stubServerFault();

    renderPicker(null, vi.fn());

    // ‏نظير `A05` بادعاءيه: الرسالة من الخادم لا من تأليفنا، والرقم يربط بلاغ
    // المستخدم بسجلات الخادم (بند 6.1 في قواعد الباك-إند)
    expect(await screen.findByText(SERVER_MESSAGE)).not.toBeNull();
    expect(screen.getByText(new RegExp(TRACE, "u"))).not.toBeNull();
  });

  it("B13: حالة الفشل تُميَّز عن «لا نتائج مطابقة» الفارغة", async () => {
    stubServerFault();

    const input = renderPicker(null, vi.fn());

    await screen.findByRole("alert");

    openPopup(input);

    // ‏`Autocomplete` يُطلق `noOptionsText` من تلقائه على `options: []` — وهي بعينها
    // حالة الفشل. فثلاثة أخطار تعبر `B12` صامتة: رسالتان متناقضتان معاً، أو ابتلاع
    // الخطأ بـ«لا نتائج»، أو اختفاؤه عند أول حرف يكتبه المستخدم
    await waitFor(() => expect(screen.queryByText("لا نتائج مطابقة.")).toBeNull());

    fireEvent.change(input, { target: { value: "ا" } });

    // ‏والخطر الثالث: الخطأ لا يتبخّر بالكتابة
    await waitFor(() => expect(screen.queryByRole("alert")).not.toBeNull());
  });

  it("B14: فرع معطَّل لا يظهر في الخيارات قبل أي فلترة نصّية", async () => {
    stubPages([{ items: [...BRANCHES, INACTIVE], hasNextPage: false }]);

    // ‏المساواة لا الاحتواء: تؤكّد حضور الثلاثة النشطة **وغياب المعطَّل** معاً، فلا
    // تمرّ بالخواء. والترشيح في **طبقة العرض لا طبقة الجلب** — `useAllBranches` يبقى
    // مصدراً أميناً تحتاجه شاشة إدارة الفروع المستقبلية لترى المعطَّل بالضبط
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);
  });

  it("B15: الفرع المعطَّل خارج المجموعة لا مخفيّ فوقها — الكتابة باسمه ⟵ «لا نتائج مطابقة»", async () => {
    stubPages([{ items: [...BRANCHES, INACTIVE], hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    expect(await openAndReadOptions(input)).toEqual(LABELS);

    // ‏**صحة تنفيذ لا تسمية:** `B14` وحدها تخضرّ لو كان الاستبعاد بصريّاً — خيارٌ
    // مُصيَّر ومخفيّ بـ`visibility` يخرج من شجرة الإتاحة فلا يراه `getByRole`،
    // **والخيار يبقى مبلوغاً بالكتابة**. فالكتابة باسمه هي ما يفرّق «خارج المجموعة»
    // عن «مخفيّ فوقها»
    fireEvent.change(input, { target: { value: "مغلق" } });

    await waitFor(() => expect(screen.queryByText("لا نتائج مطابقة.")).not.toBeNull());
  });
});
