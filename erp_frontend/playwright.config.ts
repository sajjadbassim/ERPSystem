import { defineConfig, devices } from "@playwright/test";

// ‏اختبارات المتصفح **معزولة عن Vitest بنيوياً لا بالاتفاق**: `vite.config.ts` يكنس
// ‏`src/**/*.test.{ts,tsx}`، فملفٌ هنا بذلك الاسم والموضع كان سيلتقطه Vitest ويُرسبه
// (لا متصفح في jsdom). فالعزل ثلاثيّ: مجلد `e2e/` خارج `src`، ولاحقة `.spec.ts` لا
// ‏`.test.ts`، وأمر `test:e2e` منفصل لا يمسّ `npm test`.
export default defineConfig({
  testDir: "e2e",
  testMatch: /.*\.spec\.ts/,

  // ‏تسلسليّ بعامل واحد: `BR01` يكتب في قاعدة التطوير عبر التمهيد، ويقرأ شجرة
  // حسابات مشتركة. والتوازي على حالة مشتركة يُنتج رسوباً متقطّعاً — وهو أسوأ من
  // الرسوب الثابت لأنه يُدرَّب على تجاهله.
  fullyParallel: false,
  workers: 1,

  // ‏صفر إعادة محاولة: إعادةُ المحاولة تُخفي التقطّع بدل أن تكشفه، والاختبار الذي
  // يمرّ في الثانية ولا يمرّ في الأولى **لم يمرّ**.
  retries: 0,

  reporter: [["list"]],

  globalSetup: "./e2e/global-setup.ts",

  use: {
    // ‏المتصفح يكلّم Vite لا الـAPI: الوكيل يجعل الأصل والوجهة أصلاً واحداً.
    baseURL: "http://localhost:5173",

    // ‏**بلا `ignoreHTTPSErrors` عن قصد**: مقيس أن المتصفح لا يبلغ شهادة التطوير
    // الموقَّعة ذاتياً أصلاً — الوكيل في `vite.config.ts` هو من يكلّم `https://…:7175`
    // بـ`secure: false`. فإضافة الخيار «احتياطاً» كانت ستُخفي خطأ شهادة حقيقياً
    // في مسار المتصفح يوماً.

    trace: "retain-on-failure",
    screenshot: "only-on-failure"
  },

  // ‏Chromium وحده: ثلاثة متصفحات تُثلّث الزمن والتنزيل بلا تغطية إضافية لمنطقنا،
  // و**R-UI-03** «تشغيلية لا مصقولة». ويُوسَّع حين يظهر عطب متصفح بعينه.
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } }
  ],

  // ‏Vite **يُشغَّل تلقائياً** بخلاف الـAPI: عملية بلا حالة ولا أسرار، وإقلاعها
  // فوريّ وفشلها يظهر فوراً. و`reuseExistingServer` يلتقط خادم تطوير قائماً بدل
  // أن يصطدم بالمنفذ.
  webServer: {
    command: "npm run dev",
    url: "http://localhost:5173",
    reuseExistingServer: true,
    timeout: 60_000,
    stdout: "ignore",
    stderr: "pipe"
  }
});
