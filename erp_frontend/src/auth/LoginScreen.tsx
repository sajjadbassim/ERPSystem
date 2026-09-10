import { traceIdToShow } from "../api/api-error";
import { SubmitButton, TextField, ZodForm } from "../forms/ZodForm";
import { loginSchema } from "../schemas/login-schema";
import { useAuth } from "./AuthProvider";

// ‏الشاشة **لا تملك حالة خطأ خاصة بها**: الرسالة تُقرأ من `useAuth().error` وحده.
// ومصدران لخطأ واحد كانا سيفترقان — رسالة تبقى معروضة بعد نجاح المحاولة التالية،
// وهو نمط «نسختان من حقيقة واحدة» الممنوع في هذا المشروع.
//
// ‏ولا حالة تحميل محلية كذلك: `formState.isSubmitting` في `<SubmitButton>` يقيسها
// من إحالة الإرسال نفسها ما دام `onSubmit` يُرجع الوعد. يحرسه `LGN05`.
export function LoginScreen() {
  const { error, login } = useAuth();

  return (
    <ZodForm
      schema={loginSchema}
      defaultValues={{ userName: "", password: "" }}
      onSubmit={(values) => login(values)}>
      <h1>تسجيل الدخول</h1>

      <TextField name="userName" label="اسم المستخدم" />
      <TextField name="password" label="كلمة المرور" type="password" />

      {/* ‏`role="alert"` لا نصّاً مجرداً: فشل الدخول حدث يجب أن يبلغ قارئ الشاشة
          فور وقوعه، لا أن ينتظر أن يتصفّح المستخدم الصفحة بحثاً عنه */}
      {error === null ? null : (
        <p role="alert">
          {error.message}

          {/* ‏`traceIdToShow` لا شرط مكتوب هنا: القاعدة واحدة تعيش في موضع واحد.
              ونسخها في كل شاشة كان يجعلها تنحرف — إحداها تُظهره في 401 والأخرى لا */}
          {traceIdToShow(error) === null ? null : (
            <span>{` رقم التتبّع: ${traceIdToShow(error) ?? ""}`}</span>
          )}
        </p>
      )}

      <SubmitButton>تسجيل الدخول</SubmitButton>
    </ZodForm>
  );
}
