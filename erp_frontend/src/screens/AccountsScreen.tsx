import { useAllAccounts } from "../api/useAllAccounts";
import type { AccountItem } from "../api/useAccounts";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { DataGrid } from "../components/data-grid/DataGrid";
import type { GridColumn } from "../components/data-grid/DataGrid";
import { useCurrencyLookup } from "../currency/currency-registry";
import { ACCOUNT_TYPE_LABELS, NORMAL_BALANCE_LABELS } from "./account-labels";

// ‏الأعمدة الستة من `AccountResponseDto` المقيس. وما لا يُعرض مقصود: `id` و`companyId`
// و`parentAccountId` و`currencyId` معرّفات بلا معنى بصريّ، و`systemAccountRole` بلا
// مستهلك — ولا يُعرض حقل لأنه موجود.
const COLUMNS: GridColumn<AccountItem>[] = [
  { accessorKey: "code", header: "رمز الحساب" },
  { accessorKey: "name", header: "اسم الحساب" },

  // ‏`accessorFn` لا `accessorKey`: القيمة المعروضة **مشتقّة** لا منسوخة. والترجمة
  // في المُلحِق لا في `cell` كي يفرز العمود على النصّ العربي المعروض لا على الرقم
  {
    id: "accountType",
    header: "النوع",
    accessorFn: (account) => ACCOUNT_TYPE_LABELS[account.accountType]
  },
  {
    id: "normalBalance",
    header: "الطبيعة",
    accessorFn: (account) => NORMAL_BALANCE_LABELS[account.normalBalance]
  },

  // ‏نصّ صريح لا علامة عامة: «تجميعي» يقول للمحاسب **ما هو الحساب**، بينما خلية
  // فارغة مقابل خلية فيها علامة تقول «شيء ما مختلف» ولا تقول ماذا. يحرسه `A03`
  {
    id: "isPostable",
    header: "قابل للترحيل",
    accessorFn: (account) => (account.isPostable ? "نعم" : "تجميعي")
  },
  {
    id: "isActive",
    header: "نشط",
    accessorFn: (account) => (account.isActive ? "نشط" : "معطَّل")
  },

  // ‏العملة **لا تُشتق بمُلحِق** بخلاف بقية الأعمدة: حلّها يحتاج قراءة السجل، وهي
  // هوك لا يُستدعى إلا داخل مكوّن. فالخلية مكوّن، والفرز عليها خارج نطاق هذه القطعة
  {
    id: "currency",
    header: "العملة",
    cell: (context) => <CurrencyCell currencyId={context.row.original.currencyId ?? null} />
  }
];

// ‏**ثلاث حالات لا اثنتان** — وهي عين تمييز الدَّين ٢:
//
//   `null`            ⟵ الحساب **غير مقيَّد بعملة**، وهي حالة مشروعة في العقد
//                        (`currencyId: null | string`) لا خطأ. فخلية محايدة.
//   السجل لم يجهز     ⟵ «لا أعرف بعد» — ولا يُقال «غير معروفة» فيُتَّهم صفٌّ سليم
//   جهز ولم يجد       ⟵ **هنا وحدها** يُعلَن الجهل
//
// ‏ودمج الأولى أو الثانية في الثالثة كان يجعل الإنذار يظهر على صفوف لا عيب فيها،
// فيتعوّد المستخدم عليه ويفقد معناه. يحرسه `A07` و`A08`
function CurrencyCell({ currencyId }: { currencyId: string | null }) {
  const { isReady, currency } = useCurrencyLookup(currencyId ?? "");

  if (currencyId === null) {
    return <span>—</span>;
  }

  if (!isReady) {
    return <span aria-busy="true">…</span>;
  }

  // ‏الرمز أولاً ثم الكود — نفس سقوط `<MoneyDisplay>` (R-RPT-04)، ولم يُستخرج
  // مشتركاً بعدُ: قرار `V10` قائم، وسطرٌ مكرَّر أرخص من تجريد سابق لأوانه
  return <span>{currency === undefined ? "غير معروفة" : currency.code}</span>;
}

export function AccountsScreen() {
  const { status, accounts, error } = useAllAccounts();

  return (
    <>
      <h1>شجرة الحسابات</h1>

      {/* ‏**الفصل بين «لم تصل بعد» و«لا شيء»** — نمط `C01` و`Q06` نفسه: شبكة فارغة
          أثناء التحميل تقول «لا حسابات» وهي لم تسأل بعد. فلا يُرندَر جسم الشبكة
          أصلاً قبل أن يستقرّ الاستعلام. يحرسه `A04` */}
      {status === "pending" ? <p>جارٍ تحميل شجرة الحسابات…</p> : null}

      {/* ‏والخطأ لا يُبتلع في «لا حسابات»: انقطاع الخادم كان سيبدو شجرة فارغة —
          وهو الفشل المفتوح الذي وُجدت `Q06` له. يحرسه `A05` */}
      {status === "error" && error !== null ? <ApiErrorMessage error={error} /> : null}

      {status === "success" ? (
        <DataGrid rows={accounts} columns={COLUMNS} emptyMessage="لا حسابات مسجَّلة." />
      ) : null}
    </>
  );
}
