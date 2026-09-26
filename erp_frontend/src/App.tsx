import { useState } from "react";

import { useAuth } from "./auth/AuthProvider";
import { LoginScreen } from "./auth/LoginScreen";
import { AccountsScreen } from "./screens/AccountsScreen";
import { JournalEntryScreen } from "./screens/JournalEntryScreen";

// ‏**‏`<AccountsScreen>` خلفت `<ProtectedHome>`** (2026-09-10).
//
// ‏وذاك وُجد بغرض واحد معلَن — «أصغر شاشة محمية ممكنة» تُمكّن قياس سلسلة النقل حيّاً —
// وقد أدّاه. فلمّا جاءت شاشة حقيقية تستهلك المكوّنات المبنية، لم يبقَ في التطبيق
// موضعٌ لمكوّن غرضه أن يُقاس به. **حذفه إتمام لدوره لا تراجع عنه.**
//
// ‏البوابة **منع عرض لا إعادة توجيه**. ومصدر القرار `status` من المزوّد وحده، لا فحص
// للمخزن هنا: نسخةٌ ثانية من الشرط كانت ستنحرف عن الأولى. يحرسه `LGN06`
export function App() {
  const { status } = useAuth();

  return status === "authenticated" ? <AuthenticatedShell /> : <LoginScreen />;
}

type Destination = "accounts" | "journal-entry";

const DESTINATIONS: readonly { id: Destination; label: string }[] = [
  { id: "accounts", label: "شجرة الحسابات" },
  { id: "journal-entry", label: "قيد يومية" }
];

// ‏**حالة تنقّل داخلية، بلا موجّه وبلا URL** — قرار 2026-09-12 بثمنه المعلن: لا روابط
// عميقة، لا زرّ رجوع، وإعادة التحميل تعود إلى الوجهة الافتراضية. يحرسه `NAV05`.
//
// ‏والحالة **داخل** البوابة لا فوقها: تُمحى بانتهاء الجلسة فلا تَرِث جلسةٌ تالية وجهة
// سابقتها. والوجهة المعروضة وحدها مركَّبة، لا الاثنتان مع إخفاء، فلا تبقى استعلامات
// الوجهة الغائبة حيّة (`NAV02`)
function AuthenticatedShell() {
  const [destination, setDestination] = useState<Destination>("accounts");

  return (
    <>
      <nav aria-label="التنقّل">
        {DESTINATIONS.map((item) => (
          <button
            key={item.id}
            type="button"
            aria-current={item.id === destination ? "page" : undefined}
            onClick={() => setDestination(item.id)}
          >
            {item.label}
          </button>
        ))}
      </nav>

      {destination === "accounts" ? <AccountsScreen /> : <JournalEntryScreen />}
    </>
  );
}
