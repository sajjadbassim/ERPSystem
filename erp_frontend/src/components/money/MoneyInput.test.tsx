import { afterEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import {
  CurrencyRegistryProvider,
  CurrencyRegistryStateProvider
} from "../../currency/currency-registry";
import type { RegisteredCurrency } from "../../currency/currency-registry";
import { MoneyInput } from "./MoneyInput";
import type { MoneyInputValue } from "./MoneyInput";

// ‏مصفوفة الحالات V — نظير U لـ`<MoneyDisplay>`.
//
// **الفجوات عند `V02` و`V03` و`V08` و`V10` مقصودة ولا تُردم بإعادة ترقيم**، بالقاعدة
// المكتوبة في `erp_backend/docs/STATE.md`: إعادة الترقيم تجعل الإحالة تشير إلى غير مقصودها بصمت.
//   `V02`/`V03` (قفل العملة بعملة الحساب / حرّيتها) — أُسقطا مع منتقي العملة الداخلي:
//         العملة صارت خاصية عرض يقرّرها المستضيف، فلا اختيار داخل المكوّن يُقفل أو يُحرَّر.
//   `V08` (عملة الدفاتر ⟵ سعر 1، الخطأ 50012) — محجوز لنموذج القيد: **قرار** القفل
//         سياق شركة. والمكوّن ينفّذ القفل وحده (`V15`–`V17`) ولا يعرف متى يلزم.
//   `V10` (تطبيع `decimalPlaces` مشتركاً) — أُسقط: بعد `V09` لا استعمال له هنا.

const IQD: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-000000000001",
  code: "IQD",
  name: "دينار عراقي",
  symbol: "د.ع",
  decimalPlaces: 0,
  isActive: true
};

const USD: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-000000000002",
  code: "USD",
  name: "دولار أمريكي",
  symbol: "$",
  decimalPlaces: 2,
  isActive: true
};

const UNKNOWN_ID = "0199a1f0-0000-7000-8000-0000000000ff";

const REGISTRY = [IQD, USD];

const BASE: MoneyInputValue = {
  amountFC: "100.0000",
  side: "debit",
  exchangeRate: "1",
  exchangeRateDate: "2026-09-08"
};

type RenderOptions = {
  value?: Partial<MoneyInputValue>;
  currencyId?: string;
  isRateLocked?: boolean;
  registry?: "ready" | "loading";
};

function renderInput(options: RenderOptions = {}) {
  const onChange = vi.fn<(next: MoneyInputValue) => void>();

  const input = (
    <MoneyInput
      value={{ ...BASE, ...options.value }}
      currencyId={options.currencyId ?? IQD.id}
      isRateLocked={options.isRateLocked ?? false}
      onChange={onChange}
    />
  );

  render(
    options.registry === "loading" ? (
      <CurrencyRegistryStateProvider status="loading" currencies={[]} error={null}>
        {input}
      </CurrencyRegistryStateProvider>
    ) : (
      <CurrencyRegistryProvider currencies={REGISTRY}>{input}</CurrencyRegistryProvider>
    )
  );

  return onChange;
}

