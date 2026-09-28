import { useState } from "react";

import { useTrialBalance } from "../api/useTrialBalance";
import type { TrialBalance } from "../api/useTrialBalance";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { DataGrid } from "../components/data-grid/DataGrid";
import type { GridColumn } from "../components/data-grid/DataGrid";
import { MoneyDisplay } from "../components/money/MoneyDisplay";
import { BranchPicker } from "../components/pickers/BranchPicker";

type Row = TrialBalance["rows"][number];

// ‏**تسمية عرض لا منطق:** `basis` يصل من الخادم (R-RPT-02)، وهذه ترجمته إلى العربية فحسب.
// وقيمة لم تُترجم تُعرض كما وصلت — لا تُخفى ولا يُخمَّن معناها
const BASIS_LABELS: Record<string, string> = { BaseCurrency: "عملة الدفاتر" };

// ‏لا فرز: ترتيب الخادم (برمز الحساب) هو ترتيب الميزان. وصريح على كل عمود لأن عمودَي
// الرمز والاسم ذوا `accessor` فيُفرزان لولاه
const UNSORTABLE = { enableSorting: false } as const;

// ‏كل مبلغ عبر `<MoneyDisplay>` بـ`amountBase` و`currencyId` **من كائنه هو** لا من الرأس
// ‏(R-UI-02، R-RPT-05): المبلغ يحمل عملته، ولا يستعيرها من غيره
const COLUMNS: GridColumn<Row>[] = [
  { ...UNSORTABLE, accessorKey: "accountCode", header: "رمز الحساب" },
  { ...UNSORTABLE, accessorKey: "accountName", header: "اسم الحساب" },
  {
    ...UNSORTABLE,
    id: "debit",
    header: "مدين",
    cell: ({ row }) => (
      <MoneyDisplay amount={row.original.debitBalance.amountBase} currencyId={row.original.debitBalance.currencyId} />
    )
  },
  {
    ...UNSORTABLE,
    id: "credit",
    header: "دائن",
    cell: ({ row }) => (
      <MoneyDisplay amount={row.original.creditBalance.amountBase} currencyId={row.original.creditBalance.currencyId} />
    )
  }
];

function rowId(row: Row): string {
  return row.accountId;
}

// ‏**الواجهة لا تجمع ولا تطرح ولا تشتق (R-API-01):** الأرصدة والمجموعان من الخادم كما وصلت،
// والمجموعان يُعرضان حرفياً ولو خالفا مجموع الصفوف (`TB03`)
function Report(props: { trialBalance: TrialBalance }) {
  const { trialBalance } = props;
  const { totalDebit, totalCredit } = trialBalance.totals;

  return (
    <>
      {/* ‏R-RPT-02 والرمز من الاستجابة (R-RPT-04) — لا رمز مكتوب في الكود */}
      <p>{`الأساس: ${BASIS_LABELS[trialBalance.basis] ?? trialBalance.basis} (${trialBalance.baseCurrencyCode})`}</p>

      <DataGrid
        rows={trialBalance.rows}
        columns={COLUMNS}
        getRowId={rowId}
        emptyMessage="لا حركة مرحَّلة لهذا الفرع."
      />

      <section aria-label="المجاميع">
        <p>
          <span>إجمالي المدين:</span> <MoneyDisplay amount={totalDebit.amountBase} currencyId={totalDebit.currencyId} />
        </p>
        <p>
          <span>إجمالي الدائن:</span> <MoneyDisplay amount={totalCredit.amountBase} currencyId={totalCredit.currencyId} />
        </p>
      </section>
    </>
  );
}

// ‏الفرع **حالة محلية بلا نموذج**: لا إرسال هنا ولا تحقق، فـ`<ZodForm>` بلا مستهلك.
// ‏وأربع حالات متمايزة لا تتداخل: لا فرع، تحميل، فشل، نجاح — والفراغ **نجاحٌ بلا صفوف**
// يعلنه `<DataGrid>` نفسه، فلا يظهر مع التحميل ولا مع الفشل (نمط `B12`/`B13`)
export function TrialBalanceScreen() {
  const [branchId, setBranchId] = useState<string | null>(null);
  const { status, trialBalance, error } = useTrialBalance(branchId);

  return (
    <>
      <h1>ميزان المراجعة</h1>

      <BranchPicker value={branchId} onChange={setBranchId} />

      {branchId === null ? (
        <p>اختر فرعاً لعرض ميزان المراجعة.</p>
      ) : status === "error" && error !== null ? (
        <ApiErrorMessage error={error} />
      ) : trialBalance === null ? (
        <p aria-busy="true">جارٍ تحميل الميزان…</p>
      ) : (
        <Report trialBalance={trialBalance} />
      )}
    </>
  );
}
