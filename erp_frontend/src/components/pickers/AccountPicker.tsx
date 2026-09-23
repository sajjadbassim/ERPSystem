import Autocomplete from "@mui/material/Autocomplete";
import TextField from "@mui/material/TextField";

import { useAllAccounts } from "../../api/useAllAccounts";
import type { AccountItem } from "../../api/useAccounts";
import { ApiErrorMessage } from "../ApiErrorMessage";

export type AccountPickerProps = {
  // ‏معرّف الحساب لا الكائن — اتساقاً مع `currencyId` في `<MoneyInput>`: يُبقي عقد
  // المستهلك مستقلاً عن شكل `AccountResponseDto`
  value: string | null;

  // ‏`null` عند تفريغ الاختيار: يفرّق «لا اختيار» عن «لم يُحمَّل بعد»
  onChange: (accountId: string | null) => void;
};

// ‏الرمز وحده غامض والاسم وحده أغمض. والحقلان مقيسان في `AccountResponseDto`
function optionLabel(account: AccountItem): string {
  return `${account.code} — ${account.name}`;
}

export function AccountPicker(props: AccountPickerProps) {
  // ‏قرار الدَّين ٣: **تحميل كامل + فلترة محلية**. فلا نداء عند كل حرف، والفلترة
  // من Autocomplete على القائمة المحمَّلة (يحرسها `S02`)
  //
  // ‏**و`error` يُستهلك منذ 2026-09-23 — وكان يُسقَط.** إسقاطه كان يجعل الفشل
  // (401/شبكة/5xx) يظهر «لا نتائج مطابقة.» — أي الفشل المفتوح الذي وُجدت `Q06`
  // لمنعه نصّاً، وخرقاً لـ R-RPT-06. سُدّ بـ`S11` في جولة `<BranchPicker>` نفسها
  // كي لا يفترق المنتقيان على القرص
  const { status, accounts, error } = useAllAccounts();

  // ‏الاشتقاق من القائمة لا حفظ الكائن: القيمة الواردة **معرّف**، والكائن المطابق
  // له يُعثر عليه هنا. وقبل اكتمال التحميل لا مطابق فيكون `null` — لا كائن مصطنع
  const selected = accounts.find((account) => account.id === props.value) ?? null;

  return (
    <>
      <Autocomplete
        options={accounts}
        value={selected}
        loading={status === "pending"}
        loadingText="جارٍ تحميل الحسابات…"
        // ‏**يُسكَت عند الفشل**: `Autocomplete` يُطلقه من تلقائه على `options: []`،
        // وهي بعينها حالة الفشل — فيصير العطل «لا نتائج مطابقة.». يحرسه `S11`
        noOptionsText={status === "error" ? "" : "لا نتائج مطابقة."}
        clearText="مسح الاختيار"
        getOptionLabel={optionLabel}
        isOptionEqualToValue={(option, value) => option.id === value.id}
        // ‏لفٌّ صريح بوسيط واحد. توقيع Autocomplete هو
        // ‏`(event, value, reason, details)` — أربعة وسائط — فتمريره مباشرةً **يسرّب
        // حدث DOM وداخليات المكتبة إلى عقد المستهلك**. وهنا الخطر افتراضيّ لا عَرَضيّ،
        // بخلاف `F01` و`G06` حيث كان زلّة محتملة. يحرسه `S05`
        onChange={(_event, option) => props.onChange(option === null ? null : option.id)}
        renderInput={(params) => <TextField {...params} label="الحساب" />}
      />

      {/* ‏الخطأ **إلى جانب الحقل لا بدلاً منه**: إخفاء المنتقي عند الفشل يمنع
          المستخدم من رؤية سياقه، والحقل الفارغ وحده يكذب. يحرسه `S11` */}
      {status === "error" && error !== null ? <ApiErrorMessage error={error} /> : null}
    </>
  );
}
