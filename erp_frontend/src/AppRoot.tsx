import { useState } from "react";
import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { AppThemeProvider } from "./theme/AppThemeProvider";
import { AuthProvider } from "./auth/AuthProvider";
import { queryClientConfig } from "./api/query-config";

// ‏وُجد هذا المكوّن لأن `main.tsx` يستدعي `createRoot` فلا يُختبَر. فتُنقل تركيبة
// المزوّدات إليه ويبقى `main.tsx` سطرين — وهكذا يصير **الدَّين ٧ مقيساً** (`L09`)
// بدل أن يُدَّعى.

export type AppRootProps = {
  children: ReactNode;
};

export function AppRoot(props: AppRootProps) {
  // ‏عميل واحد لعمر التطبيق: إنشاؤه في جسم المكوّن كان يُنتج عميلاً جديداً عند كل
  // تصيير، فتُفقد الذاكرة المؤقتة ويُعاد كل استعلام من الصفر
  // ‏الإعدادات من `query-config` لا مكتوبة هنا: `main.tsx` ليس الموضع الوحيد الذي
  // يُنشأ فيه عميل (الاختبارات تُنشئ عملاءها)، وقرارٌ مبثوث في مواضع ينحرف بينها
  const [queryClient] = useState(() => new QueryClient(queryClientConfig));

  // ‏والترتيب مقصود: `AuthProvider` **داخل** مزوّد الاستعلام لا فوقه، لأن بوابة
  // ‏`enabled` في الهوكات تقرأ حالة المصادقة وهي داخل شجرة الاستعلام
  return (
    <AppThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>{props.children}</AuthProvider>
      </QueryClientProvider>
    </AppThemeProvider>
  );
}
