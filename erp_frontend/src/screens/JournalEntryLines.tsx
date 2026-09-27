import { createContext, useContext, useMemo } from "react";
import { useFieldArray, useWatch } from "react-hook-form";

import { DataGrid } from "../components/data-grid/DataGrid";
import type { GridColumn } from "../components/data-grid/DataGrid";
import { MoneyInput } from "../components/money/MoneyInput";
import type { MoneyInputValue } from "../components/money/MoneyInput";
import { AccountPicker } from "../components/pickers/AccountPicker";
import { CurrencyPicker } from "../components/pickers/CurrencyPicker";
import { ControlledField, TextField } from "../forms/ZodForm";
import { isRateLocked, newLine } from "../schemas/journal-line-schema";
import type { JournalEntryFormValues } from "../schemas/journal-line-schema";

// ‏ما يتغيّر في الشبكة يصل الخلايا **عبر سياق لا عبر إغلاق تعريفات الأعمدة.**
//
// ‏مقيس 2026-09-27 (`JE13`): أعمدة في `useMemo` تتبع `canRemove` و`baseCurrencyId`
// تُبنى من جديد عند تغيّر أيّهما، فتصير دالة كل خلية **نوع مكوّن جديداً** — فيعيد React
// تركيب كل خلية في كل سطر. أي أن `getRowId` كان يُهزم عند النزول من ثلاثة أسطر إلى
// اثنين، وعند وصول الشركة. فالأعمدة ثابتة على مستوى الوحدة، والمتغيّر هنا
type LinesContextValue = {
  baseCurrencyId: string | null;
  canRemove: boolean;
  remove: (index: number) => void;
};

const LinesContext = createContext<LinesContextValue>({ baseCurrencyId: null, canRemove: false, remove: () => {} });

function LineAccount(props: { index: number }) {
  return (
    <ControlledField<string>
      name={`lines.${props.index}.accountId`}
      render={(field) => (
        <AccountPicker
          forPosting
          value={field.value === "" ? null : field.value}
          onChange={(id) => field.onChange(id ?? "")}
        />
      )}
    />
  );
}

function LineCurrency(props: { index: number }) {
  return (
    <ControlledField<string>
      name={`lines.${props.index}.currencyId`}
      render={(field) => (
        <CurrencyPicker value={field.value === "" ? null : field.value} onChange={(id) => field.onChange(id ?? "")} />
      )}
    />
  );
}

// ‏القفل **لكل سطر** من عملته هو (قرار V08): `isRateLocked` تُستدعى بعملة هذا السطر
// لا بعملة سطر آخر ولا بحالة واحدة للشاشة. يحرسه `JE10`
function LineMoney(props: { index: number }) {
  const { baseCurrencyId } = useContext(LinesContext);

  const currencyId = useWatch<JournalEntryFormValues, `lines.${number}.currencyId`>({
    name: `lines.${props.index}.currencyId`
  });

  return (
    <ControlledField<MoneyInputValue>
      name={`lines.${props.index}.money`}
      render={(field) => (
        <MoneyInput
          value={field.value}
          currencyId={currencyId}
          isRateLocked={isRateLocked(currencyId, baseCurrencyId)}
          onChange={field.onChange}
        />
      )}
    />
  );
}

function LineRemove(props: { index: number }) {
  const { canRemove, remove } = useContext(LinesContext);

  return (
    <button type="button" disabled={!canRemove} onClick={() => remove(props.index)}>
      حذف السطر
    </button>
  );
}

// ‏الصفّ في الشبكة **مفتاح لا بيانات**: `fields` من `useFieldArray` لقطةٌ تبقى على قيمها
// الأولى، فالخلايا تقرأ القيم الحيّة من النموذج بالفهرس ولا تقرأ من الصفّ شيئاً
type LineRow = { id: string };

// ‏قرار 2026-09-27: لا فرز — ترتيب السطور ترتيب القيد. **وهو صريح على كل عمود** مع أن
// الأعمدة عرضية بلا `accessor` فلا تُفرز أصلاً: الإعلان يبقى إن أُضيف `accessor` غداً
const UNSORTABLE = { enableSorting: false } as const;

const COLUMNS: GridColumn<LineRow>[] = [
  { ...UNSORTABLE, id: "account", header: "الحساب", cell: ({ row }) => <LineAccount index={row.index} /> },
  { ...UNSORTABLE, id: "currency", header: "العملة", cell: ({ row }) => <LineCurrency index={row.index} /> },

  // ‏عمود واحد لا عمودا مدين ودائن: الجانب داخل `<MoneyInput>`، والتوزيع في
  // ‏`toLinesRequest` خارج أي مكوّن
  { ...UNSORTABLE, id: "money", header: "المبلغ", cell: ({ row }) => <LineMoney index={row.index} /> },
  {
    ...UNSORTABLE,
    id: "description",
    header: "بيان السطر",
    cell: ({ row }) => <TextField name={`lines.${row.index}.description`} label="بيان السطر" />
  },
  { ...UNSORTABLE, id: "remove", header: "", cell: ({ row }) => <LineRemove index={row.index} /> }
];

function lineRowId(row: LineRow): string {
  return row.id;
}

// ‏حالة الأسطر **في النموذج وحده** (`useFieldArray`) — لا حالة React موازية تنحرف عنه.
// ‏والمعرّف `field.id` تولّده المكتبة للصفّ، فهو هوية مستقرة لا فهرس (`getRowId`)
export function JournalEntryLines(props: { baseCurrencyId: string | null }) {
  const { fields, append, remove } = useFieldArray<JournalEntryFormValues, "lines">({ name: "lines" });

  // ‏الحدّ الأدنى سطران (50017): حذفٌ ينزل تحته قيدٌ يرفضه الخادم حتماً. للتجربة وحدها
  // ‏(R-API-05). ولا تأكيد حذف — لا قاعدة توجبه
  const canRemove = fields.length > 2;

  const context = useMemo(
    () => ({ baseCurrencyId: props.baseCurrencyId, canRemove, remove }),
    [props.baseCurrencyId, canRemove, remove]
  );

  return (
    <LinesContext.Provider value={context}>
      <section aria-label="سطور القيد">
        <DataGrid rows={fields} columns={COLUMNS} emptyMessage="لا سطور." getRowId={lineRowId} />

        <button type="button" onClick={() => append(newLine("debit"))}>
          إضافة سطر
        </button>
      </section>
    </LinesContext.Provider>
  );
}
