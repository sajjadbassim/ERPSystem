import { useAuth } from "./auth/AuthProvider";
import { LoginScreen } from "./auth/LoginScreen";
import { AccountsScreen } from "./screens/AccountsScreen";

// ‏**‏`<AccountsScreen>` خلفت `<ProtectedHome>`** (2026-09-10).
//
// ‏وذاك وُجد بغرض واحد معلَن — «أصغر شاشة محمية ممكنة» تُمكّن قياس سلسلة النقل حيّاً —
// وقد أدّاه. فلمّا جاءت شاشة حقيقية تستهلك المكوّنات المبنية، لم يبقَ في التطبيق
// موضعٌ لمكوّن غرضه أن يُقاس به. **حذفه إتمام لدوره لا تراجع عنه.**
//
// ‏البوابة **منع عرض لا إعادة توجيه**: لا موجّه في المشروع (مقيس: صفر تبعية توجيه
// في `package.json`)، وشاشة واحدة لا تحتاج مسارات. وموضع قرار التوجيه §4.5 حين
// تصير الوجهات اثنتين فأكثر. ومصدر القرار `status` من المزوّد وحده، لا فحص للمخزن
// هنا: نسخةٌ ثانية من الشرط كانت ستنحرف عن الأولى. يحرسه `LGN06`
export function App() {
  const { status } = useAuth();

  return status === "authenticated" ? <AccountsScreen /> : <LoginScreen />;
}
