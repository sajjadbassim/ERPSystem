import type { ReactNode } from "react";
import { CacheProvider } from "@emotion/react";
import createCache from "@emotion/cache";
import { ThemeProvider, createTheme } from "@mui/material/styles";
import rtlPlugin from "@mui/stylis-plugin-rtl";
import { prefixer } from "stylis";

// ‏RTL في MUI **ثلاث آليات لا واحدة**، وإغفال أيّها يترك الاتجاه ناقصاً بصمت:
//
//   ١. `dir="rtl"` على `<html>` — في `index.html`. يحكم تدفّق النصّ في المتصفح،
//      **ولا يقلب حرفاً واحداً من CSS**. وهو غير مرئي في jsdom أصلاً (المسبار `P01`)،
//      ولهذا لا تحرسه `R01`/`R02` ولا تدّعيان ذلك.
//   ٢. `theme.direction` — تتفرّع عليه مكوّنات MUI في منطقها (الدرج، المبدّل…).
//      يحرسه `R02`.
//   ٣. ملحق القلب في ذاكرة Emotion — هو وحده الذي يحوّل `padding-left` إلى
//      ‏`padding-right` في الأنماط المولَّدة. يحرسه `R01`.
//
// ‏**والملحق هو `@mui/stylis-plugin-rtl` لا `stylis-plugin-rtl` المجتمعي:** التوثيق
// الرسمي الحالي يوجب تفرّع MUI، وقد وُجد لإصلاح مشاكل طبقات CSS ودعم أحدث Stylis.
// والمجتمعي هو ما أوصيتُ به في المرحلة صفر قبل أن يُقاس — فسقطت التوصية بالقياس.
//
// ‏و`prefixer` يسبقه في المصفوفة: البادئات تُضاف على الخاصية قبل قلبها، والعكس
// يترك بادئة على خاصية لم تعد موجودة.

const rtlCache = createCache({
  key: "muirtl",
  stylisPlugins: [prefixer, rtlPlugin]
});

// ‏سمة تشغيلية لا مصقولة (R-UI-03): الاتجاه وحده هو المقصود هنا، والصقل بعد
// استقرار النموذج المالي
const theme = createTheme({
  direction: "rtl"
});

export type AppThemeProviderProps = {
  children: ReactNode;
};

export function AppThemeProvider(props: AppThemeProviderProps) {
  return (
    <CacheProvider value={rtlCache}>
      <ThemeProvider theme={theme}>{props.children}</ThemeProvider>
    </CacheProvider>
  );
}
