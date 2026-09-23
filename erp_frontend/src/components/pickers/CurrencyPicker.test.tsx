import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { CurrencyPicker } from "./CurrencyPicker";
import { AuthProvider } from "../../auth/AuthProvider";
import { clearTokens, setTokens } from "../../auth/token-store";
import { CurrencyRegistryProvider } from "../../currency/currency-registry";
import type { RegisteredCurrency } from "../../currency/currency-registry";
import { CurrencySourceProvider } from "../../currency/currency-source";

// ‏مصفوفة الحالات CUR (منتقي العملة).
//
// ‏**البادئة `CUR` لا `C`:** الثابت المقيس في ستة عشر ملفاً هو «بادئة ↔ ملف» بلا
// استثناء واحد، و`C` محجوزة فعلاً لـ`currency-source.test.tsx`. **وللمشروع حلّ
// مُجرَّب لهذا التصادم بعينه:** حين تعذّرت `L` على `LoginScreen` (محجوزة لـ`auth`)
// أخذت `LGN`، وحين تعذّرت `Q` على `query-config` أخذت `QC`.
//
// ‏**ولا توسيع لـ`C07`+:** السابقة الحاكمة `G`(الشبكة العامة) مقابل `A`(مستهلكها)،
// ونظيرها `F`(النموذج) مقابل `Z` — **المستهلك يأخذ بادئة جديدة ولا يمتدّ في بادئة
// المكوّن العام**. و`<CurrencyPicker>` مستهلك لسجل `C` لا امتداد له.
//
// ‏🔒 **‏`CUR10` محجوزة عمداً ولا تُكتب هنا:** سقف `MAX_PAGES` في مصدر العملات
// ‏(`currency-source.tsx:20`) — فرعٌ بلا حارس يُسجَّل ولا يُبنى، نظير `S10` و`B11`.
// ‏**والترقيم لا يُعاد**، على عرف فجوة `V10` نفسه. فالمصفوفة **خمس عشرة حالة في
// ست عشرة خانة**.
//
// ‏**ولا نظير لـ`B10` هنا — يسقط بسببين مستقلّين كلٌّ منهما كافٍ:** `CurrencyResponseDto`
// ‏**بلا `companyId`** أصلاً، و`GET /api/currencies` لا يقبل إلا `PageNumber`/`PageSize`
// ‏— كلاهما مقيس من `openapi.json`. فليس ثمّة مرشِّح يمكن إرساله بالخطأ، والحالة كانت
// ستمرّ **بالخواء لا بالحراسة**. والعملة **بيانات نظام لا بيانات شركة**.
//
// ‏🔒 **قاعدة حاكمة: لا تأكيد سالب بلا شرط موجب يسبقه.** كل حالة تنفي شيئاً
// ‏(`CUR06`, `CUR12`, `CUR13`, `CUR14`, `CUR16`) تؤكّد أولاً أن ما يجب أن يظهر قد ظهر.

// ‏**المسار المعتمَد (أ): توسيع السجل القائم، لا هوك `useAllCurrencies` موازٍ.**
// الحجة مقيسة: المفتاح `["currencies","all"]` **محجوز فعلاً** في `currency-source.tsx:66`.
// والسجل يمحو الخطأ عند حدوده بقرار موثَّق شرطُه المكتوب «ولا شاشة كهذه اليوم» —
// ‏**وقد سقط الشرط بظهور هذا المنتقي**، فرفعه تنفيذٌ للمكتوب لا نقضٌ له.

const IQD: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-0000000000c1",
  code: "IQD",
  name: "دينار عراقي",
  symbol: "د.ع",
  decimalPlaces: 0,
  isActive: true
};

const USD: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-0000000000c2",
  code: "USD",
  name: "دولار أمريكي",
  symbol: "$",
  decimalPlaces: 2,
  isActive: true
};

const SAR: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-0000000000c3",
  code: "SAR",
  name: "ريال سعودي",
  symbol: "ر.س",
  decimalPlaces: 2,
  isActive: true
};

const CURRENCIES: RegisteredCurrency[] = [IQD, USD, SAR];

// ‏`symbol: null` — **الحقل الاختياري الوحيد** في `CurrencyResponseDto` (مقيس من
// ‏`openapi.json`: خارج `required`، ونوعه `["null","string"]`). ويُبقى **خارج**
// ‏`CURRENCIES` عمداً: لو دخلها لأثبتته `CUR01` ضمناً وصارت `CUR15` تكراراً لا حارساً
const EUR: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-0000000000c4",
  code: "EUR",
  name: "يورو",
  symbol: null,
  decimalPlaces: 2,
  isActive: true
};

