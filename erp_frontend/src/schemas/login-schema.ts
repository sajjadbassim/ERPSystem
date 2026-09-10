import { z } from "zod";

import type { components } from "../../api-types/schema";

// ‏النموذج يلتقط الإلزاميين وحدهما. و`companyCode` **متروك بقصد**: العقد يصفه
// ‏`null | string` اختيارياً، ولا يلزم إلا عند التباس اسم بين شركتين (الحارس `E07`
// في الباك-إند) — وحقلٌ يظهر لكل مستخدم ليُترك فارغاً دائماً كلفة بلا مقابل.
// ‏و`Pick` من نوع العقد لا كتابة يدوية: إعادة تسمية حقل في الخلفية تكسر الترجمة هنا
type LoginFormValues = Pick<components["schemas"]["LoginRequestDto"], "userName" | "password">;

// ‏كل قيد **مصدره مقيس**، ولا قيد بلا مصدر:
//   `userName` ≤ 50   ⟸ العقد: `maxLength: 50` (ومصدره `AuthDtos.cs:8`)
//   غير فارغين        ⟸ **ليس في العقد** — بل `[Required]` في `AuthDtos.cs:7,11`
//   `password` بلا حدّ أعلى ⟸ العقد **لا يعلن** له `maxLength`، ولا يُخترع واحد هنا
//
// ‏**R-API-05:** تحقق للتجربة وحده. والمعتبر ما يفرضه الخادم — فطول كلمة المرور
// وقواعد تعقيدها شأنه، ونسخها هنا كانت ستصير نسخة ثانية تنحرف بصمت.
export const loginSchema = z.object({
  userName: z
    .string()
    .min(1, "اسم المستخدم مطلوب.")
    .max(50, "اسم المستخدم أطول من الحد المسموح."),
  password: z.string().min(1, "كلمة المرور مطلوبة.")
}) satisfies z.ZodType<LoginFormValues>;
