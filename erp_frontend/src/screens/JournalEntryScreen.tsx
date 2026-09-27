import { useId } from "react";
import { useWatch } from "react-hook-form";

import { useAllBranches } from "../api/useAllBranches";
import { useCompany } from "../api/useCompany";
import type { UseCompanyResult } from "../api/useCompany";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { BranchPicker } from "../components/pickers/BranchPicker";
import { ControlledField, TextField, ZodForm } from "../forms/ZodForm";
import { journalEntryFormSchema, newLine } from "../schemas/journal-line-schema";
import type { JournalEntryFormValues } from "../schemas/journal-line-schema";
import { JournalEntryLines } from "./JournalEntryLines";

// ‏سطران لا سطر: الحدّ الأدنى الذي يقبله الخادم (50017). والجانبان مدين ثم دائن
const DEFAULT_VALUES: JournalEntryFormValues = {
  branchId: "",
  postingDate: "",
  documentDate: "",
  description: "",
  lines: [newLine("debit"), newLine("credit")]
};

function DateField(props: { name: "postingDate" | "documentDate"; label: string }) {
  const id = useId();

  return (
    <ControlledField<string>
      name={props.name}
      render={(field) => (
        <p>
          <label htmlFor={id}>{props.label}</label>
          <input id={id} type="date" value={field.value} onChange={(event) => field.onChange(event.target.value)} />
        </p>
      )}
    />
  );
}

// ‏الشركة **مشتقّة من الفرع المختار** لا من أول فرع ولا من الرمز (قرار V08). وقبل
// الاختيار `null` فلا نداء (`JE03`). و`useAllBranches` لا يُطلق نداءً ثانياً: مفتاح
// الاستعلام نفسه الذي يملأ `<BranchPicker>`. ومستهلكان (العرض والسطور) لا يضاعفان
// النداء: مفتاح `useCompany` واحد
function useSelectedCompany(): UseCompanyResult {
  const branchId = useWatch<JournalEntryFormValues, "branchId">({ name: "branchId" });
  const { branches } = useAllBranches();

  return useCompany(branches.find((branch) => branch.id === branchId)?.companyId ?? null);
}

function BaseCurrency() {
  const { status, company, error } = useSelectedCompany();

  if (status === "error" && error !== null) {
    return <ApiErrorMessage error={error} />;
  }

  // ‏R-RPT-04: الرمز من الخادم لا ثابتاً في الكود
  return company === null ? null : <p>{`عملة الدفاتر: ${company.baseCurrencyCode}`}</p>;
}

// ‏قفل السعر يحتاج **معرّف** عملة الدفاتر لا رمزها. و`null` قبل معرفة الشركة، فلا قفل
// مخمَّن (`JE11`)
function Lines() {
  const { company } = useSelectedCompany();

  return <JournalEntryLines baseCurrencyId={company?.baseCurrencyId ?? null} />;
}

// ‏**بلا إرسال — بقرار (2026-09-26)، وقائم في جولة السطور.** زرّ الترحيل ومعه
// ‏`toHeaderRequest`/`toLinesRequest` جولةٌ تالية. و`sourceModule` ليس هنا حقلاً
// ولا قيمة: يُفرض عند حدّ الإرسال وحده (`JE02`, `JH08`)
export function JournalEntryScreen() {
  return (
    <ZodForm schema={journalEntryFormSchema} defaultValues={DEFAULT_VALUES} onSubmit={() => {}}>
      <h1>قيد يومية</h1>

      <section aria-label="رأس القيد">
        {/* ‏النموذج يحمل `""` لا `null`: مدخل المخطط نصّ (`z.uuid`)، و`<BranchPicker>`
            يفرّق «لا اختيار» بـ`null` — فالترجمة بين العقدين هنا وحدها */}
        <ControlledField<string>
          name="branchId"
          render={(field) => (
            <BranchPicker
              value={field.value === "" ? null : field.value}
              onChange={(branchId) => field.onChange(branchId ?? "")}
            />
          )}
        />

        <BaseCurrency />

        <DateField name="postingDate" label="تاريخ الترحيل" />
        <DateField name="documentDate" label="تاريخ المستند" />
        <TextField name="description" label="البيان" />
      </section>

      <Lines />
    </ZodForm>
  );
}
