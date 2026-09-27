import { describe, expect, it } from "vitest";

import { isRateLocked, journalEntryFormSchema, journalLineSchema, newLine, toLinesRequest } from "./journal-line-schema";
import type { JournalLineValues } from "./journal-line-schema";

// ‏مصفوفة الحالات `JL` (سطر القيد: العقد المحليّ وحدّ الإرسال).
//
// ‏البادئة `JL` لملف جديد (بادئة ↔ ملف) — لا توسيع لـ`JH` ولا `Z`.
//
// ‏مصدر كل قيد مقيس:
//   `accountId` · `currencyId` uuid · `exchangeRateDate` date · وصف ≤ 500 ⟸ العقد (`openapi.json:2913`)
//   سطران على الأقل                                                 ⟸ الإجراء (50017)
//   سعر عملة الدفاتر واحد                                            ⟸ الإجراء (50012)
//   `debitFC`/`creditFC` اختياريان، والغائب صفر عند الخادم              ⟸ `JournalEntryDtos.cs:24-25`
//
// ‏و**R-API-05**: تحقق للتجربة وحدها، والمعتبر ما يفرضه الخادم.

const BASE = "0199a1f0-0000-7000-8000-0000000000e1";
const FOREIGN = "0199a1f0-0000-7000-8000-0000000000e2";
const ACCOUNT = "0199a1f0-0000-7000-8000-0000000000a1";

const LINE: JournalLineValues = {
  accountId: ACCOUNT,
  currencyId: FOREIGN,
  money: { amountFC: "1000.50", side: "debit", exchangeRate: "1320", exchangeRateDate: "2026-09-26" },
  description: "دفعة مورد"
};

const HEADER = {
  branchId: "0199a1f0-0000-7000-8000-0000000000b1",
  postingDate: "2026-09-26",
  documentDate: "",
  description: "قيد تسوية"
};

function issuesOf(values: unknown, schema: { safeParse: (v: unknown) => { error?: { issues: { path: PropertyKey[]; code: string }[] } } } = journalLineSchema): string[] {
  return (schema.safeParse(values).error?.issues ?? []).map((issue) => `${issue.path.join(".")}:${issue.code}`);
}

describe("journalLineSchema — JL (سطر القيد)", () => {
  // ‏⚠ لا تصلح حمرةً أولى — حراسة انحدار (نظير `Z01`)
  it("JL01: سطر صالح يمرّ", () => {
    expect(issuesOf(LINE)).toEqual([]);
  });

  it("JL02: حساب فارغ وعملة فارغة وتاريخ سعر فارغ ⟵ ثلاثة أخطاء بمساراتها", () => {
    const issues = issuesOf({ ...LINE, accountId: "", currencyId: "", money: { ...LINE.money, exchangeRateDate: "" } });

    expect(issues).toContain("accountId:invalid_format");
    expect(issues).toContain("currencyId:invalid_format");
    expect(issues).toContain("money.exchangeRateDate:invalid_format");
  });

  it("JL03: وصف سطر أطول من 500 يُرفض، والفارغ يمرّ (اختياري)", () => {
    expect(issuesOf({ ...LINE, description: "أ".repeat(501) })).toContain("description:too_big");
    expect(issuesOf({ ...LINE, description: "" })).toEqual([]);
  });

  it("JL04: القيد بسطر واحد يُرفض على «lines»، وبسطرين يمرّ", () => {
    expect(issuesOf({ ...HEADER, lines: [LINE] }, journalEntryFormSchema)).toContain("lines:too_small");
    expect(issuesOf({ ...HEADER, lines: [LINE, { ...LINE, money: { ...LINE.money, side: "credit" } }] }, journalEntryFormSchema)).toEqual([]);
  });
});

describe("isRateLocked — JL (قفل السعر لكل سطر)", () => {
  // ‏الادعاء: القفل **مقارنة لا قرار** — عملة السطر تساوي عملة الدفاتر المعروفة.
  // و`null` تعني «الشركة لم تُعرف بعد»: فلا قفل مخمَّن، والخادم يفرض 50012 على أي حال
  it("JL05: مقفل عند التساوي وحده — لا عند الاختلاف، ولا قبل معرفة عملة الدفاتر، ولا بلا عملة", () => {
    expect(isRateLocked(BASE, BASE)).toBe(true);
    expect(isRateLocked(FOREIGN, BASE)).toBe(false);
    expect(isRateLocked(BASE, null)).toBe(false);
    expect(isRateLocked("", BASE)).toBe(false);
  });
});

describe("toLinesRequest — JL (حدّ الإرسال)", () => {
  // ‏**نقل قيمة لا حساب**: الجانب يقرّر المفتاح وحده، والمبلغ يعبر حرفياً. والجانب
  // الآخر **يُحذف لا يُصفَّر**: غيابه صفر عند الخادم، و`"0"` من المتصفح قيمة مُختلَقة
  it("JL06: المدين ⟵ debitFC وحده، والدائن ⟵ creditFC وحده، والمبلغ حرفياً", () => {
    const [debit, credit] = toLinesRequest(
      [LINE, { ...LINE, money: { ...LINE.money, side: "credit", amountFC: "0.0001" } }],
      BASE);

    expect(debit).toEqual({
      accountId: ACCOUNT,
      currencyId: FOREIGN,
      exchangeRate: "1320",
      exchangeRateDate: "2026-09-26",
      description: "دفعة مورد",
      debitFC: "1000.50"
    });

    expect(credit).toHaveProperty("creditFC", "0.0001");
    expect(credit).not.toHaveProperty("debitFC");
  });

  // ‏نظير `V16` على حدّ الإرسال: سطرٌ اختيرت له عملة أجنبية وسعرها، ثم بُدّلت إلى
  // عملة الدفاتر — `<MoneyInput>` **يعرض** 1 ولم يُصدر شيئاً بعد. فلو أُرسل المخزَّن
  // لصار المعروض غير المُرسَل. والقاعدة من `isRateLocked` نفسها لا نسخة منها
  it("JL07: سطر عملة الدفاتر يُرسَل بسعر 1 ولو بقي في القيم سعر سابق", () => {
    const [line] = toLinesRequest([{ ...LINE, currencyId: BASE }], BASE);

    expect(line?.exchangeRate).toBe("1");
  });

  it("JL08: السطر غير المقفل يُرسَل بسعره حرفياً — ومنه ما قبل معرفة عملة الدفاتر", () => {
    expect(toLinesRequest([LINE], BASE)[0]?.exchangeRate).toBe("1320");
    expect(toLinesRequest([{ ...LINE, currencyId: BASE }], null)[0]?.exchangeRate).toBe("1320");
  });

  it("JL09: وصف السطر الفارغ ⟵ null، والترتيب محفوظ", () => {
    const lines = toLinesRequest([{ ...LINE, description: "" }, { ...LINE, accountId: FOREIGN }], BASE);

    expect(lines[0]?.description).toBeNull();
    expect(lines.map((line) => line.accountId)).toEqual([ACCOUNT, FOREIGN]);
  });

  it("JL10: السطران الافتراضيان مدين ثم دائن، فارغان بلا سعر مُختلَق", () => {
    expect([newLine("debit"), newLine("credit")]).toEqual([
      { accountId: "", currencyId: "", money: { amountFC: "", side: "debit", exchangeRate: "", exchangeRateDate: "" }, description: "" },
      { accountId: "", currencyId: "", money: { amountFC: "", side: "credit", exchangeRate: "", exchangeRateDate: "" }, description: "" }
    ]);
  });
});
