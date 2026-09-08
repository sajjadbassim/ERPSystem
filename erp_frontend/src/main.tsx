import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { AppThemeProvider } from "./theme/AppThemeProvider";

const container = document.getElementById("root");

// ‏الفشل صريح لا صامت: `createRoot(container!)` يمرّ في TypeScript ويسقط في المتصفح
// برسالة من داخل React لا تدلّ على أن `#root` هو الغائب
if (!container) {
  throw new Error("عنصر الجذر #root غير موجود في index.html.");
}

// ‏المزوّد عند الجذر لا داخل شاشة: ذاكرة الأنماط تُنشأ مرة واحدة، ومزوّدان بمفتاحين
// مختلفين كانا سيحقنان نسختين من كل نمط
createRoot(container).render(
  <StrictMode>
    <AppThemeProvider>
      <App />
    </AppThemeProvider>
  </StrictMode>
);
