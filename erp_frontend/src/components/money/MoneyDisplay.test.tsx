import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import type { ReactElement } from "react";

import { CurrencyRegistryProvider } from "../../currency/currency-registry";
import type { RegisteredCurrency } from "../../currency/currency-registry";
import { MoneyDisplay } from "./MoneyDisplay";

// ‏مصفوفة الحالات U (واجهة) — نظير A/B/M/N في `ErpApi.Tests`. كل اختبار يحمل رقم
// حالته في اسمه، فيُحال إليه في التوثيق بلا وصف.
//
// **هذه الاختبارات هي عقد `<MoneyDisplay>` لا وصفه.** كُتبت قبل المكوّن عمداً
// (‏ROADMAP §3 خطوة ٥).

// ‏`RegisteredCurrency` **مشتق من العقد** لا مكتوب بيد (R-API-04)، فهذه القيم تُخطئ
// الترجمة إن تغيّر `CurrencyResponseDto` في الخلفية — بدل أن تنحرف بصمت
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

// عملة بلا رمز — `symbol` قابل للعدم في العقد (`schema.d.ts:1528`)
const EUR_NO_SYMBOL: RegisteredCurrency = {
  id: "0199a1f0-0000-7000-8000-000000000003",
  code: "EUR",
  name: "يورو",
  symbol: null,
  decimalPlaces: 2,
  isActive: true
};

const REGISTRY = [IQD, USD, EUR_NO_SYMBOL];

function renderInRegistry(element: ReactElement): string {
  const { container } = render(
    <CurrencyRegistryProvider currencies={REGISTRY}>{element}</CurrencyRegistryProvider>
  );

  return (container.textContent ?? "").trim();
}

// ‏«رقم مجرد» = نصّ لا يبقى منه شيء بعد نزع الأرقام والفواصل والمسافات. الصياغة
// **شاملة لا لائحة عملات معروفة**: عملة جديدة بلا رمز تسقط في الشرط نفسه، بينما
// لائحة أسماء كانت ستمرّرها — فتفشل مفتوحة (نظير `M02`)
function isBareNumber(text: string): boolean {
  return text.replace(/[\d\s.,٫٬+-]/gu, "").length === 0;
}

describe("MoneyDisplay — R-UI-02 / R-RPT-04..06", () => {
  it("U01: يعرض العملة مع الرقم — رمزها من السجل لا ثابتاً في الكود", () => {
    const text = renderInRegistry(<MoneyDisplay amount="1000.0000" currencyId={IQD.id} />);

    expect(text).toContain("1,000");
    expect(text).toContain("د.ع");
  });

  it("U02: لا يعرض رقماً مجرداً بلا عملة أبداً — شرط موجب يفشل مغلقاً", () => {
    const text = renderInRegistry(<MoneyDisplay amount="1000.0000" currencyId={IQD.id} />);

    expect(isBareNumber(text)).toBe(false);
  });

  it("U03: عملة مجهولة ⟵ خطأ معروض لا رقم (R-RPT-06)", () => {
    const unknownId = "0199a1f0-0000-7000-8000-00000000ffff";
    const { container, queryByRole } = render(
      <CurrencyRegistryProvider currencies={REGISTRY}>
        <MoneyDisplay amount="1000.0000" currencyId={unknownId} />
      </CurrencyRegistryProvider>
    );

    // الرقم بلا عملته صحيح عددياً وخاطئ في معناه — وعرضه أسوأ من عدم عرضه
    expect(container.textContent ?? "").not.toMatch(/1[,.]?0{3}/u);
    expect(queryByRole("alert")).not.toBeNull();
  });

  it("U04: الخانات من `Currency.DecimalPlaces`، والزائد **يُقرَّب لا يُبتر**", () => {
    // ‏القيمتان مختارتان ليفترق الناتج بين ثلاث سياسات، لا اثنتين:
    //
    //   IQD (0 خانات) ⟸ "1000.5000"
    //     قصّ ⟶ 1,000 | نصف-إلى-الزوج ⟶ 1,000 | **نصف-إلى-أعلى ⟶ 1,001**
    //   USD (2 خانة) ⟸ "1000.1250"
    //     قصّ ⟶ 1,000.12 | نصف-إلى-الزوج ⟶ 1,000.12 | **نصف-إلى-أعلى ⟶ 1,000.13**
    //
    // فنجاح الحالتين معاً لا يتحقق إلا بـ round-half-up وحده. والقصّ مرفوض لأنه
    // يعرض للمستخدم **رقماً أصغر من المبلغ الفعلي** — خرق للعرض الصادق حتى وإن لم
    // يمسّ المخزَّن ولا المحسوب في القيد.
    const dinar = renderInRegistry(<MoneyDisplay amount="1000.5000" currencyId={IQD.id} />);
    const dollar = renderInRegistry(<MoneyDisplay amount="1000.1250" currencyId={USD.id} />);

    expect(dinar).toContain("1,001");
    expect(dollar).toContain("1,000.13");

    // ‏وصفر خانات يعني **لا كسر معروضاً أصلاً**، لا كسراً مصفَّراً
    expect(dinar).not.toMatch(/\d\.\d/u);
  });

  // ‏⚠ تأكيد `isBareNumber` أدناه **إعادة صياغة لا حارس مستقل عن `U02`.**
  //
  // ‏نجاح `toContain("EUR")` قبله **يستلزمه منطقياً**: `isBareNumber` تُرجع `false` لأي
  // نصّ يبقى فيه محرف غير رقمي، و`"EUR"` كذلك. فلا عطل في التنفيذ يُرسبه وحده —
  // قِيس ذلك في المرحلة ٤ (العطل `M-3`): رسب `toContain` فلم يُبلَغ هذا السطر أصلاً.
  //
  // ‏يبقى مكتوباً لأنه يوثّق النيّة، **ولا يُحتسب ضمن ما يحرسه `U05`**. وحراسة «لا رقم
  // مجرد» يؤدّيها `U02` وحده، وهي مُثبَتة بعطل حقيقي (`M-3` أرسبها على النصّ `'1,000'`).
  it("U05: عملة بلا رمز تسقط إلى الكود ولا تسقط إلى رقم مجرد", () => {
    const text = renderInRegistry(<MoneyDisplay amount="1000.0000" currencyId={EUR_NO_SYMBOL.id} />);

    expect(text).toContain("EUR");
    expect(isBareNumber(text)).toBe(false);
  });
});
