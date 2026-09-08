import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import Button from "@mui/material/Button";
import { useTheme } from "@mui/material/styles";

import { AppThemeProvider } from "./AppThemeProvider";

// ‏مصفوفة الحالات R (اتجاه) — تحرس أن RTL **يعمل فعلاً** لا أن يُعلَن.
//
// ‏ولماذا لا يُختبر `dir="rtl"` على `<html>`؟ لأنه **قِيس أنه غير مرئي هنا**: المسبار
// ‏`P01` في المرحلة ٢ أثبت أن `index.html` لا يُحمَّل في jsdom أصلاً
// (`documentElement.dir = ""`). فاختبارٌ عليه كان سيقيس سقالته لا ما يحرسه — وهو
// السبب نفسه الذي رُفض من أجله اختبار الدخان حينها.
//
// وهاتان الحالتان تحرسان **الآليتين اللتين لا يقدر عليهما `dir` وحده**: قلب الأنماط
// في CSS المحقون، وبلوغ الاتجاه إلى منطق المكوّنات.

function collectInjectedCss(): string {
  return Array.from(document.querySelectorAll("style"))
    .map((element) => element.textContent ?? "")
    .join("\n");
}

// ‏يقرأ الاتجاه من السمة كما تقرؤه مكوّنات MUI نفسها (الدرج والمبدّل وغيرها تتفرّع
// عليه)، فيثبت أنه بلغ الشجرة لا أنه كُتب في كائن معزول
function DirectionProbe() {
  return <span>{useTheme().direction}</span>;
}

describe("AppThemeProvider — RTL يعمل فعلاً لا يُعلَن", () => {
  it("R01: ملحق القلب يعكس الخصائص الاتجاهية في CSS المحقون", () => {
    render(
      <AppThemeProvider>
        <Button sx={{ paddingLeft: "10px" }}>حفظ</Button>
      </AppThemeProvider>
    );

    const css = collectInjectedCss();

    // ‏قِيس بمسبار مؤقت (`P02`، حُذف) أن هذا بالضبط ما يفترق: بلا إعداد RTL يخرج
    // ‏`padding-left:10px` ولا يخرج نظيره المعكوس. و`10px` تخصّص الفحص فلا يصادف
    // حشوة أخرى من MUI
    expect(css).toContain("padding-right:10px");
    expect(css).not.toContain("padding-left:10px");
  });

  it("R02: اتجاه السمة يبلغ منطق المكوّنات", () => {
    render(
      <AppThemeProvider>
        <DirectionProbe />
      </AppThemeProvider>
    );

    // ‏المطابقة على النمط ثم على النصّ — لا `getByText("rtl")` مباشرةً: الأول يُرسب
    // بـ«توقّعت 'ltr' أن تكون 'rtl'»، والثاني يرمي «لم أجد عنصراً» فيخفي القيمة الفعلية
    expect(screen.getByText(/^(rtl|ltr)$/u).textContent).toBe("rtl");
  });
});
