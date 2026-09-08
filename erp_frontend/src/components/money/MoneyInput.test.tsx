import { afterEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import { CurrencyRegistryProvider } from "../../currency/currency-registry";
import type { RegisteredCurrency } from "../../currency/currency-registry";
import { MoneyInput } from "./MoneyInput";
import type { MoneyInputValue } from "./MoneyInput";

// ‏مصفوفة الحالات V — نظير U لـ`<MoneyDisplay>`.
//
// **الفجوة عند `V08` و`V10` مقصودة ولا تُردم بإعادة ترقيم**، بالقاعدة المكتوبة في
// ‏`erp_backend/docs/STATE.md`: إعادة الترقيم تجعل الإحالة تشير إلى غير مقصودها بصمت.
//   `V08` (عملة الدفاتر ⟵ سعر 1، الخطأ 50012) — مؤجَّل إلى §4.5 لأنه يستلزم
//         `Company.baseCurrencyId`، وهو سياق شركة لا شأن حقلٍ واحد.
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

const REGISTRY = [IQD, USD];

const BASE: MoneyInputValue = {
  amountFC: "100.0000",
  currencyId: IQD.id,
  exchangeRate: "1",
  exchangeRateDate: "2026-09-08"
};

type RenderOptions = {
  value?: Partial<MoneyInputValue>;
  accountCurrencyId?: string | null;
};

function renderInput(options: RenderOptions = {}) {
  const onChange = vi.fn<(next: MoneyInputValue) => void>();

  render(
    <CurrencyRegistryProvider currencies={REGISTRY}>
      <MoneyInput
        value={{ ...BASE, ...options.value }}
        accountCurrencyId={options.accountCurrencyId ?? null}
        onChange={onChange}
      />
    </CurrencyRegistryProvider>
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

    // ‏مساواة لا احتواء: إضافة `debitBase` لاحقاً تُسقط هذا التأكيد — وهو المقصود.
    // العقد لا يُنمّى بصمت، والمقابل بالأساس ليس من التقاط هذا الحقل (R-API-01)
    expect(Object.keys(firstEmitted(onChange) ?? {}).sort()).toEqual([
      "amountFC",
      "currencyId",
      "exchangeRate",
      "exchangeRateDate"
    ]);
    expect(firstEmitted(onChange)?.amountFC).toBe("250");
  });

  it("V02: حساب مقيَّد بعملة ⟵ العملة معروضة ومقفلة (50011)", () => {
    const onChange = renderInput({
      accountCurrencyId: USD.id,
      value: { currencyId: USD.id, exchangeRate: "1320" }
    });

    expect(screen.getByLabelText("العملة")).toBeDisabled();

    fireEvent.change(screen.getByLabelText("المبلغ"), { target: { value: "250" } });

    expect(firstEmitted(onChange)?.currencyId).toBe(USD.id);
  });

  it("V03: حساب بلا عملة ⟵ الاختيار حرّ", () => {
    const onChange = renderInput({ accountCurrencyId: null });

    const currency = screen.getByLabelText("العملة");
    expect(currency).toBeEnabled();

    fireEvent.change(currency, { target: { value: USD.id } });

    expect(firstEmitted(onChange)?.currencyId).toBe(USD.id);
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
    const onChange = renderInput({ value: { currencyId: IQD.id } });

    fireEvent.change(screen.getByLabelText("المبلغ"), { target: { value: "10.5" } });

    expect(firstEmitted(onChange)?.amountFC).toBe("10.5");
  });

  // ‏`V12` **يسدّ حدّاً مقيساً في `V06`، لا يكرّره.** العطل `N-1` أثبت أن `V06` يمسك
  // الاشتقاق عند **التركيب**، لكنه لا يبدّل العملة قط — فاشتقاقٌ يُطلَق عند تبديلها
  // وحدها كان يمرّ من تحته سالماً، وهو المسار الأرجح عملياً لتسلّل الاشتقاق.
  //
  // والتأكيد على `currencyId` **قبل** تأكيد الجلب مقصود: بدونه يصير «لم يُستدعَ
  // ‏`fetch`» صادقاً بالفراغ لو تعطّل التبديل أصلاً — حارسٌ يمرّ لأن شيئاً لم يحدث.
  it("V12: تبديل العملة لا يُطلق أي جلب لاشتقاق السعر", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const onChange = renderInput({ accountCurrencyId: null });

    fireEvent.change(screen.getByLabelText("العملة"), { target: { value: USD.id } });

    expect(firstEmitted(onChange)?.currencyId).toBe(USD.id);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("V11: العملة مقروءة بجوار المبلغ لا قيمةً مخزَّنة فقط", () => {
    renderInput({ value: { currencyId: IQD.id } });

    const currency = screen.getByLabelText("العملة") as HTMLSelectElement;
    const selected = currency.options[currency.selectedIndex];

    // ‏قابل للرسوب فعلاً: تسمية الخيارات بالاسم («دينار عراقي») بدل الرمز تُسقطه
    expect(selected?.textContent ?? "").toContain("د.ع");
  });
});
