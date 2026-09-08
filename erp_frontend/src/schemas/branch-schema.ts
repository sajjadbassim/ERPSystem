import { z } from "zod";

import type { components } from "../../api-types/schema";

type BranchCreateDto = components["schemas"]["BranchCreateDto"];

// ‏كل قيد هنا **مصدره مقيس**، ولا قيد بلا مصدر:
//   `companyId` uuid  ⟸ العقد: `format: "uuid"`
//   `code`  ≤ 20      ⟸ العقد: `maxLength: 20`
//   `name`  ≤ 200     ⟸ العقد: `maxLength: 200`
//   غير فارغين        ⟸ **ليس في العقد** — بل `[Required]` في `BranchDtos.cs:11,15`،
//                        و`AllowEmptyStrings = false` افتراضاً يرفض `""`
//
// ‏**R-API-05:** تحقق للتجربة وحده، والمعتبر ما يفرضه الخادم. ولهذا لا يُوسَّع هذا
// المخطط بقاعدة مالية: تكرارها هنا يُنشئ نسخة ثانية تنحرف بصمت.
//
// ‏و`satisfies` تربطه بالعقد **عند الترجمة**: انحراف حرف واحد في اسم حقل يُخرج
// ‏`TS1360` فيكسر البناء — مُثبَت بعطل مؤقت. حارسه `npm run typecheck` داخل
// ‏`npm run build`، لا `npm test` (R-API-04).
export const branchCreateSchema = z.object({
  companyId: z.uuid("معرّف الشركة غير صالح."),
  code: z.string().min(1, "رمز الفرع مطلوب.").max(20, "رمز الفرع أطول من الحد المسموح."),
  name: z.string().min(1, "اسم الفرع مطلوب.").max(200, "اسم الفرع أطول من الحد المسموح.")
}) satisfies z.ZodType<BranchCreateDto>;
