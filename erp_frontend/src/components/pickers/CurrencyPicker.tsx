import Autocomplete from "@mui/material/Autocomplete";
import TextField from "@mui/material/TextField";

import { useCurrencyRegistry } from "../../currency/currency-registry";
import type { RegisteredCurrency } from "../../currency/currency-registry";
import { ApiErrorMessage } from "../ApiErrorMessage";

export type CurrencyPickerProps = {
  // ‏معرّف العملة لا الكائن — اتساقاً مع `<BranchPicker>` و`<AccountPicker>`
  // و`<MoneyInput>`: يُبقي عقد المستهلك مستقلاً عن شكل `CurrencyResponseDto`
  value: string | null;

  // ‏`null` عند تفريغ الاختيار: يفرّق «لا اختيار» عن «لم يُحمَّل بعد»
  onChange: (currencyId: string | null) => void;
};

// ‏`symbol ?? code` — **النمط نفسه المفروض في `<MoneyDisplay>:61`** بحكم R-RPT-04:
// الرمز من السجل لا ثابتاً في الكود، **ويسقط إلى الكود عند غيابه لا إلى لا شيء**.
// و`symbol` هو الحقل الاختياري الوحيد في `CurrencyResponseDto` (مقيس من العقد)،
// فبلا السقوط يُعرض `" — يورو"`. يحرسه `CUR15`.
function optionLabel(currency: RegisteredCurrency): string {
  return `${currency.symbol ?? currency.code} — ${currency.name}`;
}

export function CurrencyPicker(props: CurrencyPickerProps) {
  // ‏**السجل لا هوك خاصّ** (المسار أ): المفتاح `["currencies","all"]` محجوز فعلاً
  // في `currency-source.tsx`، فاستعلام ثانٍ يعني إمّا تعريفين لمدخل ذاكرة واحد
  // (تمنعه R-CUR-03) وإمّا حلقة صفحات مكرَّرة. يحرسه `CUR16`.
  const { isReady, currencies, error } = useCurrencyRegistry();

  // ‏**الفشل يُفحص قبل الجهوز، لا بعده.** السجل يطوي الفشل في «قيد التحميل» بقصد
  // (`C05`)، فـ`isReady === false` تصدق في الحالتين. والترتيب المعكوس كان يعرض
  // «جارٍ التحميل…» إلى الأبد على شبكة معطوبة — فشلٌ مفتوح صامت
  const hasFailed = error !== null;

  // ‏**ترشيح `isActive` في طبقة العرض لا طبقة الجلب** — وهنا **مُلزِم بنيوياً** لا
  // تحقيقَ تجربة كما في `<BranchPicker>`: لو هبط إلى مصدر السجل المشترك لاختفت
  // العملة المعطَّلة عن `useCurrencyLookup`، **فأنذر كل مبلغ تاريخيّ بها** بـR-RPT-06.
  //
  // ‏و**الاستبعاد من المجموعة لا إخفاءٌ فوقها**: خيارٌ مُصيَّر ومخفيّ يبقى مبلوغاً
  // بالكتابة. يحرسه `CUR13` و`CUR14` معاً.
  //
  // ‏وهو تحقيق تجربة لا قاعدة (R-API-05): الخادم يرفض الاستعمال الجديد لما
  // ‏`IsActive = 0` بحكم R-LIFE-06 سواء رشّحنا أم لم نرشّح
  const options = currencies.filter((currency) => currency.isActive);

  // ‏الاشتقاق من القائمة لا حفظ الكائن: القيمة الواردة **معرّف**، والكائن المطابق
  // له يُعثر عليه هنا. وقبل اكتمال التحميل لا مطابق فيكون `null` — لا كائن مصطنع
  const selected = options.find((currency) => currency.id === props.value) ?? null;

  return (
    <>
      <Autocomplete
        options={options}
        value={selected}
        loading={!hasFailed && !isReady}
        loadingText="جارٍ تحميل العملات…"
        // ‏**يُسكَت عند الفشل**: `Autocomplete` يُطلقه من تلقائه على `options: []`،
        // وهي بعينها حالة الفشل — فيصير العطل «لا نتائج مطابقة.» أو يزاحمها الخطأ
        // برسالتين متناقضتين. يحرسه `CUR12`
        noOptionsText={hasFailed ? "" : "لا نتائج مطابقة."}
        clearText="مسح الاختيار"
        getOptionLabel={optionLabel}
        isOptionEqualToValue={(option, value) => option.id === value.id}
        // ‏لفٌّ صريح بوسيط واحد — درس `S05` معمَّماً: توقيع `Autocomplete` رباعيّ
        // ‏(`event, value, reason, details`)، وتمريره مباشرةً يسرّب حدث DOM
        // وداخليات المكتبة إلى عقد المستهلك. يحرسه `CUR05`
        onChange={(_event, option) => props.onChange(option === null ? null : option.id)}
        renderInput={(params) => <TextField {...params} label="العملة" />}
      />

      {/* ‏الخطأ **إلى جانب الحقل لا بدلاً منه**: إخفاء المنتقي عند الفشل يمنع
          المستخدم من رؤية سياقه، والحقل الفارغ وحده يكذب. يحرسه `CUR11` */}
      {hasFailed ? <ApiErrorMessage error={error} /> : null}
    </>
  );
}