function firstEmitted(onChange: ReturnType<typeof renderInput>) {
  return onChange.mock.calls[0]?.[0];
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("MoneyInput — R-UI-02 / R-AMT-02 / R-FX-06", () => {
  it("V01: يُصدر الحقول الأربع بأسمائها ولا شيء غيرها", () => {
    const onChange = renderInput();

    fireEvent.change(screen.getByLabelText("المبلغ"), { target: { value: "250" } });

    // ‏مساواة لا احتواء: إضافة `debitBase` أو عودة `currencyId` تُسقط هذا التأكيد — وهو
    // المقصود. العملة خاصية عرض لا جزء من القيمة: مصدران لحقيقة واحدة ممنوعان (R-CUR-03)
    expect(Object.keys(firstEmitted(onChange) ?? {}).sort()).toEqual([
      "amountFC",
      "exchangeRate",
      "exchangeRateDate",
      "side"
    ]);
    expect(firstEmitted(onChange)?.amountFC).toBe("250");
  });

  it("V04: مبلغ صفر ⟵ خطأ معروض (50014)", () => {
    renderInput({ value: { amountFC: "0" } });

    expect(screen.queryByRole("alert")).not.toBeNull();
  });

  it("V05: مبلغ سالب ⟵ خطأ معروض (50014)", () => {
    renderInput({ value: { amountFC: "-5" } });

    expect(screen.queryByRole("alert")).not.toBeNull();
  });

  it("V06: سعر الصرف يُدخل يدوياً، ولا يُطلق المكوّن أي جلب (R-FX-06)", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const onChange = renderInput();

    const rate = screen.getByLabelText("سعر الصرف");
    expect(rate).toBeEnabled();

    fireEvent.change(rate, { target: { value: "1330" } });

    expect(firstEmitted(onChange)?.exchangeRate).toBe("1330");

    // ‏لا نقطة بحث بالعملتين والتاريخ في العقد أصلاً، واختيار السعر المنطبق قرار
    // مالي تمنعه R-API-01. هذا التأكيد يمنع اشتقاقاً يتسلل لاحقاً
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("V07: سعر صرف صفر أو سالب ⟵ خطأ معروض (50020)", () => {
    renderInput({ value: { exchangeRate: "0" } });

    expect(screen.queryByRole("alert")).not.toBeNull();
  });

  it("V09: المبلغ المُدخَل لا يُقرَّب ولا يُبتر عند الإصدار (R-AMT-07)", () => {
    // الدينار صفر خانات عرضاً، والعمود في القاعدة DECIMAL(19,4) مهما كانت العملة.
    // فتقريب الإدخال يغيّر **قيمة مالية**، لا صورة عرض
    const onChange = renderInput({ currencyId: IQD.id });

    fireEvent.change(screen.getByLabelText("المبلغ"), { target: { value: "10.5" } });

    expect(firstEmitted(onChange)?.amountFC).toBe("10.5");
  });

  // ‏`V12` بعد إزالة المنتقي الداخلي: العملة تتبدّل **من الخارج** لا من داخل المكوّن،
  // والمسار الأرجح لتسلّل الاشتقاق هو نفسه — أثرٌ يُطلَق عند تغيّر العملة.
  //
  // والتأكيد على الوسم الجديد **قبل** تأكيد الجلب مقصود: بدونه يصير «لم يُستدعَ `fetch`»
  // صادقاً بالفراغ لو لم يصل التبديل أصلاً — حارسٌ يمرّ لأن شيئاً لم يحدث.
  it("V12: تبديل العملة لا يُطلق أي جلب لاشتقاق السعر", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const onChange = vi.fn<(next: MoneyInputValue) => void>();
    const view = (currencyId: string) => (
      <CurrencyRegistryProvider currencies={REGISTRY}>
        <MoneyInput value={BASE} currencyId={currencyId} isRateLocked={false} onChange={onChange} />
      </CurrencyRegistryProvider>
    );

    const { rerender } = render(view(IQD.id));
    rerender(view(USD.id));

    expect(screen.getByLabelText("المبلغ")).toHaveAccessibleDescription("$");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("V11: العملة مقروءة بجوار المبلغ لا قيمةً مخزَّنة فقط", () => {
    renderInput({ currencyId: IQD.id });

    // ‏مربوطة بالحقل بـ`aria-describedby` لا مجرد نصّ في الصفحة. وقابل للرسوب فعلاً:
    // الوسم بالاسم («دينار عراقي») بدل الرمز يُسقطه
    expect(screen.getByLabelText("المبلغ")).toHaveAccessibleDescription("د.ع");
  });

  it("V13: اختيار الجانب يُصدَر في القيمة", () => {
    const onChange = renderInput({ value: { side: "debit" } });

    fireEvent.change(screen.getByLabelText("الجانب"), { target: { value: "credit" } });

    expect(firstEmitted(onChange)?.side).toBe("credit");
  });

  // ‏**التوزيع لا الحساب** (R-API-01): الجانب يقرّر أين يُوضع المبلغ عند التجميع خارج
  // المكوّن، ولا يمسّ المبلغ نفسه. والسعر ≠ 1 هنا مقصود: ضربٌ في السعر يُسقطه، كما
  // يُسقطه قلب الإشارة للدائن
  it("V14: المبلغ يُصدَر حرفياً مهما كان الجانب والسعر — لا قلب إشارة ولا ضرب", () => {
    const onChange = renderInput({
      currencyId: USD.id,
      value: { side: "credit", exchangeRate: "1320" }
    });

    fireEvent.change(screen.getByLabelText("المبلغ"), { target: { value: "250" } });

    expect(firstEmitted(onChange)?.amountFC).toBe("250");
    expect(firstEmitted(onChange)?.side).toBe("credit");
  });

  it("V15: السعر المقفل معطَّل ومعروض «1»", () => {
    renderInput({ isRateLocked: true, value: { exchangeRate: "1320" } });

    const rate = screen.getByLabelText("سعر الصرف");
    expect(rate).toBeDisabled();
    expect(rate).toHaveValue("1");
  });

  // ‏«1» في **كل إصدار فعلي** لا في العرض وحده: قيمةٌ واردة بسعر آخر لا تتسرّب إلى
  // الحمولة عبر حقل آخر (50012)
  it("V16: السعر المقفل يُصدَر «1» مع تعديل أي حقل آخر", () => {
    const onChange = renderInput({ isRateLocked: true, value: { exchangeRate: "1320" } });

    fireEvent.change(screen.getByLabelText("المبلغ"), { target: { value: "250" } });

    expect(firstEmitted(onChange)?.amountFC).toBe("250");
    expect(firstEmitted(onChange)?.exchangeRate).toBe("1");
  });

  // ‏`fireEvent.change` يُطلق `onChange` على الحقل المعطَّل — فهذا يقيس **المعالج** لا
  // الخاصية البصرية. `disabled` وحده يمرّ من `V15` ويرسب هنا
  it("V17: السعر المقفل لا يُحرَّر في المعالج ولو وصله حدث", () => {
    const onChange = renderInput({ isRateLocked: true });

    fireEvent.change(screen.getByLabelText("سعر الصرف"), { target: { value: "1330" } });

    expect(onChange).not.toHaveBeenCalled();
  });

  // ‏R-UI-02 وR-RPT-06: مبلغ بلا عملة معروفة لا يُدخَل، كما لا يُعرض. والفحص على
  // المعالج أيضاً لا على `disabled` وحده، للسبب نفسه في `V17`
  it("V18: العملة قيد التحميل ⟵ حقل المبلغ معطَّل ولا يُصدر", () => {
    const onChange = renderInput({ registry: "loading" });

    const amount = screen.getByLabelText("المبلغ");
    expect(amount).toBeDisabled();

    fireEvent.change(amount, { target: { value: "250" } });

    expect(onChange).not.toHaveBeenCalled();
  });

  it("V19: عملة مجهولة ⟵ حقل المبلغ معطَّل وإنذار معروض", () => {
    const onChange = renderInput({ currencyId: UNKNOWN_ID });

    const amount = screen.getByLabelText("المبلغ");
    expect(amount).toBeDisabled();
    expect(screen.getByText("تعذّر إدخال المبلغ: عملته غير معروفة.")).toBeInTheDocument();

    fireEvent.change(amount, { target: { value: "250" } });

    expect(onChange).not.toHaveBeenCalled();
  });

  // ‏سطور القيد تُرندر المكوّن مرات عدّة في الصفحة نفسها. بمعرّفات ثابتة يربط كل وسم
  // بالحقل الأول وحده، فيكتب المستخدم في سطر ويتغيّر سطر آخر
  it("V20: نسختان في الصفحة ⟵ كل وسم يربط حقل نسخته", () => {
    const first = vi.fn<(next: MoneyInputValue) => void>();
    const second = vi.fn<(next: MoneyInputValue) => void>();

    render(
      <CurrencyRegistryProvider currencies={REGISTRY}>
        <MoneyInput value={BASE} currencyId={IQD.id} isRateLocked={false} onChange={first} />
        <MoneyInput value={BASE} currencyId={IQD.id} isRateLocked={false} onChange={second} />
      </CurrencyRegistryProvider>
    );

    const amounts = screen.getAllByLabelText("المبلغ");
    expect(amounts).toHaveLength(2);

    fireEvent.change(amounts[1]!, { target: { value: "250" } });

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });
});