// ‏اسمه يحمل كلمة **لا تطابق** أياً من الثلاث النشطة، فـ`CUR14` تقيس خروجه من
// المجموعة لا تشابه نصوص
const INACTIVE: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-0000000000c9",
  code: "SYP",
  name: "ليرة سورية",
  symbol: "ل.س",
  decimalPlaces: 2,
  isActive: false
};

// ‏`symbol ?? code` — النمط نفسه المفروض في `<MoneyDisplay>:61` بحكم R-RPT-04:
// الرمز من السجل لا ثابتاً في الكود، ويسقط إلى الكود عند غيابه ولا يسقط إلى لا شيء
function label(currency: RegisteredCurrency): string {
  return `${currency.symbol ?? currency.code} — ${currency.name}`;
}

const LABELS = CURRENCIES.map(label);

// ‏نفس ثابتَي `A05` و`B12` حرفياً، ولسبب مقيس: `traceIdToShow` يُرجع `null` إن كانت
// الرسالة **تحوي** الرقم أصلاً (`api-error.ts:93`). فرسالة لا تحويه شرطٌ لظهوره
const TRACE = "0HNOF2DK5ILLO:00000002";
const SERVER_MESSAGE = "الخدمة غير متاحة مؤقتاً.";

// ‏⚠ **وعاء `CUR` ليس وعاء `B`:** مصفوفة `B` تكتفي بـ`QueryClientProvider` +
// ‏`AuthProvider` لأن `useAllBranches` هوك مستقلّ. وهذا المنتقي يقرأ **السياق**،
// فيلزمه `<CurrencySourceProvider>` فوقهما كما في `currency-source.test.tsx:55-65`
function createSourceWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <AuthProvider>
          <CurrencySourceProvider>{children}</CurrencySourceProvider>
        </AuthProvider>
      </QueryClientProvider>
    );
  };
}

// ‏وعاء `CUR16` وحدها: سجل **صريح بلا مصدر**. وهو المُميِّز الذي يكشف المسارين
// ‏(ب) و(ج) معاً — تفصيله عند الحالة نفسها
function createRegistryWrapper(currencies: readonly RegisteredCurrency[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <AuthProvider>
          <CurrencyRegistryProvider currencies={currencies}>{children}</CurrencyRegistryProvider>
        </AuthProvider>
      </QueryClientProvider>
    );
  };
}

type Page = { items: RegisteredCurrency[]; hasNextPage: boolean };

// ‏يردّ بحسب `PageNumber` في الرابط لا بحسب ترتيب النداء: فيثبت `CUR08` أن الحلقة
// تطلب الصفحة الثانية فعلاً، وأن اسم المعامل `PageNumber` بحرفه المقيس من العقد
function stubPages(pages: Page[]) {
  const fetchMock = vi.fn(async (input: unknown) => {
    const requested = /PageNumber=(\d+)/u.exec(String(input));
    const page = pages[requested === null ? 0 : Number(requested[1]) - 1];

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
            json: async () => ({ success: true, data: { data: CURRENCIES, hasNextPage: false } })
          });
      })
  );

  vi.stubGlobal("fetch", fetchMock);

  return { release: () => release() };
}

// ‏**503 لا 401**: قرار الدَّين ٦ يقصر عرض رقم التتبّع على أعطال الخادم، وحجبه في
// ‏4xx محروس سلفاً في `T02` على طبقة `api-error`
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

