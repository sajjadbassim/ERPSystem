import { useId, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useWatch } from "react-hook-form";

import { useAllBranches } from "../api/useAllBranches";
import type { BranchItem } from "../api/useAllBranches";
import { companyQueryKey, useCompany } from "../api/useCompany";
import type { CompanyItem, UseCompanyResult } from "../api/useCompany";
import { usePostJournalEntry } from "../api/usePostJournalEntry";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { BranchPicker } from "../components/pickers/BranchPicker";
import { ControlledField, SubmitButton, TextField, ZodForm } from "../forms/ZodForm";
import { toHeaderRequest } from "../schemas/journal-entry-header-schema";
import { journalEntryFormSchema, newLine, toLinesRequest } from "../schemas/journal-line-schema";
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
// الاختيار `null` فلا نداء (`JE03`). واشتقاقٌ واحد يستعمله العرض وحدّ الإرسال معاً
function companyIdOf(branches: readonly BranchItem[], branchId: string): string | null {
  return branches.find((branch) => branch.id === branchId)?.companyId ?? null;
}

// ‏و`useAllBranches` لا يُطلق نداءً ثانياً: مفتاح الاستعلام نفسه الذي يملأ
// ‏`<BranchPicker>`. ومستهلكان (العرض والسطور) لا يضاعفان النداء: مفتاح `useCompany` واحد
function useSelectedCompany(): UseCompanyResult {
  const branchId = useWatch<JournalEntryFormValues, "branchId">({ name: "branchId" });
  const { branches } = useAllBranches();

  return useCompany(companyIdOf(branches, branchId));
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

// ‏**لا توازن حيّ (قرار 2026-09-28) — R-API-01 حرفياً:** لا مجموع مدين ولا دائن في
// المتصفح. يُرسَل ما أُدخل، والخادم يرفض بـ50001/50002، ورسالته تُعرض بحرفها أعلى
// الشاشة. **خطأ عام واحد لا رقم ولا سطر** — الخادم لا يُرسل أيّهما.
//
// ‏و`sourceModule` ليس هنا حقلاً ولا قيمة: يُفرض عند حدّ الإرسال وحده (`JE02`, `JH08`)
export function JournalEntryScreen() {
  const queryClient = useQueryClient();
  const { branches } = useAllBranches();
  const post = usePostJournalEntry();

  // ‏قرار 2026-09-28: النجاح يفرّغ النموذج. **بإعادة تركيبه لا بتصفير قيمه**: مفتاح
  // جديد يُعيد كل شيء إلى الافتراضي — الرأس والسطور وحالة المنتقيات — ولا يترك
  // حقلاً نُسي في قائمة تصفير. والنتيجة خارج النموذج فتبقى بعد التفريغ
  const [formKey, setFormKey] = useState(0);

  async function submit(values: JournalEntryFormValues) {
    // ‏عملة الدفاتر **من الذاكرة نفسها التي تعرضها الشاشة** لا من جلبٍ جديد: فيُرسَل
    // القفل على ما رآه المستخدم. وقبل وصول الشركة لا قفل، والخادم يفرض 50012
    const companyId = companyIdOf(branches, values.branchId);
    const company = companyId === null ? undefined : queryClient.getQueryData<CompanyItem>(companyQueryKey(companyId));

    try {
      await post.mutateAsync({
        ...toHeaderRequest(values),
        lines: toLinesRequest(values.lines, company?.baseCurrencyId ?? null)
      });

      setFormKey((key) => key + 1);
    } catch {
      // ‏الخطأ في `post.error` ويُعرض أدناه. وابتلاعه هنا ليس إسكاتاً: RHF تُعيد رمي ما
      // يُرمى من المعالج، فيصير رفض الخادم **وعداً مرفوضاً بلا معالج** فوق عرضه
    }
  }

  return (
    <>
      <h1>قيد يومية</h1>

      {post.error === null ? null : <ApiErrorMessage error={post.error} />}

      {/* ‏`role="status"` لا `alert`: نجاح يُعلَن بهدوء، والتنبيه للأعطال */}
      {post.data === undefined ? null : (
        <p role="status">{`${post.data.message ?? ""} — رقم المستند: ${post.data.documentNumber}`}</p>
      )}

      <ZodForm key={formKey} schema={journalEntryFormSchema} defaultValues={DEFAULT_VALUES} onSubmit={submit}>
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

        {/* ‏`<SubmitButton>` القائم وحده يكفي — مقيس: الشبكة **داخل** `<form>` نفسه، فحالة
            ‏`isSubmitting` تغطي الرأس والسطور معاً، ولا حالة تحميل ثانية (`JE17`) */}
        <SubmitButton>ترحيل القيد</SubmitButton>
      </ZodForm>
    </>
  );
}
