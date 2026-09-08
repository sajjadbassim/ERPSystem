import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

import { SubmitButton, TextField, ZodForm } from "./ZodForm";
import { branchCreateSchema } from "../schemas/branch-schema";

// ‏مصفوفة الحالات F (النموذج العام).
//
// أول مستهلك حقيقي: إنشاء فرع — `POST /api/branches` جسمه `BranchCreateDto` بعينه،
// وهو نفس المخطط المبنيّ في `Z` والمربوط بالعقد عبر `satisfies`. فلا مخطط اختبار
// موازٍ ينحرف عن المستعمَل فعلاً.
//
// ورسائل الأخطاء تُفحص بـ**شظية مميِّزة** لا بالنصّ الكامل: المقصود إثبات أن الرسالة
// الصحيحة بلغت **حقلها الصحيح**، لا تثبيت صياغتها. وهذا يشدّد ما كان مكتوباً في
// جولة `Z` من أن الرسائل «غير محروسة» — صارت محروسةً **بالإسناد** لا بالنصّ.

const LABELS = {
  companyId: "الشركة",
  code: "رمز الفرع",
  name: "اسم الفرع"
} as const;

const EMPTY = { companyId: "", code: "", name: "" };

const VALID_COMPANY_ID = "0199a1f0-0000-7000-8000-000000000001";

function renderBranchForm(onSubmit: (values: unknown) => void | Promise<void>) {
  render(
    <ZodForm schema={branchCreateSchema} defaultValues={EMPTY} onSubmit={onSubmit}>
      <TextField name="companyId" label={LABELS.companyId} />
      <TextField name="code" label={LABELS.code} />
      <TextField name="name" label={LABELS.name} />
      <SubmitButton>حفظ</SubmitButton>
    </ZodForm>
  );
}

function fill(values: Partial<Record<keyof typeof LABELS, string>>) {
  for (const [field, value] of Object.entries(values)) {
    const label = LABELS[field as keyof typeof LABELS];
    fireEvent.change(screen.getByLabelText(label), { target: { value } });
  }
}

function submit() {
  fireEvent.click(screen.getByRole("button", { name: "حفظ" }));
}

describe("ZodForm — F (النموذج العام، أول مستهلك: إنشاء فرع)", () => {
  it("F01: إرسال صالح يمرّر القيم حرفياً بلا اقتصاص ولا تحوير", async () => {
    const onSubmit = vi.fn();
    renderBranchForm(onSubmit);

    // ‏المسافات الطرفية هي **الكاشف الوحيد** للاقتصاص الصامت: بلا `" BR1 "` يمرّ أي
    // تنفيذ يقتصّ. و`" BR1 "` صالح للمخطط (خمسة محارف ≤ 20، وغير فارغ).
    // ‏⚠ إن أُريد الاقتصاص لاحقاً فهو **قرار صريح مُختبَر**، لا سلوك افتراضي يتسلل
    fill({ companyId: VALID_COMPANY_ID, code: " BR1 ", name: "الفرع الرئيسي" });
    submit();

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));

    expect(onSubmit).toHaveBeenCalledWith({
      companyId: VALID_COMPANY_ID,
      code: " BR1 ",
      name: "الفرع الرئيسي"
    });
  });

  it("F02: خطآن في حقلين ⟵ كل رسالة تحت حقلها لا في كومة واحدة", async () => {
    renderBranchForm(vi.fn());

    fill({ companyId: "ليس-uuid", code: "BR1", name: "" });
    submit();

    // ‏الوصف المتاح (`aria-describedby`) لا مجرد وجود النصّ في الصفحة: هو ما يثبت
    // **الإسناد** إلى الحقل، ويقرؤه قارئ الشاشة والمبصر معاً
    await waitFor(() =>
      expect(screen.getByLabelText(LABELS.companyId)).toHaveAccessibleDescription(/الشركة/u)
    );

    expect(screen.getByLabelText(LABELS.name)).toHaveAccessibleDescription(/اسم الفرع/u);

    // ‏الحارس الحقيقي ضد «كومة أخطاء واحدة تحت أول حقل»
    expect(screen.getByLabelText(LABELS.name)).not.toHaveAccessibleDescription(/الشركة/u);
  });

  it("F03: تجاوز الطول يظهر أيضاً — نوع خطأ آخر لا يُغفَل", async () => {
    renderBranchForm(vi.fn());

    fill({ companyId: VALID_COMPANY_ID, code: "A".repeat(21), name: "الفرع الرئيسي" });
    submit();

    // ‏`too_big` غير `invalid_format` وغير `too_small`: تنفيذٌ يعالج نوعين ويُسقط
    // الثالث يمرّ من `F02` وحدها
    await waitFor(() =>
      expect(screen.getByLabelText(LABELS.code)).toHaveAccessibleDescription(/أطول/u)
    );
  });

  it("F04: حمولة فاسدة ⟵ صفر استدعاء لدالة الإرسال", async () => {
    const onSubmit = vi.fn();
    renderBranchForm(onSubmit);

    fill({ companyId: "ليس-uuid", code: "", name: "" });
    submit();

    // ‏الانتظار حتى يظهر خطأ **قبل** تأكيد عدم الاستدعاء: بدونه يمرّ التأكيد لأن
    // الإرسال لم يبدأ بعد لا لأنه مُنع — فخّ «الصادق بالفراغ» نفسه
    await waitFor(() =>
      expect(screen.getByLabelText(LABELS.companyId)).toHaveAccessibleDescription(/الشركة/u)
    );

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("F05: حالة الإرسال معلَنة أثناءه ومنتهية بعده", async () => {
    let release: () => void = () => {};
    const onSubmit = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          release = resolve;
        })
    );

    renderBranchForm(onSubmit);
    fill({ companyId: VALID_COMPANY_ID, code: "BR1", name: "الفرع الرئيسي" });
    submit();

    await waitFor(() => expect(screen.getByRole("button", { name: "حفظ" })).toBeDisabled());

    release();

    await waitFor(() => expect(screen.getByRole("button", { name: "حفظ" })).toBeEnabled());
  });

  it("F06: نقرتان متتاليتان ⟵ نداء واحد فقط", async () => {
    let release: () => void = () => {};
    const onSubmit = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          release = resolve;
        })
    );

    renderBranchForm(onSubmit);
    fill({ companyId: VALID_COMPANY_ID, code: "BR1", name: "الفرع الرئيسي" });

    // ‏الخطر ملموس: `POST /api/branches` بلا حجب يقبل نقرتين ⟵ **فرعان**.
    // و`F05` تعلن الحالة، وهذه تثبت أن الإعلان **يمنع التكرار فعلاً** — بلاها يبقى
    // الإعلان بلا أثر مُثبَت (نمط `Z01` و`U05` نفسه)
    submit();
    submit();

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));

    release();
  });
});
