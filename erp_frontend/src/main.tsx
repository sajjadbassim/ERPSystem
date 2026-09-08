import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";

const container = document.getElementById("root");

// ‏الفشل صريح لا صامت: `createRoot(container!)` يمرّ في TypeScript ويسقط في المتصفح
// برسالة من داخل React لا تدلّ على أن `#root` هو الغائب
if (!container) {
  throw new Error("عنصر الجذر #root غير موجود في index.html.");
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>
);
