import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { AppRoot } from "./AppRoot";

const container = document.getElementById("root");

// ‏الفشل صريح لا صامت: `createRoot(container!)` يمرّ في TypeScript ويسقط في المتصفح
// برسالة من داخل React لا تدلّ على أن `#root` هو الغائب
if (!container) {
  throw new Error("عنصر الجذر #root غير موجود في index.html.");
}

// ‏`<AppRoot>` لا `<AppThemeProvider>` وحده: هو من يركّب السمة ومزوّد الاستعلام
// والمصادقة **بالترتيب الصحيح** (المصادقة داخل مزوّد الاستعلام). وقبل هذا السطر كان
// ‏`AppRoot` مبنيّاً ومختبَراً بـ`L09` **ولا أحد يستدعيه** — فالتطبيق الجاري في
// المتصفح بلا مزوّد استعلام. وهذا السطر بعينه هو ما يغلق الدَّين ٧، ولا يحرسه اختبار
// لأن `main.tsx` يستدعي `createRoot` — ولهذا تُركَّب `AppRoot` في اختبارات `LGN`
// بنفس الشكل الذي يُركَّب به هنا، فينكسر أيّ افتراق بينهما
createRoot(container).render(
  <StrictMode>
    <AppRoot>
      <App />
    </AppRoot>
  </StrictMode>
);
