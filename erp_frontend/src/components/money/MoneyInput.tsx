import { useId } from "react";
import Big from "big.js";

import { useCurrencyLookup } from "../../currency/currency-registry";

// ‏الجانب يقرّر **أين** يوضع المبلغ عند التجميع خارج المكوّن (`debitFC` أو `creditFC`)،
// ولا يمسّ المبلغ نفسه: توزيع لا حساب (R-API-01)
export type MoneyInputSide = "debit" | "credit";

export type MoneyInputValue = {
  // ‏نصّ كما يُرسل على السلك، بلا `Number` (R-API-03)
  amountFC: string;
  side: MoneyInputSide;
  exchangeRate: string;
  exchangeRateDate: string;
};

export type MoneyInputProps = {
  value: MoneyInputValue;

  // ‏خاصية عرض لا جزء من القيمة: العملة يقرّرها المستضيف (عملة الحساب أو اختيار
  // المستخدم في منتقٍ خارجي)، ونسخة ثانية منها في القيمة مصدرٌ ثانٍ لحقيقة واحدة
  // (R-CUR-03). والمعرّف لا الرمز، كما في `<MoneyDisplay>`: ليبقى فرعا «لم يُحمَّل»
  // و«مجهولة» حارسين حقيقيين داخل المكوّن
  currencyId: string;

  // ‏المكوّن ينفّذ القفل ولا يعرف سببه: متى تكون العملة عملة الدفاتر (50012) قرار
  // سياق شركة يملكه نموذج القيد (`V08`)
  isRateLocked: boolean;

  onChange: (next: MoneyInputValue) => void;
};

const LOCKED_RATE = "1";

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
  const id = useId();
  const { isReady, currency } = useCurrencyLookup(props.currencyId);

  // ‏R-UI-02 وR-RPT-06: مبلغ لا تُعرف عملته لا يُدخَل كما لا يُعرض
  const isAmountEnabled = isReady && currency !== undefined;

  const exchangeRate = props.isRateLocked ? LOCKED_RATE : props.value.exchangeRate;

  // ‏R-API-05: هذا تحقق **للتجربة وحدها**، والمعتبر ما يفرضه الخادم. غرضه أن يرى
  // المستخدم الخطأ قبل رحلة ذهاب وإياب، لا أن يحلّ محلّ الفحص المعتبر
  const messages: string[] = [];

  if (!isPositive(props.value.amountFC)) {
    messages.push("المبلغ يجب أن يكون موجباً.");
  }

  if (!isPositive(exchangeRate)) {
    messages.push("سعر الصرف يجب أن يكون موجباً.");
  }

  // ‏يُصدر ما أُدخل حرفياً: لا تقريب ولا بتر (R-AMT-07 — التقريب عند العرض وحده).
  // والعمود في القاعدة `DECIMAL(19,4)` مهما كانت خانات العملة، فتقريب المُدخَل
  // يغيّر **قيمة مالية** لا صورة عرض.
  //
  // ‏والسعر المقفل يُفرض **في كل إصدار** لا في العرض وحده، فلا تتسرّب قيمة واردة بسعر
  // آخر عبر تعديل حقل غيره (`V16`)
  const emit = (patch: Partial<MoneyInputValue>) => {
    props.onChange({ ...props.value, ...patch, exchangeRate: patch.exchangeRate ?? exchangeRate });
  };

  return (
    <div>
      {/* ‏R-UI-02: العملة ملازمة للمبلغ ومربوطة به. والحراسة في المعالج لا في
          `disabled` وحده: الحدث يصل الحقل المعطَّل برمجياً (`V18`, `V19`) */}
      <label htmlFor={`${id}-amount`}>المبلغ</label>
      <input
        id={`${id}-amount`}
        value={props.value.amountFC}
        disabled={!isAmountEnabled}
        aria-describedby={`${id}-currency`}
        onChange={(event) => {
          if (isAmountEnabled) {
            emit({ amountFC: event.target.value });
          }
        }}
      />
      {!isReady ? (
        <span id={`${id}-currency`} aria-busy="true">…</span>
      ) : currency === undefined ? (
        <span id={`${id}-currency`} role="alert">تعذّر إدخال المبلغ: عملته غير معروفة.</span>
      ) : (
        // ‏R-RPT-04: الرمز من السجل لا ثابتاً في الكود، ويسقط إلى الكود عند غيابه
        <span id={`${id}-currency`}>{currency.symbol ?? currency.code}</span>
      )}

      <label htmlFor={`${id}-side`}>الجانب</label>
      <select
        id={`${id}-side`}
        value={props.value.side}
        onChange={(event) => emit({ side: event.target.value as MoneyInputSide })}
      >
        <option value="debit">مدين</option>
        <option value="credit">دائن</option>
      </select>

      {/* ‏يُدخل يدوياً ولا يُشتق: لا نقطة بحث بالعملتين والتاريخ في العقد، واختيار
          السعر المنطبق قرار مالي تمنعه R-API-01. والتجاوز اليدوي مسنود بـR-FX-06.
          والقفل في المعالج لا في `disabled` وحده (`V17`) */}
      <label htmlFor={`${id}-rate`}>سعر الصرف</label>
      <input
        id={`${id}-rate`}
        value={exchangeRate}
        disabled={props.isRateLocked}
        onChange={(event) => {
          if (!props.isRateLocked) {
            emit({ exchangeRate: event.target.value });
          }
        }}
      />

      <label htmlFor={`${id}-rate-date`}>تاريخ سعر الصرف</label>
      <input
        id={`${id}-rate-date`}
        type="date"
        value={props.value.exchangeRateDate}
        onChange={(event) => emit({ exchangeRateDate: event.target.value })}
      />

      {messages.length > 0 ? <p role="alert">{messages.join(" ")}</p> : null}
    </div>
  );
}
