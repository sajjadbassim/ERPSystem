import { z } from "zod";

import type { components } from "../../api-types/schema";
import { LOCKED_RATE } from "../components/money/MoneyInput";
import type { MoneyInputSide, MoneyInputValue } from "../components/money/MoneyInput";
import { journalEntryHeaderSchema } from "./journal-entry-header-schema";

type JournalLineCreateDto = components["schemas"]["JournalLineCreateDto"];

// ‏**شكل السطر محلياً لا شكله على السلك:** المبلغ وجانبه وسعره وتاريخه كتلة واحدة هي
// قيمة `<MoneyInput>` بعينها (`satisfies MoneyInputValue`)، فلا ترجمة بين المكوّن
// والنموذج. والتوزيع على `debitFC`/`creditFC` في `toLinesRequest` وحدها.
//
// ‏مصدر كل قيد مقيس:
//   `accountId` · `currencyId` uuid · `exchangeRateDate` date · وصف ≤ 500 ⟸ العقد
//       (`openapi.json:2913`)
//   سطران على الأقل  ⟸ **الإجراء لا العقد**: 50017. والعقد بلا `minItems` بقصد
//       (`JournalEntryDtos.cs:60` — العدد يفحصه الإجراء وحده)
//
// ‏**ولا فحص إيجابية للمبلغ والسعر هنا:** `<MoneyInput>` يعرضه للتجربة، والخادم يفرضه
// ‏(50014، 50020). ونسخة ثالثة منه تنحرف بصمت.
//
// ‏**R-API-05:** هذا كله تحقق للتجربة، والمعتبر ما يفرضه الخادم
export const journalLineSchema = z.object({
  accountId: z.uuid("الحساب مطلوب."),
  currencyId: z.uuid("عملة السطر مطلوبة."),
  money: z.object({
    amountFC: z.string(),
    side: z.enum(["debit", "credit"]),
    exchangeRate: z.string(),
    exchangeRateDate: z.iso.date("تاريخ سعر الصرف مطلوب.")
  }) satisfies z.ZodType<MoneyInputValue>,
  description: z.string().max(500, "وصف السطر أطول من الحد المسموح.")
});

export type JournalLineValues = z.infer<typeof journalLineSchema>;

// ‏تقاطع لا `extend`: مخطط الرأس يحمل تحقّقاً على مستوى الكائن (50026)، فيبقى كما هو
// ويُضاف إليه السطر من جانبه
export const journalEntryFormSchema = journalEntryHeaderSchema.and(
  z.object({ lines: z.array(journalLineSchema).min(2, "القيد يتطلب سطرين على الأقل.") })
);

export type JournalEntryFormValues = z.infer<typeof journalEntryFormSchema>;

// ‏سطر فارغ بلا سعر مُختلَق: السعر يُدخل يدوياً (قرار `<MoneyInput>`)، والمقفل يفرضه
// المكوّن عند العرض وحدّ الإرسال عند الإرسال
export function newLine(side: MoneyInputSide): JournalLineValues {
  return { accountId: "", currencyId: "", money: { amountFC: "", side, exchangeRate: "", exchangeRateDate: "" }, description: "" };
}

// ‏قرار V08: **مقارنة لا قرار** — عملة السطر تساوي عملة الدفاتر المعروفة. و`null`
// ‏«لم تُعرف الشركة بعد» فلا قفل مخمَّن، والخادم يفرض 50012 على أي حال.
//
// ‏**موضعها الوحيد:** يستهلكها السطر (خاصية `isRateLocked`) وحدّ الإرسال معاً، فلا
// يفترق ما يُعرض عمّا يُرسل
export function isRateLocked(currencyId: string, baseCurrencyId: string | null): boolean {
  return baseCurrencyId !== null && currencyId === baseCurrencyId;
}

// ‏حدّ الإرسال. **نقل قيمة لا حساب (R-API-01):** الجانب يختار المفتاح، والمبلغ يعبر
// حرفياً بلا تقريب (R-AMT-07). والجانب الآخر **يُحذف لا يُصفَّر** — غيابه صفر عند
// الخادم (`JournalEntryDtos.cs:24-25`)، و`"0"` من هنا قيمة مُختلَقة.
//
// ‏والسعر المقفل يُفرض هنا أيضاً: سطرٌ بُدّلت عملته إلى عملة الدفاتر يعرض 1 ولم يُصدر
// ‏`<MoneyInput>` شيئاً بعد، فيبقى في القيم سعرٌ سابق (`JL07` — نظير `V16`)
export function toLinesRequest(lines: readonly JournalLineValues[], baseCurrencyId: string | null): JournalLineCreateDto[] {
  return lines.map((line) => {
    const amount = line.money.side === "debit" ? { debitFC: line.money.amountFC } : { creditFC: line.money.amountFC };

    return {
      accountId: line.accountId,
      currencyId: line.currencyId,
      exchangeRate: isRateLocked(line.currencyId, baseCurrencyId) ? LOCKED_RATE : line.money.exchangeRate,
      exchangeRateDate: line.money.exchangeRateDate,
      description: line.description === "" ? null : line.description,
      ...amount
    };
  });
}
