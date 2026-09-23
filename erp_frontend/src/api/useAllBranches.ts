import { useQuery } from "@tanstack/react-query";

import type { components } from "../../api-types/schema";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, readFailure } from "./api-error";
import { apiFetch } from "./http";
import { MASTER_DATA_STALE_TIME } from "./query-config";

export type BranchItem = components["schemas"]["BranchResponseDto"];

type BranchesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfBranchResponseDto"];

export type UseAllBranchesResult = {
  status: "pending" | "success" | "error";

  // ‏قائمة لا `undefined` أبداً — النهج نفسه المعتمَد في `useAllAccounts`
  branches: BranchItem[];

  // ‏**الخطأ يبلغ المستهلك** (الدَّين ٦): بدونه لا يملك المنتقي إلا أن يعرض نصّاً
  // من تأليفه أو — أسوأ — «لا نتائج مطابقة.» فيبدو الفشل قائمةً فارغة. يحرسه `B12`
  error: Error | null;
};

// ‏`MaxPageSize = 100` في قواعد الباك-إند (بند 10)، فطلب أكثر منه يُقلَّص صامتاً
const PAGE_SIZE = 100;

// ‏سقف أمان: خادم يُرجع `hasNextPage: true` أبداً كان سيعلّق المتصفح في حلقة شبكة
// لا تنتهي. والسقف مشتقّ لا اعتباطيّ: 100 صفحة × 100 = عشرة آلاف فرع، وعندها يكون
// قرار «التحميل الكامل» (الدَّين ٣) قد سقط أصلاً بنصّه.
// ‏⚠ **فرع بلا حارس** — نظير `S10` حرفياً، والخانة المحجوزة له `B11`
const MAX_PAGES = 100;

// ‏**لا ترشيح لـ`isActive` هنا بقرار** (2026-09-23): الهوك **مصدر بيانات أمين**
// يخدم أي مستهلك لاحق، وشاشة إدارة الفروع المستقبلية تحتاج رؤية المعطَّل بالضبط.
// والترشيح خاصّ بالعرض في `<BranchPicker>` وحده، فيبقى في طبقة العرض لا الجلب —
// وهو المنطق نفسه المطبَّق على `isPostable` سلفاً. يحرسه `B14`
async function fetchAllBranches(): Promise<BranchItem[]> {
  const branches: BranchItem[] = [];

  for (let pageNumber = 1; pageNumber <= MAX_PAGES; pageNumber += 1) {
    // ‏`PageNumber`/`PageSize` بحرفهما الكبير كما في العقد — مقيس من معاملات
    // ‏`GET /api/branches`، وهما **كل** ما يقبله (لا بحث ولا مرشِّح شركة).
    // ونطاق الشركة والفرع المخصَّص يفرضهما الخادم بنيوياً منذ `00c84a4`
    // (الحرّاس `K25`–`K28`)، فترشيحٌ ثانٍ هنا يُنشئ مصدر حقيقة ثانياً — تمنعه
    // ‏R-API-05 و R-CUR-03. يحرسه `B10`
    const response = await apiFetch(
      `/api/branches?PageNumber=${pageNumber}&PageSize=${PAGE_SIZE}`);

    if (!response.ok) {
      throw new ApiError(await readFailure(response, `تعذّر جلب الفروع (${response.status}).`));
    }

    const body = (await response.json()) as BranchesEnvelope;
    const page = body.data ?? undefined;

    branches.push(...(page?.data ?? []));

    // ‏`!== true` لا `=== false`: الحقل اختياري في العقد، وغيابه يعني **لا مزيد**
    if (page?.hasNextPage !== true) {
      return branches;
    }
  }

  throw new Error(`تجاوز تحميل الفروع ${MAX_PAGES} صفحة.`);
}

export function useAllBranches(): UseAllBranchesResult {
  // ‏لا استعلام قبل رمز صالح — نظير `L08`. وعبر `useAuth()` لا بقراءة المخزن
  // مباشرةً: المخزن وحدة مفردة **غير تفاعلية**، فقراءته تُبقي الاستعلام معطَّلاً
  // بعد تسجيل الدخول حتى يقع تصيير لسبب آخر
  const { status } = useAuth();

  const query = useQuery({
    queryKey: ["branches", "all"],
    queryFn: fetchAllBranches,
    enabled: status === "authenticated",

    // ‏**البيات يُعلَن ولا يُورَث** (قرار `QC`): الفروع بيانات مرجعية بطيئة التغيّر،
    // وثمن إعادة جلبها **حلقة صفحات** لا نداء واحد — فتُعلن بياتها صراحةً
    staleTime: MASTER_DATA_STALE_TIME
  });

  return {
    status: query.status,
    branches: query.data ?? [],
    error: query.error
  };
}
