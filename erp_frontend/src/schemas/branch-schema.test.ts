import { describe, expect, it } from "vitest";

import { branchCreateSchema } from "./branch-schema";

// ‏مصفوفة الحالات Z (مخططات التحقق).
//
// كل قيد هنا **مصدره مقيس**، ولا قيد بلا مصدر:
//   `companyId` uuid  ⟸ العقد: `format: "uuid"`
//   `code`  ≤ 20      ⟸ العقد: `maxLength: 20`
//   `name`  ≤ 200     ⟸ العقد: `maxLength: 200`
//   غير فارغين        ⟸ **ليس في العقد** — بل `[Required]` في `BranchDtos.cs:11,15`،
//                        و`AllowEmptyStrings = false` افتراضاً يرفض `""`
//
// و**R-API-05**: هذا تحقق للتجربة وحدها، والمعتبر ما يفرضه الخادم.

const VALID = {
  companyId: "0199a1f0-0000-7000-8000-000000000001",
  code: "BR1",
  name: "الفرع الرئيسي"
};

describe("branchCreateSchema — Z (مطابق لعقد BranchCreateDto)", () => {
  // ‏⚠ هذه الحالة **لا تصلح حمرةً أولى**: أي مخطط متساهل يُرضيها، وقد أرضاها الجسم
  // غير المنفَّذ فعلاً. قيمتها **حراسة انحدار** لا إثبات تنفيذ — والادعاء الحقيقي
  // تحمله `Z02` و`Z03`.
  it("Z01: حمولة صالحة تمرّ", () => {
    expect(branchCreateSchema.safeParse(VALID).success).toBe(true);
  });

  it("Z02: معرّف شركة ليس uuid ورمز فارغ ⟵ خطآن بنوعيهما", () => {
    const result = branchCreateSchema.safeParse({ companyId: "x", code: "", name: "ن" });

    expect(result.success).toBe(false);

    // ‏الفحص بـ**نوع الخطأ ومساره** لا بنصّ الرسالة: النصّ يتغيّر مع نسخة zod،
    // والنوع جزء من عقدها. نظير «الأخطاء تُفحص بالرقم لا بالنص» في الباك-إند
    const issues = (result.error?.issues ?? []).map((issue) => `${issue.path.join(".")}:${issue.code}`);

    expect(issues).toContain("companyId:invalid_format");
    expect(issues).toContain("code:too_small");
  });

  it("Z03: رمز أطول من 20 محرفاً يُرفض (maxLength في العقد)", () => {
    const result = branchCreateSchema.safeParse({ ...VALID, code: "A".repeat(21) });

    expect(result.success).toBe(false);

    const issues = (result.error?.issues ?? []).map((issue) => `${issue.path.join(".")}:${issue.code}`);

    expect(issues).toContain("code:too_big");
  });
});
