import Big from "big.js";

import { useCurrencies } from "../../currency/currency-registry";

export type MoneyInputValue = {
  // ‏نصّ كما يُرسل على السلك، بلا `Number` (R-API-03)
  amountFC: string;
  currencyId: string;
  exchangeRate: string;
  exchangeRateDate: string;
};

export type MoneyInputProps = {
  value: MoneyInputValue;

  // ‏`null` ⟵ الحساب غير مقيَّد بعملة (`Account.CurrencyId IS NULL`)، فالاختيار حرّ.
  // وغير ذلك تُفرض عملة الحساب — وإلا ردّ الخادم الخطأ 50011
  accountCurrencyId: string | null;

  onChange: (next: MoneyInputValue) => void;
};

// ‏موجب حصراً — الخادم يفرضه بـ50014 على المبلغ و50020 على السعر. وكل ما ليس عشرياً
// موجباً يسقط في الشرط نفسه: الفارغ والمشوَّه والصفر والسالب. **شرط موجب واحد** بدل
// لائحة حالات تفشل مفتوحة أمام مُدخَل لم يخطر على بال.
//
// ‏و`Big` لا `Number`: R-API-03 [صارم] يمنع تمرير المبالغ عبر `Number`، ولو كان
// المقصود مقارنةً بالصفر وحدها — القاعدة على المسار لا على النيّة.
function isPositive(amount: string): boolean {
  try {
    return new Big(amount).gt(0);
  } catch {
    return false;
  }
}

export function MoneyInput(props: MoneyInputProps) {
  const currencies = useCurrencies();

  // ‏القفل من **قيد الحساب** لا من العملة الحالية: حساب صندوق الدولار لا يقبل حركة
  // بغير عملته (R-PAY: الحسابات النقدية أحادية العملة)، والخادم يردّها بـ50011
  const isCurrencyLocked = props.accountCurrencyId !== null;

  // ‏R-API-05: هذا تحقق **للتجربة وحدها**، والمعتبر ما يفرضه الخادم. غرضه أن يرى
  // المستخدم الخطأ قبل رحلة ذهاب وإياب، لا أن يحلّ محلّ الفحص المعتبر
  const messages: string[] = [];

  if (!isPositive(props.value.amountFC)) {
    messages.push("المبلغ يجب أن يكون موجباً.");
  }

  if (!isPositive(props.value.exchangeRate)) {
    messages.push("سعر الصرف يجب أن يكون موجباً.");
  }

  // ‏يُصدر ما أُدخل حرفياً: لا تقريب ولا بتر (R-AMT-07 — التقريب عند العرض وحده).
  // والعمود في القاعدة `DECIMAL(19,4)` مهما كانت خانات العملة، فتقريب المُدخَل
  // يغيّر **قيمة مالية** لا صورة عرض
  const emit = (patch: Partial<MoneyInputValue>) => {
    props.onChange({ ...props.value, ...patch });
  };

  return (
    <div>
      <label htmlFor="money-input-amount">المبلغ</label>
      <input
        id="money-input-amount"
        value={props.value.amountFC}
        onChange={(event) => emit({ amountFC: event.target.value })}
      />

      {/* ‏R-UI-02: العملة ملازمة للمبلغ في الحقل نفسه. حقل رقم عارٍ هو الرقم المجرد
          الممنوع بعينه، وإن كانت العملة محفوظة في الحالة دون أن تُقرأ */}
      <label htmlFor="money-input-currency">العملة</label>
      <select
        id="money-input-currency"
        value={props.value.currencyId}
        disabled={isCurrencyLocked}
        onChange={(event) => emit({ currencyId: event.target.value })}
      >
        {currencies.map((currency) => (
          <option key={currency.id} value={currency.id}>
            {currency.symbol ?? currency.code}
          </option>
        ))}
      </select>

      {/* ‏يُدخل يدوياً ولا يُشتق: لا نقطة بحث بالعملتين والتاريخ في العقد، واختيار
          السعر المنطبق قرار مالي تمنعه R-API-01. والتجاوز اليدوي مسنود بـR-FX-06 */}
      <label htmlFor="money-input-rate">سعر الصرف</label>
      <input
        id="money-input-rate"
        value={props.value.exchangeRate}
        onChange={(event) => emit({ exchangeRate: event.target.value })}
      />

      <label htmlFor="money-input-rate-date">تاريخ سعر الصرف</label>
      <input
        id="money-input-rate-date"
        type="date"
        value={props.value.exchangeRateDate}
        onChange={(event) => emit({ exchangeRateDate: event.target.value })}
      />

      {messages.length > 0 ? <p role="alert">{messages.join(" ")}</p> : null}
    </div>
  );
}
