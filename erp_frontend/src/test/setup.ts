// ‏مسار `/vitest` لا الجذر: هو الذي يسجّل المطابِقات في `expect` الخاص بـVitest
// **ويحقن أنواعها** — والاستيراد من الجذر يترك `toBeInTheDocument` خطأ نوع
import "@testing-library/jest-dom/vitest";

import { afterEach } from "vitest";
import { cleanup } from "@testing-library/react";

// ‏التنظيف صريح لأن `globals: false`. المكتبة تسجّله تلقائياً حين تجد `afterEach`
// عامّاً وحده؛ وبلا هذا السطر تتراكم أشجار الاختبارات في `document.body` فيصير
// ‏`getByRole` يرى عنصري اختبارين — **فشل عابر لا يُعزى إلى سببه**
afterEach(cleanup);
