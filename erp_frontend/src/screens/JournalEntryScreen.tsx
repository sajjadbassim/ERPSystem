import { useId } from "react";
import { useWatch } from "react-hook-form";

import { useAllBranches } from "../api/useAllBranches";
import { useCompany } from "../api/useCompany";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { BranchPicker } from "../components/pickers/BranchPicker";
import { ControlledField, TextField, ZodForm } from "../forms/ZodForm";
import { journalEntryHeaderSchema } from "../schemas/journal-entry-header-schema";
import type { JournalEntryHeaderValues } from "../schemas/journal-entry-header-schema";

const EMPTY_HEADER: JournalEntryHeaderValues = { branchId: "", postingDate: "", documentDate: "", description: "" };

function DateField(props: { name: keyof JournalEntryHeaderValues; label: string }) {
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
// الاختيار `null` فلا نداء (`JE03`). و`useAllBranches` هنا لا يُطلق نداءً ثانياً:
// مفتاح الاستعلام نفسه الذي يملأ `<BranchPicker>`
function BaseCurrency() {
  const branchId = useWatch<JournalEntryHeaderValues, "branchId">({ name: "branchId" });
  const { branches } = useAllBranches();

  const companyId = branches.find((branch) => branch.id === branchId)?.companyId ?? null;
  const { status, company, error } = useCompany(companyId);

  if (status === "error" && error !== null) {
    return <ApiErrorMessage error={error} />;
  }

  // ‏R-RPT-04: الرمز من الخادم لا ثابتاً في الكود
  return company === null ? null : <p>{`عملة الدفاتر: ${company.baseCurrencyCode}`}</p>;
}

// ‏**رأس بلا إرسال — بقرار (2026-09-26).** رأسٌ بلا سطور لا يُرحَّل (50016)، فزرّ
// الإرسال يأتي مع السطور ومعه `toHeaderRequest`. حتى ذلك الحين `onSubmit` فارغ لأن
// لا شيء يُطلقه، وقواعد الرأس محروسة في المخطط (`JH`).
//
// ‏و`sourceModule` ليس هنا حقلاً ولا قيمة: يُفرض عند حدّ الإرسال وحده (`JE02`, `JH08`)
export function JournalEntryScreen() {
  return (
    <ZodForm schema={journalEntryHeaderSchema} defaultValues={EMPTY_HEADER} onSubmit={() => {}}>
      <h1>قيد يومية</h1>

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
    </ZodForm>
  );
}
