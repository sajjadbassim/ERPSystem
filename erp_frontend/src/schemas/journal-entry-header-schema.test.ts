import { describe, expect, it } from "vitest";

import { journalEntryHeaderSchema, toHeaderRequest } from "./journal-entry-header-schema";
import type { JournalEntryHeaderValues } from "./journal-entry-header-schema";

// ‏مصفوفة الحالات `JH` (رأس القيد: المخطط وحدّ الإرسال).
//
// ‏البادئة `JH` لا توسيعٌ لـ`Z`: `Z` ملف `branch-schema.test.ts`، والثابت «بادئة ↔ ملف»
// بلا استثناء. و`J` وحدها تُركت لأن `JE` بادئة الشاشة، فلا تتشابه البادئتان.
//
// ‏مصدر كل قيد مقيس، ومصدران لا واحد:
//   العقد (`openapi.json:3395`)  ⟸ `branchId` uuid · `postingDate` date · وصف ≤ 500
//   الإجراء المخزَّن             ⟸ وصف ≥ 5 بعد قصّ **المسافات** (50025)، وتاريخ المستند
//                                   لا يتأخر عن تاريخ الترحيل (50026)
//
// ‏و**R-API-05**: هذا تحقق للتجربة وحدها، والمعتبر ما يفرضه الخادم.

const VALID: JournalEntryHeaderValues = {
  branchId: "0199a1f0-0000-7000-8000-0000000000b1",
  postingDate: "2026-09-26",
  documentDate: "",
  description: "قيد تسوية"
};

function issuesOf(values: unknown): string[] {
  const result = journalEntryHeaderSchema.safeParse(values);

  return (result.error?.issues ?? []).map((issue) => `${issue.path.join(".")}:${issue.code}`);
}

describe("journalEntryHeaderSchema — JH (رأس القيد)", () => {
  // ‏⚠ **لا تصلح حمرةً أولى** — نظير `Z01`: أي مخطط متساهل يُرضيها. قيمتها حراسة انحدار
  it("JH01: رأس صالح يمرّ", () => {
    expect(journalEntryHeaderSchema.safeParse(VALID).success).toBe(true);
  });

  it("JH02: فرع فارغ وتاريخ ترحيل فارغ ⟵ خطآن بمساريهما", () => {
    const issues = issuesOf({ ...VALID, branchId: "", postingDate: "" });

    expect(issues).toContain("branchId:invalid_format");
    expect(issues).toContain("postingDate:invalid_format");
  });

  // ‏الحشو بالمسافات هو الكاشف: `"   ab c   "` طوله عشرة، وبعد القصّ أربعة. فتنفيذ
  // يعدّ الطول بلا قصّ يمرّره، والخادم يرفضه بـ50025
  it("JH03: وصف أقل من خمسة أحرف بعد قصّ المسافات يُرفض", () => {
    expect(issuesOf({ ...VALID, description: "   ab c   " })).toContain("description:custom");
  });

  // ‏الحدّ من الجهتين، وبقاعدة الخادم لا بقاعدة المتصفح: `LTRIM/RTRIM` تقصّ المسافة
  // وحدها، فالجدولة تُحسب حرفاً عنده. و`trim()` في JS كانت سترفض ما يقبله الخادم —
  // تحقق «للتجربة» أشدّ من المعتبر يمنع قيداً صحيحاً (R-API-05)
  it("JH04: خمسة أحرف بالضبط تمرّ — والجدولة حرفٌ كما في الإجراء", () => {
    expect(issuesOf({ ...VALID, description: "  abcde  " })).toEqual([]);
    expect(issuesOf({ ...VALID, description: "\tabcd" })).toEqual([]);
  });

  it("JH05: وصف أطول من 500 يُرفض (maxLength في العقد)", () => {
    expect(issuesOf({ ...VALID, description: "أ".repeat(501) })).toContain("description:too_big");
  });

  it("JH06: تاريخ مستند بعد تاريخ الترحيل يُرفض على حقله، ومساويه يمرّ", () => {
    expect(issuesOf({ ...VALID, documentDate: "2026-09-27" })).toContain("documentDate:custom");
    expect(issuesOf({ ...VALID, documentDate: "2026-09-26" })).toEqual([]);
  });

  it("JH07: تاريخ المستند اختياري — الفراغ يمرّ، والمشوَّه يُرفض", () => {
    expect(issuesOf({ ...VALID, documentDate: "" })).toEqual([]);
    expect(issuesOf({ ...VALID, documentDate: "26/09/2026" }).some((issue) => issue.startsWith("documentDate:"))).toBe(true);
  });
});

describe("toHeaderRequest — JH (حدّ الإرسال)", () => {
  // ‏الادعاء: `sourceModule` **يُفرض عند الحدّ ولا يُقرأ من القيم**. فقيمة مدسوسة
  // تحمل 4 (`ManualFxAdjustment` — المعفى من R-FX-07) لا تعبر. والمساواة التامة لا
  // الاحتواء: تثبت أن لا مفتاح دخيلاً يتسرّب إلى الحمولة أيضاً
  it("JH08: sourceModule يُرسَل 1 دائماً — ولو دُسّ غيره في القيم", () => {
    const tampered = { ...VALID, sourceModule: 4 } as unknown as JournalEntryHeaderValues;

    expect(toHeaderRequest(tampered)).toEqual({
      branchId: VALID.branchId,
      postingDate: VALID.postingDate,
      documentDate: null,
      description: VALID.description,
      sourceModule: 1
    });
  });

  // ‏الفراغ يُرسَل `null` فيُسقطه **الخادم** إلى تاريخ الترحيل (`JournalEntryService.cs:112`).
  // وإسقاطه هنا كان نسخة ثانية من القاعدة، و`""` يرفضه محلِّل `DateOnly?` بـ400
  it("JH09: تاريخ المستند الفارغ ⟵ null، والمملوء يمرّ، والوصف حرفياً بلا قصّ", () => {
    expect(toHeaderRequest(VALID).documentDate).toBeNull();

    const filled = toHeaderRequest({ ...VALID, documentDate: "2026-09-20", description: "  قيد تسوية  " });

    expect(filled.documentDate).toBe("2026-09-20");
    expect(filled.description).toBe("  قيد تسوية  ");
  });
});
