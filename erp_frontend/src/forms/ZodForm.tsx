import type { ReactNode } from "react";
import { FormProvider, useForm, useFormContext } from "react-hook-form";
import type { DefaultValues, FieldValues } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type { z } from "zod";

// ‏التعميم على **قيم** النموذج لا على نوع المخطط: `useForm` يشترط
// ‏`TFieldValues extends FieldValues`، ولا سبيل لإقناع TypeScript بأن `z.infer<TSchema>`
// يحقق الشرط ما دام `TSchema` حرّاً. فالقيد يوضع حيث يُفرض
export type ZodFormProps<TValues extends FieldValues> = {
  // ‏المخرَج **والمدخل** معاً: تثبيت المخرَج وحده يترك مدخل المحلِّل `FieldValues`،
  // فيرفض `useForm<TValues>` نوعَ محلِّله. مقيس بـ`TS2322` قبل التثبيت
  schema: z.ZodType<TValues, TValues>;
  defaultValues: DefaultValues<TValues>;
  onSubmit: (values: TValues) => void | Promise<void>;
  children: ReactNode;
};

// ‏حدود التجريد: **العام** يملك الربط بـZod/RHF وحجب الإرسال وحالة الإرسال وإسناد
// الخطأ إلى حقله. و**المستهلك** يملك الحقول نفسها — لأن تعميم شكل الحقل بمستهلك
// واحد تجريدٌ على محور مجهول (القاعدة نفسها التي منعت استخراج `symbol ?? code`).
//
// ‏والحجب من `handleSubmit` نفسها لا من فحص مكتوب بجانبها: RHF لا تستدعي المُعالج
// إلا بعد نجاح المحلِّل. وفحصٌ ثانٍ موازٍ كان سيصير **نسخة ثانية من القاعدة تنحرف
// بصمت** — وهو ما مُنع في الباك-إند حين تُرك عدد السطور للإجراء المخزَّن وحده.
//
// ‏**R-API-05:** هذا كله تحقق للتجربة، والمعتبر ما يفرضه الخادم.
export function ZodForm<TValues extends FieldValues>(props: ZodFormProps<TValues>) {
  const form = useForm<TValues>({
    resolver: zodResolver(props.schema),
    defaultValues: props.defaultValues
  });

  // ‏`(values) => props.onSubmit(values)` لا `props.onSubmit` مباشرةً: RHF تستدعي
  // المُعالج بـ`(data, event)`، فتمريره مباشرةً **يسرّب حدث DOM إلى عقد المستهلك**.
  // أمسكته `F01` بمطابقة الوسائط بالمساواة — ولو فحصت الوسيط الأول وحده لمرّ
  return (
    <FormProvider {...form}>
      <form onSubmit={form.handleSubmit((values) => props.onSubmit(values))}>{props.children}</form>
    </FormProvider>
  );
}

export type TextFieldProps = {
  name: string;
  label: string;

  // ‏إضافة **اختيارية** لا تغيّر سلوك أي مستهلك قائم (الافتراض `text` كما كان).
  // ووُجدت لحاجة واحدة مقيسة: حقل كلمة المرور. وحقلٌ يعرض كلمة المرور نصّاً ظاهراً
  // ليس «صقلاً مؤجَّلاً» بل عطب تشغيل — وهو حدّ ما يوجبه R-UI-03 لا ما يتجاوزه
  type?: "text" | "password";
};

export function TextField(props: TextFieldProps) {
  const { register, formState } = useFormContext();

  const id = `field-${props.name}`;
  const errorId = `error-${props.name}`;
  const message = formState.errors[props.name]?.message;

  return (
    <p>
      <label htmlFor={id}>{props.label}</label>

      {/* ‏الإسناد عبر `aria-describedby` لا مجرد وضع النصّ بجوار الحقل: الثاني يُظهر
          الرسالة، والأول **يربطها بحقلها** — فيقرؤها قارئ الشاشة والمبصر معاً، ولا
          تصير الأخطاء كومة واحدة لا يُعرف أيّها لأيّ حقل */}
      <input
        id={id}
        type={props.type ?? "text"}
        aria-invalid={message === undefined ? undefined : true}
        aria-describedby={message === undefined ? undefined : errorId}
        {...register(props.name)}
      />

      {message === undefined ? null : (
        <span id={errorId} role="alert">
          {String(message)}
        </span>
      )}
    </p>
  );
}

export type SubmitButtonProps = {
  children: ReactNode;
};

// ‏التعطيل أثناء الإرسال ليس تحسيناً بصرياً: `POST /api/branches` بلا حجب يقبل
// نقرتين ⟵ **فرعان**. والزرّ المعطَّل لا تُنشَّط عليه إحالة الإرسال أصلاً، فتُمنع
// النقرة الثانية في الطبقة نفسها التي تُعلن الحالة — لا في حارس منفصل يُنسى
export function SubmitButton(props: SubmitButtonProps) {
  const { formState } = useFormContext();

  return (
    <button type="submit" disabled={formState.isSubmitting}>
      {props.children}
    </button>
  );
}
