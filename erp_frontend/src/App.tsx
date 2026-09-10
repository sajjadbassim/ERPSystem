import { useQuery } from "@tanstack/react-query";

import type { components } from "../api-types/schema";
import { ApiError, readFailure, traceIdToShow } from "./api/api-error";
import { apiFetch } from "./api/http";
import { MASTER_DATA_STALE_TIME } from "./api/query-config";
import { useAuth } from "./auth/AuthProvider";
import { LoginScreen } from "./auth/LoginScreen";

type BranchesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfBranchResponseDto"];
type Branch = components["schemas"]["BranchResponseDto"];

// ‏عرض الفشل: الرسالة، ورقم التتبّع **إن كان عطل خادم**. وهو ثاني مستهلك لهذه
// القاعدة بعد `<LoginScreen>` — ولم يُستخرج مكوّناً مشتركاً بقصد: القاعدة نفسها
// مستخرَجة أصلاً في `traceIdToShow`، والمتبقّي **شكل** لا منطق. وتعميم الشكل
// بمستهلكَين تجريدٌ على محور مجهول (قاعدة `symbol ?? code` نفسها)
function BranchesError({ error }: { error: Error }) {
  const failure = error instanceof ApiError ? error.failure : null;
  const traceId = failure === null ? null : traceIdToShow(failure);

  return (
    <p role="alert">
      {error.message}
      {traceId === null ? null : <span>{` رقم التتبّع: ${traceId}`}</span>}
    </p>
  );
}

// ‏أصغر شاشة محمية ممكنة — وُجدت لغرض واحد: أن يكون في التطبيق **نداء حيّ إلى نقطة
// محمية**، فيُقاس أن الرمز يُرفَق ويُقبَل من طرف إلى طرف. وليست «شاشة فروع»: لا
// إنشاء ولا تعديل ولا ترقيم صفحات — تلك من نصيب §4.5 بمنتقياتها.
function ProtectedHome() {
  const query = useQuery<Branch[]>({
    queryKey: ["branches"],

    // ‏الفروع بيانات مرجعية كشجرة الحسابات — تُعلن بياتها صراحةً (قرار `QC`)
    staleTime: MASTER_DATA_STALE_TIME,

    queryFn: async () => {
      const response = await apiFetch("/api/branches");

      // ‏الرمي لا السقوط إلى قائمة فارغة: 401 يبدو «لا فروع» وهو انعدام جلسة —
      // نمط الفشل نفسه الذي وُجدت `Q06` له
      if (!response.ok) {
        // ‏رسالة الخادم لا رسالتنا (الدَّين ٦): كان هنا `تعذّر جلب الفروع (500).` —
        // نصّ من تأليفنا بالرمز بينما الاستجابة تحمل ما هو أدقّ منها ورقمَ تتبّعها
        throw new ApiError(await readFailure(response, `تعذّر جلب الفروع (${response.status}).`));
      }

      const body = (await response.json()) as BranchesEnvelope;

      // ‏فكّ الطبقات الثلاث القابلة للعدم كما في `useAccounts`: الغلاف والصفحة
      // ومصفوفتها، ولا حقل منها إلزامي في العقد
      return body.data?.data ?? [];
    }
  });

  return (
    <>
      <h1>نظام ERP — الواجهة</h1>

      {query.isPending ? <p>جارٍ التحميل…</p> : null}

      {query.isError ? <BranchesError error={query.error} /> : null}

      <ul>
        {(query.data ?? []).map((branch) => (
          <li key={branch.id}>{`${branch.code} — ${branch.name}`}</li>
        ))}
      </ul>
    </>
  );
}

// ‏البوابة **منع عرض لا إعادة توجيه**: لا موجّه في المشروع (مقيس: صفر تبعية توجيه
// في `package.json`). فشاشة الدخول لا «تُفتَح» أصلاً وهناك جلسة قائمة — لا تُعرض.
// ‏ومصدر القرار `status` من المزوّد وحده، لا فحص للمخزن هنا: نسخةٌ ثانية من الشرط
// كانت ستنحرف عن الأولى. يحرسه `LGN06`
export function App() {
  const { status } = useAuth();

  return status === "authenticated" ? <ProtectedHome /> : <LoginScreen />;
}
