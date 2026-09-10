import type { components } from "../../api-types/schema";

// ‏الغلاف الموحَّد لكل استجابات الفشل التي **يصوغها التطبيق** — وأي `ApiResponseOfX`
// يشترك في الحقول الأربعة، فيُؤخَذ أحدها ممثّلاً بدل كتابة النوع بيد (R-API-04)
type FailureEnvelope = components["schemas"]["ApiResponseOfTokenPairDto"];

export type ApiFailure = {
  // ‏`null` = **لا استجابة أصلاً** (فشل نقل)، وهو غير أي رمز حالة. ودمجهما في صفر
  // كان سيجعل «لم يصل الطلب» يبدو رمز حالة لا وجود له
  status: number | null;

  message: string;
  traceId: string | null;
};

// ‏الحدّ عند 500، والفصل دلاليّ لا اصطلاحيّ: ما دون الخمسمئة **جواب مقصود** من
// الخادم عن طلب المستخدم — بياناته خاطئة، أو جلسته منتهية، أو لا صلاحية له —
// وكلها يصحّحها بنفسه ولا يبلّغ عنها أحداً. وما فوقها **إقرار بأن الخادم أخفق**،
// وعندها وحدها يصير رقم التتبّع مفيداً لأن ثمّة بلاغاً سيُرفع.
//
// ‏وفشل النقل (`status === null`) في الجهة نفسها: ليس من صنع المستخدم.
export function isServerFault(failure: ApiFailure): boolean {
  return failure.status === null || failure.status >= 500;
}

// ‏يُقرأ الغلاف إن وُجد، ويُسقَط إلى نصّنا إن لم يوجد.
//
// ⚠ **والسقوط ليس حالة نادرة بل الأشيع:** لا `OnChallenge` مخصَّص في `ErpApi`
// (مقيس بالكنس)، فـ401 الصادر عن `SetFallbackPolicy` يعود **بجسم فارغ** — بلا
// غلاف ولا رسالة ولا `traceId`. ومثله 502 من الوكيل. فقراءة الجسم بلا حارس كانت
// ترمي `SyntaxError` **من داخل معالج الخطأ نفسه**، فيضيع الخطأ الأصلي معه.
export async function readFailure(response: Response, fallback: string): Promise<ApiFailure> {
  try {
    const body = (await response.json()) as FailureEnvelope;

    return {
      status: response.status,

      // ‏رسالة الخادم أولى **متى وُجدت**: هي أدقّ من نصّنا بحكم موضعها، وهي عربية
      // بضمان بند 8.4 في الباك-إند. ونصّنا احتياط لا أصل
      message: body.message ?? fallback,
      traceId: body.traceId ?? null
    };
  } catch {
    return { status: response.status, message: fallback, traceId: null };
  }
}

// ‏خطأ يحمل الفشل كاملاً عبر قناة أخطاء TanStack Query. و`Error` أصلاً لا `Error`
// بحقل ملحق: المكتبة تتعامل مع `Error` وتعرض `message`، فيبقى السلوك مفهوماً
// لمن يقرأ `query.error` بلا معرفة بهذا النوع
export class ApiError extends Error {
  readonly failure: ApiFailure;

  constructor(failure: ApiFailure) {
    super(failure.message);
    this.name = "ApiError";
    this.failure = failure;
  }
}

// ‏**قابل لإعادة المحاولة = عطل عابر محتمل، لا كل عطل خادم.**
//
// ‏ولا تُستعمل `isServerFault` هنا رغم قربها: هي تشمل `status === null`، وعندنا
// معنيان مختلفان لهذه القيمة — فشل نقل (عابر، يُعاد) وتجاوز سقف الصفحات (حتميّ،
// وإعادته تكرّر مئة نداء بلا أمل). فالدمج كان سيجعل السقف يُقصف مرتين.
//
// ‏و`TypeError` هو ما يرفض به `fetch` عند انقطاع الشبكة — فهو توقيع فشل النقل،
// ولا يمرّ بـ`ApiError` أصلاً لأنه يقع قبل أن تكون هناك استجابة تُقرأ.
export function isRetryableFailure(error: unknown): boolean {
  if (error instanceof ApiError) {
    return error.failure.status !== null && error.failure.status >= 500;
  }

  return error instanceof TypeError;
}

// ‏رقم التتبّع كما يُعرض — أو `null` إن لم يكن ليُعرض. مُجمَّعة هنا لا في كل شاشة
// كي لا تنحرف القاعدة بين موضعين: إحداهما تُظهره في 401 والأخرى لا.
//
// ‏**والشرط الثاني مقيس لا مفترَض:** `ExceptionHandlingMiddleware.cs:41` يبني رسالة
// الخمسمئة هكذا: «… الرجاء ذكر الرقم التالي عند التواصل مع الدعم: {traceId}».
// أي أن الخادم **يضع الرقم في متن رسالته أصلاً**. فإلحاقه ثانيةً يعرضه مرتين
// بصيغتين، ومن يرى رقمين متطابقين بلافتتين مختلفتين يشكّ في أيّهما يُبلِّغ.
//
// ‏والفحص بالاحتواء لا بمطابقة صيغة الرسالة: الصيغة نصّ في الباك-إند قد يُعاد
// تحريره، والرقم نفسه هو الثابت الذي يُقارَن به.
export function traceIdToShow(failure: ApiFailure): string | null {
  if (!isServerFault(failure) || failure.traceId === null) {
    return null;
  }

  return failure.message.includes(failure.traceId) ? null : failure.traceId;
}
