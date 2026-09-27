import { useMutation } from "@tanstack/react-query";

import type { components } from "../../api-types/schema";
import { ApiError, readFailure } from "./api-error";
import { apiFetch } from "./http";

export type PostJournalEntryRequest = components["schemas"]["PostJournalEntryRequestDto"];

type PostedEnvelope = components["schemas"]["ApiResponseOfJournalEntryResponseDto"];

export type PostedJournalEntry = {
  // ‏رسالة الخادم بحرفها («تم ترحيل القيد بنجاح») — لا نصّ من تأليف الواجهة
  message: string | null;
  documentNumber: string;
};

// ‏**R-API-06:** نداء واحد يحمل الرأس والسطور، والخادم يُنشئ القيد وسطوره في معاملة
// واحدة. لا «أنشئ رأساً ثم أضف سطوراً».
//
// ‏و`response.ok` لا رمز بعينه: المتحكّم يُرجع **201** (`CreatedAtAction`) بينما
// ‏`openapi.json` يُعلن **200** — انحراف عقد مسجَّل. فالفحص على النجاح لا على رقمه.
//
// ‏والرفض (50001 وأخواته) يصل **400 برسالة الإجراء بلا رقم** (`SqlErrorTranslator`)،
// ‏فيُحمَل كما هو في `ApiError` ويُعرض بحرفه. ولا توازن ولا مجموع هنا (R-API-01)
async function postJournalEntry(request: PostJournalEntryRequest): Promise<PostedJournalEntry> {
  const response = await apiFetch("/api/journal-entries", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });

  if (!response.ok) {
    throw new ApiError(await readFailure(response, `تعذّر ترحيل القيد (${response.status}).`));
  }

  const body = (await response.json()) as PostedEnvelope;

  if (body.data === null || body.data === undefined) {
    throw new Error("استجابة الترحيل بلا بيانات.");
  }

  return { message: body.message ?? null, documentNumber: body.data.documentNumber };
}

// ‏**بلا `retry` — قرار الطفرات الدائم (`query-config.ts`)، والافتراض في المكتبة صفر.**
// فشل الشبكة لا يميّز «لم يصل» عن «وصل ونُفِّذ وضاع الرد»، وإعادة الثانية قيدٌ ثانٍ برقم
// مستند ثانٍ. فالإعادة قرار المستخدم. يحرسه `JE18`
export function usePostJournalEntry() {
  return useMutation({ mutationFn: postJournalEntry });
}
