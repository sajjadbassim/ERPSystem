import {
  createCoreRowModel,
  createSortedRowModel,
  flexRender,
  rowSortingFeature,
  tableFeatures,
  useTable
} from "@tanstack/react-table";
import type { ColumnDef, RowData } from "@tanstack/react-table";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";

// ‏TanStack Table **v9** لا v8: التوقيع `ColumnDef<TFeatures, TData, TValue>` — وسيط
// «المزايا» أولاً — والـAPI `useTable` و`createCoreRowModel` لا `useReactTable`
// و`getCoreRowModel`. مقيس من الأنواع والتوثيق الرسمي للنسخة المثبَّتة، لا من
// المألوف عن نسخة سابقة (الدرس المنهجي في `FRONTEND-STATE.md`).
//
// ‏**ولم يُستعمل `useLegacyTable` التوافقي** رغم قبوله شكل v8: مساعِداته موسومة
// ‏`@deprecated` في الحزمة نفسها، والبناء على مسار مهجور يشتري راحة اليوم بهجرة غداً.
//
// والمزايا تُعلَن **مرة واحدة** ويُشتق منها نوع العمود، فلا يكرّرها كل مستهلك
// ولا تنحرف نسختان
const gridFeatures = tableFeatures({
  rowSortingFeature,
  coreRowModel: createCoreRowModel(),
  sortedRowModel: createSortedRowModel()
});

export type GridFeatures = typeof gridFeatures;

// ‏`RowData` قيد المكتبة على شكل الصف. يُمرَّر ولا يُعاد تعريفه محلياً: نسخة ثانية
// منه تنحرف عن المكتبة بصمت
export type GridColumn<TRow extends RowData> = ColumnDef<GridFeatures, TRow>;

export type DataGridProps<TRow extends RowData> = {
  // ‏`readonly` ليس زينة: هو الإعلان بأن الشبكة **لا تبدّل** مصفوفة المستهلك.
  // و`useTable` يقبل `ReadonlyArray` أصلاً، فالفرز يبني نموذجاً جديداً ولا يفرز
  // في مكانه — والحارس `G04` يقيس ذلك لا يفترضه
  rows: readonly TRow[];

  // ‏`ColumnDef` نوع المكتبة يظهر في عقد المستهلك — **تسريب مقصود معلَن**: لفّه الآن
  // تجريدٌ على محور مجهول. ويفترق عن تسريب `Row<T>` في مُعالج النقر الذي يحرسه `G06`
  columns: GridColumn<TRow>[];

  // ‏جسم فارغ لا يُميَّز عن تحميل فاشل، فالرسالة إلزامية لا اختيارية
  emptyMessage: string;

  onRowClick?: (row: TRow) => void;

  // ‏هوية الصفّ ومفتاحه في React. بلاه فالهوية **الفهرس**: حذف صفّ من المنتصف يُعطي
  // مكوّنات الصفّ المحذوف لما بعده، فتنتقل إليه كل حالة يحملها مكوّنٌ لا البيانات.
  // اختياري: شبكة للعرض وحده بلا حذف (شجرة الحسابات) لا تحتاجه. يحرسه `G08`
  getRowId?: (row: TRow) => string;
};

export function DataGrid<TRow extends RowData>(props: DataGridProps<TRow>) {
  const { getRowId } = props;

  const table = useTable({
    features: gridFeatures,
    columns: props.columns,
    data: props.rows,

    // ‏لفٌّ بوسيط واحد — درس `G06`: توقيع المكتبة `(originalRow, index, parent)`،
    // وتمرير الفهرس يغري المستهلك بالرجوع إليه. يحرسه `G09`
    ...(getRowId === undefined ? {} : { getRowId: (row: TRow) => getRowId(row) })
  });

  const rows = table.getRowModel().rows;

  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id}>
              {headerGroup.headers.map((header) => {
                const sorted = header.column.getIsSorted();

                return (
                  // ‏الفرز على **خلية الرأس كلها** لا على زرّ داخلها: هدف نقر أوسع،
                  // وبلا عنصر تفاعلي متداخل. و`sortDirection` يضع `aria-sort` فيبلغ
                  // قارئ الشاشة ما يبلغه المؤشّر البصري
                  <TableCell
                    key={header.id}
                    sortDirection={sorted === false ? false : sorted}
                    onClick={header.column.getToggleSortingHandler()}
                    sx={{
                      cursor: "pointer",
                      userSelect: "none",
                      // ‏المؤشّر عبر CSS لا كنصّ في الـDOM: نصٌّ إضافي كان يلوّث
                      // ‏`textContent` فيكسر مطابقة عناوين الأعمدة في `G02`
                      '&[aria-sort="ascending"]::after': { content: '" ▲"' },
                      '&[aria-sort="descending"]::after': { content: '" ▼"' }
                    }}
                  >
                    {flexRender(header.column.columnDef.header, header.getContext())}
                  </TableCell>
                );
              })}
            </TableRow>
          ))}
        </TableHead>

        <TableBody>
          {rows.map((row) => (
            <TableRow
              key={row.id}
              hover={props.onRowClick !== undefined}
              // ‏`row.original` لا `row`: الثاني لفافة المكتبة (`Row<T>`)، وتمريرها
              // تسريب داخلياتها إلى عقد المستهلك. ولفٌّ صريح بوسيط واحد يمنع عبور
              // حدث DOM كما جرى في `<ZodForm>` — يحرسهما `G06`
              onClick={() => props.onRowClick?.(row.original)}
            >
              {/* ‏`getAllCells` لا `getVisibleCells`: الأخيرة تخصّ ميزة إخفاء الأعمدة
                  وهي غير مسجَّلة — ولا تُسجَّل ميزة لا تُستعمل */}
              {row.getAllCells().map((cell) => (
                <TableCell key={cell.id}>
                  {/* ‏`flexRender` وحده: لا تنسيق من الشبكة. فمتى وصل عمود مبلغ،
                      يكون `<MoneyDisplay>` ما يضعه المستهلك ولا طبقة تنافسه
                      (الفجوة ٨). يحرسه `G05` */}
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {rows.length === 0 ? (
        <Typography role="status" sx={{ p: 2 }}>
          {props.emptyMessage}
        </Typography>
      ) : null}
    </TableContainer>
  );
}
