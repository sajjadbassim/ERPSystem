import { useQuery } from "@tanstack/react-query";

import type { components } from "../../api-types/schema";

export type AccountItem = components["schemas"]["AccountResponseDto"];

type AccountsEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfAccountResponseDto"];

export type UseAccountsResult = {
  status: "pending" | "success" | "error";

  // ‏**قائمة لا `undefined` أبداً.** العقد يجعل `data` قابلة للعدم في الغلافين معاً
  // (`required: null` في كليهما)، فلو سُرِّبت القابلية للعدم إلى المستدعي لتكرّر فحصها
  // في كل شاشة — ونسيانه مرة يُسقط الشاشة على استجابة صالحة تماماً بحسب العقد
  items: AccountItem[];

  // ‏العقد يصف العدّادات بـ`["integer","string"]`، فتُطبَّع هنا مرة واحدة لا في كل مستدعٍ
  totalCount: number;
};

// ‏`undefined` ⟵ صفر (غياب الحقل حالة يصفها العقد). وما عداه يمرّ بـ`Number`:
// قيمة تخالف نمط العقد تُخرج `NaN` **فتظهر** بدل أن تُبتلع صفراً صامتاً — النهج نفسه
// المعتمَد في `<MoneyDisplay>` مع `decimalPlaces`: لا فرع دفاعي لا يحرسه اختبار.
//
// وهذا **عدّاد عرض لا مبلغ**، فلا يقع تحت منع `Number` في R-API-03
function toCount(value: number | string | undefined): number {
  return value === undefined ? 0 : Number(value);
}

type AccountsPage = {
  items: AccountItem[];
  totalCount: number;
};

async function fetchAccounts(): Promise<AccountsPage> {
  const response = await fetch("/api/accounts");

  // ‏**الرمي إلزامي هنا لا تحسين.** ابتلاع 401 كقائمة فارغة يجعل المصادقة الغائبة
  // تبدو **شجرة حسابات فارغة** — رقم صحيح المعنى خاطئ السبب، وهو الفشل المفتوح
  // الذي يمنعه المشروع كله. يحرسه `Q06`، وأُثبت بعطل متعمَّد.
  if (!response.ok) {
    throw new Error(`تعذّر جلب الحسابات (${response.status}).`);
  }

  const body = (await response.json()) as AccountsEnvelope;

  // ‏ثلاث طبقات قابلة للعدم تُفكّ في سطرين: الغلاف، ثم الصفحة، ثم مصفوفتها
  const page = body.data ?? undefined;

  return {
    items: page?.data ?? [],
    totalCount: toCount(page?.totalCount)
  };
}

export function useAccounts(): UseAccountsResult {
  const query = useQuery({ queryKey: ["accounts"], queryFn: fetchAccounts });

  return {
    status: query.status,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0
  };
}