function renderPicker(value: string | null, onChange: (currencyId: string | null) => void) {
  render(<CurrencyPicker value={value} onChange={onChange} />, { wrapper: createSourceWrapper() });

  return screen.getByLabelText("العملة");
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
    accessToken: "cur-access",
    refreshToken: "cur-refresh",
    accessTokenExpiresAt: "2026-09-23T14:00:00.0000000Z"
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("CurrencyPicker — CUR (منتقي العملة، بمعزل عن أي نداء حيّ)", () => {
  it("CUR01: يعرض العملات المُحمَّلة برمزها واسمها", async () => {
    stubPages([{ items: CURRENCIES, hasNextPage: false }]);

    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);
  });

  it("CUR02: الكتابة تُصفّي المعروض بلا نداء ثانٍ", async () => {
    const fetchMock = stubPages([{ items: CURRENCIES, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    await openAndReadOptions(input);
    const callsAfterLoad = fetchMock.mock.calls.length;

    fireEvent.change(input, { target: { value: "دولار" } });

    await waitFor(async () =>
      expect((await screen.findAllByRole("option")).map((o) => o.textContent)).toEqual([LABELS[1]])
    );

    // ‏هذا التأكيد هو ما يفرّق «فلترة محلية» عن «بحث خادم» — بلاه تمرّ الحالتان
    expect(fetchMock.mock.calls.length).toBe(callsAfterLoad);
  });

  it("CUR03: مسح نصّ البحث يعيد القائمة كاملة", async () => {
    stubPages([{ items: CURRENCIES, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    await openAndReadOptions(input);

    fireEvent.change(input, { target: { value: "دولار" } });
    fireEvent.change(input, { target: { value: "" } });

    // ‏يثبت أن الفلترة **عرضٌ لا إتلاف**: تنفيذ يقلّص المصدر نفسه يمرّ من `CUR02` وحدها.
    // ‏والخطر هنا **أثقل منه في الفرع**: المصدر المُقلَّص سجلٌّ مشترك يقرأه
    // ‏`<MoneyDisplay>` أيضاً، فإتلافه يُنذر R-RPT-06 على مبالغ لا علاقة لها بالمنتقي
    await waitFor(async () =>
      expect((await screen.findAllByRole("option")).map((o) => o.textContent)).toEqual(LABELS)
    );
  });

  it("CUR04: اختيار عملة يُصدر معرّفها نصّاً لا الكائن", async () => {
    stubPages([{ items: CURRENCIES, hasNextPage: false }]);
    const onChange = vi.fn();
    const input = renderPicker(null, onChange);

    await openAndReadOptions(input);
    fireEvent.click(await screen.findByRole("option", { name: LABELS[1]! }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange.mock.calls[0]?.[0]).toBe(USD.id);
  });

  it("CUR05: onChange بوسيط واحد لا أكثر", async () => {
    stubPages([{ items: CURRENCIES, hasNextPage: false }]);
    const onChange = vi.fn();
    const input = renderPicker(null, onChange);

    await openAndReadOptions(input);
    fireEvent.click(await screen.findByRole("option", { name: LABELS[1]! }));

    // ‏الخطر **افتراضيّ لا عَرَضيّ**: توقيع `onChange` في MUI رباعيّ، فتمريره مباشرةً
    // يسرّب حدث DOM وداخليات المكتبة إلى عقد المستهلك. درس `S05` معمَّماً
    expect(onChange.mock.calls[0]).toHaveLength(1);
  });

  it("CUR06: فلترة بلا مطابقة ⟵ «لا نتائج مطابقة»", async () => {
    stubPages([{ items: CURRENCIES, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    // ‏الشرط الموجب أولاً: بغيره تمرّ الحالة **بالخواء** — قائمة فارغة تُظهر النصّ
    // نفسه، فيخضرّ التأكيد وهو لا يحرس شيئاً
    expect(await openAndReadOptions(input)).toEqual(LABELS);

    fireEvent.change(input, { target: { value: "لا توجد عملة بهذا الاسم" } });

    await waitFor(() => expect(screen.queryByText("لا نتائج مطابقة.")).not.toBeNull());
  });

  it("CUR07: حالة تحميل صريحة أثناء الجلب الأول", async () => {
    const { release } = stubPendingFetch();
    const input = renderPicker(null, vi.fn());

    openPopup(input);

    // ‏بلا إعلان صريح يبدو المنتقي **فارغاً** فيستنتج المستخدم «لا عملات» — نمط
    // الفشل نفسه الذي وُجدت `Q06` له، وهو الحالة الثالثة التي بُني السجل عليها (`C01`)
    await waitFor(() => expect(screen.queryByText("جارٍ تحميل العملات…")).not.toBeNull());

    release();

    await waitFor(() => expect(screen.queryByText("جارٍ تحميل العملات…")).toBeNull());
  });

  it("CUR08: التحميل يجمع كل الصفحات لا الأولى وحدها", async () => {
    const fetchMock = stubPages([
      { items: [IQD, USD], hasNextPage: true },
      { items: [SAR], hasNextPage: false }
    ]);

    // ‏⚠ **والتوقف عند الصفحة الأولى هنا أخطر منه في الفرع** (`currency-source.tsx:27`):
    // عملة غائبة عن السجل لا تظهر «ناقصة» بل تُعرض **إنذار R-RPT-06 على كل مبلغ بها**
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);

    expect(fetchMock.mock.calls.map((call) => String(call[0]))).toEqual([
      expect.stringContaining("PageNumber=1"),
      expect.stringContaining("PageNumber=2")
    ]);
  });

  it("CUR09: تفريغ الاختيار يُصدر null", async () => {
    stubPages([{ items: CURRENCIES, hasNextPage: false }]);
    const onChange = vi.fn();

    const input = renderPicker(USD.id, onChange);

    // ‏زرّ المسح مخفيّ بـ`visibility` ما لم يكن الحقل مركَّزاً، و`getByRole` يستبعد
    // ما ليس في شجرة الإتاحة. فالتركيز جزء من الرحلة الحقيقية لا حيلة اختبار
    fireEvent.focus(input);

    fireEvent.click(await screen.findByRole("button", { name: "مسح الاختيار" }));

    expect(onChange).toHaveBeenCalledWith(null);
  });

  // ‏🔒 `CUR10` محجوزة — نظير `S10` و`B11` (سقف `MAX_PAGES`). لا تُكتب ولا يُعاد رقمها.
  //
  // ‏**ولا نظير لـ`B10` في هذه المصفوفة** — السبب في رأس الملف: لا `companyId` في
  // العقد ولا مرشِّح في نقطة النهاية، فالحالة كانت ستمرّ بالخواء لا بالحراسة.

  it("CUR11: فشل الجلب (5xx) ⟵ رسالة الخادم ورقمه، لا قائمة فارغة", async () => {
    stubServerFault();

    renderPicker(null, vi.fn());

    // ‏نظير `A05` و`B12` بادعاءيهما: الرسالة من الخادم لا من تأليفنا، والرقم يربط
    // بلاغ المستخدم بسجلات الخادم (بند 6.1 في قواعد الباك-إند).
    //
    // ‏**وهذه الحالة هي ما يُسقط شرط «ولا شاشة كهذه اليوم»:** السجل يطوي الخطأ في
    // ‏«قيد التحميل» عمداً (`currency-source.tsx:82`)، فبلا توسيعه بـ`error` يستحيل
    // إخضارها — لا لأن المنتقي ناقص بل لأن المصدر لا يبلّغ
    expect(await screen.findByText(SERVER_MESSAGE)).not.toBeNull();
    expect(screen.getByText(new RegExp(TRACE, "u"))).not.toBeNull();
  });

  it("CUR12: حالة الفشل تُميَّز عن «لا نتائج مطابقة» الفارغة", async () => {
    stubServerFault();

    const input = renderPicker(null, vi.fn());

    await screen.findByRole("alert");

    openPopup(input);

    // ‏`Autocomplete` يُطلق `noOptionsText` من تلقائه على `options: []` — وهي بعينها
    // حالة الفشل. فثلاثة أخطار تعبر `CUR11` صامتة: رسالتان متناقضتان معاً، أو ابتلاع
    // الخطأ بـ«لا نتائج»، أو اختفاؤه عند أول حرف يكتبه المستخدم
    await waitFor(() => expect(screen.queryByText("لا نتائج مطابقة.")).toBeNull());

    // ‏**والخطر الرابع — كشفه العطل `CUR-8` لا التصميم:** إسكات «لا نتائج مطابقة»
    // وحده لا يكفي. فلو فُحص الجهوز قبل الفشل (`loading={!isReady}`) لعرض MUI نصّ
    // التحميل مكانه — والسجل يطوي الفشل في «قيد التحميل» بقصد (`C05`)، فيصير العطل
    // ‏**تحميلاً أبدياً** على شبكة معطوبة. والإنذار حاضر معه، فلا `CUR11` تمسكه ولا
    // التأكيد السابق.
    //
    // ‏وهذا **استكمال حارس يدّعي أكثر مما يقيس**، لا حالة جديدة: «تُميَّز» في اسم
    // الحالة تشمل ألّا تُعرض كحالة أخرى. سابقة `V12` بنصّها — ثغرة في نطاق حارس
    // معتمَد تُسدّ فوراً ولا تُؤجَّل
    expect(screen.queryByText("جارٍ تحميل العملات…")).toBeNull();

    fireEvent.change(input, { target: { value: "د" } });

    // ‏والخطر الثالث: الخطأ لا يتبخّر بالكتابة
    await waitFor(() => expect(screen.queryByRole("alert")).not.toBeNull());
  });

  it("CUR13: عملة معطَّلة لا تظهر في الخيارات قبل أي فلترة نصّية", async () => {
    stubPages([{ items: [...CURRENCIES, INACTIVE], hasNextPage: false }]);

    // ‏المساواة لا الاحتواء: تؤكّد حضور الثلاث النشطة **وغياب المعطَّلة** معاً.
    //
    // ‏⚠ **وموضع الترشيح هنا مُلزِم بنيوياً لا تحقيقَ تجربة كما في الفرع:** لو هبط
    // إلى مصدر السجل المشترك لاختفت العملة المعطَّلة عن `useCurrencyLookup`، فأنذر
    // ‏**كل مبلغ تاريخيّ بها** بـR-RPT-06 — وهو ضرر لا نظير له في `<BranchPicker>`.
    // والخادم يمنع الاستعمال الجديد بحكم R-LIFE-06 سواء رشّحنا أم لم نرشّح
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);
  });

  it("CUR14: العملة المعطَّلة خارج المجموعة لا مخفيّة فوقها — الكتابة باسمها ⟵ «لا نتائج مطابقة»", async () => {
    stubPages([{ items: [...CURRENCIES, INACTIVE], hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    expect(await openAndReadOptions(input)).toEqual(LABELS);

    // ‏**صحة تنفيذ لا تسمية:** `CUR13` وحدها تخضرّ لو كان الاستبعاد بصريّاً —
    // ‏`display: none` يُخرج العنصر من شجرة الإتاحة فلا يراه `getByRole`، **والخيار
    // يبقى مبلوغاً بالكتابة**. مقيس في العطل الثالث من جولة `<BranchPicker>`
    fireEvent.change(input, { target: { value: "سورية" } });

    await waitFor(() => expect(screen.queryByText("لا نتائج مطابقة.")).not.toBeNull());
  });

  it("CUR15: عملة بلا symbol ⟵ تُعرض بكودها لا بفراغ", async () => {
    stubPages([{ items: [EUR], hasNextPage: false }]);

    // ‏`symbol` خارج `required` في `CurrencyResponseDto` — **الحقل الاختياري الوحيد**،
    // ولا نظير له في `BranchResponseDto` الذي حقلاه المعروضان كلاهما مطلوبان.
    // والسقوط إلى الكود هو نفسه المفروض في `<MoneyDisplay>:61` بحكم R-RPT-04.
    //
    // ‏والتأكيد على النصّ الكامل لا على الاحتواء: تنفيذ يكتب `${symbol} — ${name}`
    // بلا سقوط يُخرج `" — يورو"` — وهو **يحتوي الاسم** فيمرّ من أي تأكيد جزئي
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(["EUR — يورو"]);
  });

  it("CUR16: المنتقي يقرأ السجل ولا يفتح استعلاماً خاصاً به — صفر نداء تحت <CurrencyRegistryProvider>", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    render(<CurrencyPicker value={null} onChange={vi.fn()} />, {
      wrapper: createRegistryWrapper(CURRENCIES)
    });

    // ‏الشرط الموجب أولاً: الخيارات وصلت **من السياق** — بلاه يخضرّ النفي على مكوّن
    // لم يُصيَّر أصلاً
    expect(await openAndReadOptions(screen.getByLabelText("العملة"))).toEqual(LABELS);

    // ‏**والنفي هو إثبات المسار (أ) نفسه، لا سلوك عرض.** سجلٌّ صريح بلا مصدر: فإن
    // قرأ المنتقي السياق فلا نداء البتة؛ وإن فتح استعلامه الخاص — (ب) بالمفتاح نفسه
    // أو (ج) بمفتاح مستقلّ — **انطلق الجلب في الحالتين** لأن الرمز مبذور والبوابة
    // مفتوحة، فيرسب هذا التأكيد.
    //
    // ‏وصياغة «نداء واحد تحت `<CurrencySourceProvider>`» **لا تصلح مُميِّزاً**: هي
    // تكشف (ج) وحدها، لأن استعلامين بمفتاح واحد يُدمجان في مدخل ذاكرة واحد فيبقى
    // النداء واحداً — فتمرّ (ب) خضراء وهي المسار الخاطئ المقصود بالمنع
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
