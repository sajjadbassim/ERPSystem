// ‏`vitest/config` لا `vite`: هو `defineConfig` نفسه موسَّعاً بحقل `test`. وبه يبقى
// **إعداد واحد** يحكم بناء الإنتاج وتشغيل الاختبارات معاً — فما يُختبر هو ما يُبنى.
// وملف إعداد ثانٍ للاختبار كان سيصير محوّلاً موازياً ينحرف بصمت، وهو عين ما رُفض
// في تمهيد §4.4 بقاعدة «مولِّد واحد لا اثنان».
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// ‏الوكيل ليس رفاهية تطوير بل شرط عمل. `Program.cs` لا يسجّل CORS إطلاقاً — كنسة على
// ‏`Cors|UseCors|WithOrigins|AllowedOrigins` في `ErpApi` كله أخرجت **صفر** نتيجة — فنداء
// مباشر من أصل 5173 إلى أصل الـAPI يُحجب في المتصفح قبل أن يبلغ الخادم. والبديل الآخر
// — إضافة CORS — تعديل في `erp_backend` خارج نطاق §4.4. الوكيل يجعل الأصل والوجهة
// أصلاً واحداً، فلا يبقى للمتصفح ما يمنعه، ولا يُمسّ الباك-إند بحرف.
//
// ‏والهدف هو المنفذ المشفَّر لا `http://localhost:5299`: الـAPI يفعّل
// ‏`UseHttpsRedirection`، فالمنفذ العادي يردّ 307 إلى المشفَّر، والوكيل لا يتبع التحويل
// فيصل الرد بلا جسم — عطب يظهر كاستجابة فارغة لا كخطأ. و `secure: false` لأن شهادة
// التطوير موقَّعة ذاتياً، وهي على `localhost` وحدها ولا تعبر إلى أي بناء إنتاج.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "https://localhost:7175",
        changeOrigin: true,
        secure: false
      }
    }
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],

    // ‏بلا `globals`: كل `describe`/`it`/`expect` مستورد صراحةً. الشكل العام يوفّر
    // سطر استيراد ويكلّف تلويث فضاء الأسماء العام وحقل `types` في `tsconfig`،
    // ويجعل اسماً غير معرَّف يبدو معرَّفاً عند القراءة
    globals: false
  }
});
