import { ApiError, traceIdToShow } from "../api/api-error";

// ‏عرض الفشل: الرسالة، ورقم التتبّع **إن كان عطل خادم** (قرار الدَّين ٦).
//
// ‏**استُخرج بشرطٍ كتبه المشروع لنفسه سلفاً** فوق `AccountsError` في
// ‏`AccountsScreen.tsx`: «ويُعاد النظر في استخراجه مكوّناً مشتركاً **إن ظهر رابع**».
// وقد ظهر الرابع والخامس معاً — `<BranchPicker>` (`B12`) و`<AccountPicker>` (`S11`).
// فتجاهل الشرط كان سيجعل التعليق حبراً، وهو نمط «قرار مُتَّخذ غير منفَّذ» الذي
// يرفضه هذا المشروع نصّاً.
//
// ‏**والمنطق ليس هنا:** قاعدة إظهار الرقم مستخرَجة سلفاً في `traceIdToShow`
// (`api-error.ts`)، وهذا **شكلٌ لا منطق**. فما مُنع تكراره هو الشكل، لا القاعدة —
// وخمس نسخ من `<p role="alert">` كانت ستنحرف إحداها يوماً بلافتة أو بترتيب مختلف،
// فيرى المستخدم العطل نفسه بصيغتين.
//
// ‏و`Error` عاديّ لا `ApiError` في التوقيع بقصد: TanStack Query يُسلّم `Error`،
// وفشل النقل (`TypeError`) لا يمرّ بـ`ApiError` أصلاً (`api-error.ts:69`). فاشتراط
// النوع الأضيق كان سيُجبر كل مستدعٍ على فحصٍ يسبق النداء — وهو الفحص الواقع هنا.
export function ApiErrorMessage({ error }: { error: Error }) {
  const failure = error instanceof ApiError ? error.failure : null;
  const traceId = failure === null ? null : traceIdToShow(failure);

  return (
    <p role="alert">
      {error.message}
      {traceId === null ? null : <span>{` رقم التتبّع: ${traceId}`}</span>}
    </p>
  );
}
