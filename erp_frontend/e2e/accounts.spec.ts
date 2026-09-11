import { expect, test } from "@playwright/test";
import type { Locator, Page } from "@playwright/test";

import { BOUND_CODE, UNBOUND_CODE } from "./global-setup";
import { DEV_PASSWORD, DEV_USER } from "./erp-api";

// ‏موضع عمود «العملة» **يُقرأ من الرؤوس ولا يُثبَّت رقماً**: إدراج عمود قبله يوماً
// كان سيجعل التأكيد يقرأ خليّة أخرى **ويبقى أخضر** — فيحرس شيئاً غير الذي يدّعيه.
async function currencyColumnIndex(page: Page): Promise<number> {
  const headers = await page.getByRole("columnheader").allTextContents();
  const index = headers.findIndex((text) => text.trim() === "العملة");

  expect(index, `‏لا عمود اسمه «العملة» في الرؤوس: ${JSON.stringify(headers)}`)
    .toBeGreaterThanOrEqual(0);

  return index;
}

function rowByCode(page: Page, code: string): Locator {
  return page.getByRole("row")
    .filter({ has: page.getByRole("cell", { name: code, exact: true }) });
}

// ‏`BR01` — أول اختبار تكامل بمتصفح حقيقيّ. لا محاكاة ولا خادم وهميّ: Chromium
// ‏فعليّ ⟵ Vite ⟵ وكيل ⟵ ErpApi حيّة ⟵ SQL Server. وكل القياسات الحيّة قبله
// (`P03`–`P06`) كانت مسابر jsdom مؤقتة تُحذف، بلا متصفح.
test("BR01 — دخول حقيقي ثم شجرة الحسابات بعملة محلولة في صفّ حقيقي", async ({ page }) => {
  // ‏(١) دخول حقيقيّ — عبر الواجهة في كل تشغيلة، **بلا `storageState`**: الدخول
  //     نصف قيمة هذا الاختبار، وتخزين الحالة يتخطّى `fetch` المتصفح ومخزن الرمز
  //     وسلسلة المصادقة كلها فلا يبقى ما يُقاس منها.
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "تسجيل الدخول" })).toBeVisible();

  await page.getByLabel("اسم المستخدم").fill(DEV_USER);
  await page.getByLabel("كلمة المرور").fill(DEV_PASSWORD);
  await page.getByRole("button", { name: "تسجيل الدخول" }).click();

  // ‏(٢) التحوّل إلى الشاشة المحمية — وهو **منع عرض لا إعادة توجيه**: لا موجّه في
  //     المشروع، و`App.tsx` يبدّل المكوّن على `status` وحده. فظهور العنوان هو
  //     التحوّل نفسه، ولا عنوان URL يتغيّر لنترقّبه.
  await expect(page.getByRole("heading", { name: "شجرة الحسابات" })).toBeVisible();

  const currencyIndex = await currencyColumnIndex(page);

  // ‏(٣) الركيزة: حلقة `GET /api/currencies` ⟵ السجل ⟵ `currencyId` ⟵ رمز معروض،
  //     مقطوعةً بمتصفح حقيقيّ لا بـjsdom.
  const boundCurrency = rowByCode(page, BOUND_CODE).getByRole("cell").nth(currencyIndex);

  await expect(boundCurrency).toHaveText("IQD");

  // ‏(٤) **ضدّ النجاح الفارغ** — نظير `L12` المسجَّل: بدون هذا التأكيد يمرّ الاختبار
  //     على شبكة تكتب `IQD` في كل صفّ. والحساب غير المقيَّد بعملة حالة **مشروعة**
  //     في العقد (`currencyId: null | string`)، فخليّته محايدة `—` لا إنذار.
  const unboundCurrency = rowByCode(page, UNBOUND_CODE).getByRole("cell").nth(currencyIndex);

  await expect(unboundCurrency).toHaveText("—");

  // ‏و«غير معروفة» لا تظهر على أي صفّ: هي إعلان جهلٍ بعد جهوز السجل، وظهورها على
  //     صفّ سليم يُفقد الإنذار معناه. يحرسه `A07`/`A08` في jsdom، وهنا حيّاً.
  await expect(page.getByRole("table").getByText("غير معروفة")).toHaveCount(0);
});
