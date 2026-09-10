import Big from "big.js";

import { useCurrencyLookup } from "../../currency/currency-registry";

export type MoneyDisplayProps = {
  // ‏نصّ كما يصل من السلك، بلا `Number` (R-API-03)
  amount: string;

  // ‏المعرّف لا كائن العملة: ليبقى مسار «عملة مجهولة» حارساً حقيقياً داخل المكوّن
  currencyId: string;
};

// ‏**سياسة التقريب مركزية في دالة واحدة ولا تُكرَّر** (R-AMT-07). ومركزيتها مضمونة
// بـR-UI-02: ما دام لا يُعرض مبلغ خارج `<MoneyDisplay>`، فلا موضع ثانٍ يقرّب.
//
// ‏`round-half-up` لا القصّ ولا نصف-إلى-الزوج. القصّ يعرض رقماً **أصغر من المبلغ
// الفعلي** فيكذب على المستخدم وإن لم يمسّ المخزَّن. ونصف-إلى-الزوج يخالف ما يتوقعه
// قارئ الفاتورة. مقيس على `big.js@7.0.1`: `1000.5000`⟶`1001` و`1000.1250`⟶`1000.13`،
// بينما نصف-إلى-الزوج يُخرج `1000` و`1000.12`.
//
// والتجميع يدويّ لا بـ`Intl.NumberFormat`: مخرَج `Intl` يتبع ICU الخاص بزمن التشغيل
// ومحارف اللغة (فالعربية تُخرج `١٬٠٠٠٫١٣`)، وعقد العرض هنا مثبَّت بالنصّ في `U01`–`U05`.
// والتجميع علامة ترقيم لا حساب، فلا يمسّ الرقم.
function formatAmount(amount: string, decimalPlaces: number): string {
  const rounded = new Big(amount).round(decimalPlaces, Big.roundHalfUp).toFixed(decimalPlaces);

  const [integerPart = "", fractionPart] = rounded.split(".");
  const sign = integerPart.startsWith("-") ? "-" : "";
  const digits = sign === "" ? integerPart : integerPart.slice(1);
  const grouped = digits.replace(/\B(?=(\d{3})+(?!\d))/gu, ",");

  return fractionPart === undefined ? `${sign}${grouped}` : `${sign}${grouped}.${fractionPart}`;
}

export function MoneyDisplay(props: MoneyDisplayProps) {
  const { isReady, currency } = useCurrencyLookup(props.currencyId);

  // ‏**«لم يُحمَّل بعد» ليست «عملة مجهولة»** (الدَّين ٢، 2026-09-10). وبلا هذا الفرع
  // كان كل مبلغ يومض بإنذار R-RPT-06 ريثما يصل السجل — فيصير الإنذار الذي وُجد
  // ليكون استثناءً مشهداً معتاداً، ويتعلّم المستخدم تجاهله. يحرسه `C01`.
  //
  // ‏ولا يُعرض الرقم هنا أيضاً: R-RPT-06 قائم في الحالتين — الرقم بلا عملته ممنوع
  // سواء أكانت مجهولة أم لم تصل بعد
  if (!isReady) {
    return <span aria-busy="true">…</span>;
  }

  // ‏R-RPT-06: الرقم بلا عملته صحيح عددياً وخاطئ في معناه — وعرضه أسوأ من عدم عرضه.
  // فلا يُعرض `props.amount` هنا بحال
  if (currency === undefined) {
    return <span role="alert">تعذّر عرض المبلغ: عملته غير معروفة.</span>;
  }

  // ‏العقد يصف `decimalPlaces` بـ`number | string` لا `number` (`schema.d.ts:1530`)،
  // وافتراض `number` كان ينفجر على استجابة حقيقية. وقيمة خارج uint8 خرقٌ للعقد
  // تُظهره `big.js` برمي صريح — لا برقم ناقص الخانات يمرّ صامتاً
  const decimalPlaces = Number(currency.decimalPlaces);

  // ‏R-RPT-04: الرمز من السجل لا ثابتاً في الكود، ويسقط إلى الكود عند غيابه —
  // ولا يسقط إلى لا شيء، لأن ذلك يُنتج الرقم المجرد الممنوع (R-RPT-05)
  const label = currency.symbol ?? currency.code;

  return (
    <span>
      {formatAmount(props.amount, decimalPlaces)} {label}
    </span>
  );
}
