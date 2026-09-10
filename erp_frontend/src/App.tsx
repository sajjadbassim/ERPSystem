import { useQuery } from "@tanstack/react-query";

import type { components } from "../api-types/schema";
import { apiFetch } from "./api/http";
import { useAuth } from "./auth/AuthProvider";
import { LoginScreen } from "./auth/LoginScreen";

type BranchesEnvelope = components["schemas"]["ApiResponseOfPagedResponseOfBranchResponseDto"];
type Branch = components["schemas"]["BranchResponseDto"];

// ‏أصغر شاشة محمية ممكنة — وُجدت لغرض واحد: أن يكون في التطبيق **نداء حيّ إلى نقطة
// محمية**، فيُقاس أن الرمز يُرفَق ويُقبَل من طرف إلى طرف. وليست «شاشة فروع»: لا
// إنشاء ولا تعديل ولا ترقيم صفحات — تلك من نصيب §4.5 بمنتقياتها.
function ProtectedHome() {
  const query = useQuery<Branch[]>({
    queryKey: ["branches"],
    queryFn: async () => {
      const response = await apiFetch("/api/branches");

      // ‏الرمي لا السقوط إلى قائمة فارغة: 401 يبدو «لا فروع» وهو انعدام جلسة —
      // نمط الفشل نفسه الذي وُجدت `Q06` له
      if (!response.ok) {
        throw new Error(`تعذّر جلب الفروع (${response.status}).`);
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

      {query.isError ? <p role="alert">{query.error.message}</p> : null}

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
