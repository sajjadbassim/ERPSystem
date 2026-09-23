import Autocomplete from "@mui/material/Autocomplete";
import TextField from "@mui/material/TextField";

import { useAllBranches } from "../../api/useAllBranches";
import type { BranchItem } from "../../api/useAllBranches";
import { ApiErrorMessage } from "../ApiErrorMessage";

export type BranchPickerProps = {
  // ‏معرّف الفرع لا الكائن — اتساقاً مع `<AccountPicker>` و`<MoneyInput>`: يُبقي
  // عقد المستهلك مستقلاً عن شكل `BranchResponseDto`
  value: string | null;

  // ‏`null` عند تفريغ الاختيار: يفرّق «لا اختيار» عن «لم يُحمَّل بعد»
  onChange: (branchId: string | null) => void;
};

// ‏الرمز وحده غامض والاسم وحده أغمض. والحقلان مطلوبان في `BranchResponseDto` كما
// في `AccountResponseDto` — مقيس من العقد لا مفترَض
function optionLabel(branch: BranchItem): string {
  return `${branch.code} — ${branch.name}`;
}

export function BranchPicker(props: BranchPickerProps) {
  // ‏قرار الدَّين ٣: **تحميل كامل + فلترة محلية**. فلا نداء عند كل حرف
  const { status, branches, error } = useAllBranches();

  // ‏**ترشيح `isActive` في طبقة العرض لا طبقة الجلب** (قرار 2026-09-23): الهوك يبقى
  // مصدراً أميناً تحتاجه شاشة إدارة الفروع المستقبلية لترى المعطَّل بالضبط، والترشيح
  // خاصّ بهذا المنتقي — وهو المنطق نفسه المطبَّق على `isPostable` سلفاً.
  //
  // ‏و**الاستبعاد من المجموعة لا إخفاءٌ فوقها**: خيارٌ مُصيَّر ومخفيّ كان يبقى مبلوغاً
  // بالكتابة، فيختاره المستخدم ويُرحَّل على فرع معطَّل. يحرسه `B14` و`B15` معاً.
  //
  // ‏وهو تحقيق تجربة لا قاعدة (R-API-05): الخادم يرفض الاستعمال الجديد لما
  // ‏`IsActive = 0` بحكم R-LIFE-06 سواء رشّحنا أم لم نرشّح
  const options = branches.filter((branch) => branch.isActive);

  // ‏الاشتقاق من القائمة لا حفظ الكائن: القيمة الواردة **معرّف**، والكائن المطابق
  // له يُعثر عليه هنا. وقبل اكتمال التحميل لا مطابق فيكون `null` — لا كائن مصطنع
  const selected = options.find((branch) => branch.id === props.value) ?? null;

  return (
    <>
      <Autocomplete
        options={options}
        value={selected}
        loading={status === "pending"}
        loadingText="جارٍ تحميل الفروع…"
        // ‏**يُسكَت عند الفشل**: `Autocomplete` يُطلقه من تلقائه على `options: []`،
        // وهي بعينها حالة الفشل — فيصير العطل «لا نتائج مطابقة.» أو يزاحمها الخطأ
        // برسالتين متناقضتين. يحرسه `B13`
        noOptionsText={status === "error" ? "" : "لا نتائج مطابقة."}
        clearText="مسح الاختيار"
        getOptionLabel={optionLabel}
        isOptionEqualToValue={(option, value) => option.id === value.id}
        // ‏لفٌّ صريح بوسيط واحد — درس `S05` معمَّماً: توقيع `Autocomplete` رباعيّ
        // ‏(`event, value, reason, details`)، وتمريره مباشرةً يسرّب حدث DOM
        // وداخليات المكتبة إلى عقد المستهلك. يحرسه `B05`
        onChange={(_event, option) => props.onChange(option === null ? null : option.id)}
        renderInput={(params) => <TextField {...params} label="الفرع" />}
      />

      {/* ‏الخطأ **إلى جانب الحقل لا بدلاً منه**: إخفاء المنتقي عند الفشل يمنع
          المستخدم من رؤية سياقه، والحقل الفارغ وحده يكذب. يحرسه `B12` */}
      {status === "error" && error !== null ? <ApiErrorMessage error={error} /> : null}
    </>
  );
}
