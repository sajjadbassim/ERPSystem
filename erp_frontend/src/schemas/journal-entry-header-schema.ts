import { z } from "zod";

import type { components } from "../../api-types/schema";

type PostJournalEntryRequestDto = components["schemas"]["PostJournalEntryRequestDto"];

// ‏الرأس وحده: السطور تُبنى في جولتها، وتنضمّ عند حدّ الإرسال نفسه
export type JournalEntryHeaderRequest = Omit<PostJournalEntryRequestDto, "lines">;

// ‏قرار 2026-09-26: القيد من هذه الشاشة يدويّ دائماً، ولا يُعرض مصدره حقلاً. و`satisfies`
// تربط الرقم بالتعداد في العقد، فرقمٌ خارجه يكسر الترجمة
const MANUAL_SOURCE_MODULE = 1 satisfies components["schemas"]["JournalSourceModule"];

// ‏يطابق `LTRIM(RTRIM(@Description))` في الإجراء: المسافة وحدها تُقصّ، لا كل بياض.
// و`trim()` كانت ستقصّ الجدولة والسطر الجديد فترفض ما يقبله الخادم (`JH04`)
function trimSpaces(value: string): string {
  return value.replace(/^ +| +$/gu, "");
}

// ‏مصدر كل قيد مقيس، ومصدران لا واحد:
//   `branchId` uuid · `postingDate` date · وصف ≤ 500   ⟸ العقد (`openapi.json:3395`)
//   وصف ≥ 5 بعد قصّ المسافات                            ⟸ **الإجراء المخزَّن لا العقد**:
//       ‏`usp_JournalEntry_Post` يرمي **50025** حين `@SourceModule = 1` والوصف أقصر.
//       والعقد يُعلن الوصف اختيارياً لأنه كذلك لغير اليدويّ — فغيابه من `openapi.json`
//       لا يعني أنه مُخترَع هنا
//   تاريخ المستند لا يتأخر عن تاريخ الترحيل            ⟸ **الإجراء المخزَّن**: 50026
//
// ‏**R-API-05:** هذا كله تحقق للتجربة، والمعتبر ما يفرضه الخادم — 50025 و50026 هناك.
//
// ‏و`satisfies` تربط الحقول **المطلوبة** بالعقد عند الترجمة. أما الاختيارية فلا تكشف
// ‏`satisfies` تحريف اسمها، ويكشفه فحص الخصائص الزائدة في `toHeaderRequest` أدناه
export const journalEntryHeaderSchema = z
  .object({
    branchId: z.uuid("الفرع مطلوب."),
    postingDate: z.iso.date("تاريخ الترحيل مطلوب."),

    // ‏الفراغ «لم يُحدَّد» لا تاريخ مشوَّه: قيمة الحقل `type="date"` الفارغ نصّ فارغ
    documentDate: z.union([z.literal(""), z.iso.date()], "تاريخ المستند غير صالح."),

    description: z
      .string()
      .max(500, "وصف القيد أطول من الحد المسموح.")
      .refine((value) => trimSpaces(value).length >= 5, "القيد اليدوي يتطلب وصفاً لا يقل عن خمسة أحرف.")
  })
  // ‏مقارنة نصّين بصيغة `YYYY-MM-DD` مقارنةُ تاريخين، بلا تحويل إلى `Date` ومنطقته الزمنية
  .refine((header) => header.documentDate === "" || header.documentDate <= header.postingDate, {
    path: ["documentDate"],
    error: "تاريخ المستند لا يجوز أن يتأخر عن تاريخ الترحيل."
  }) satisfies z.ZodType<Omit<PostJournalEntryRequestDto, "lines" | "sourceModule">>;

export type JournalEntryHeaderValues = z.infer<typeof journalEntryHeaderSchema>;

// ‏حدّ الإرسال. **يُبنى حقلاً حقلاً لا بالنشر:** `{ ...values }` كان سيحمل أي مفتاح
// مدسوس في القيم إلى الحمولة، و`sourceModule` منها (`JH08`).
//
// ‏والتحويل هنا لا في المخطط: `<ZodForm>` يشترط مدخل المخطط = مخرَجه (`TS2322`)، فلا
// ‏`transform` فيه
export function toHeaderRequest(values: JournalEntryHeaderValues): JournalEntryHeaderRequest {
  return {
    branchId: values.branchId,
    postingDate: values.postingDate,

    // ‏`null` فيُسقطه **الخادم** إلى تاريخ الترحيل (`JournalEntryService.cs:112`) —
    // وإسقاطه هنا نسخة ثانية من قاعدته
    documentDate: values.documentDate === "" ? null : values.documentDate,

    // ‏حرفياً بلا قصّ: القصّ أعلاه للفحص وحده، كما في الإجراء
    description: values.description,
    sourceModule: MANUAL_SOURCE_MODULE
  };
}
