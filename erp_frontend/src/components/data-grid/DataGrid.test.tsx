import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { DataGrid } from "./DataGrid";
import type { GridColumn } from "./DataGrid";
import type { components } from "../../../api-types/schema";

// ‏مصفوفة الحالات G (الشبكة العامة).
//
// أول مصدر بيانات حقيقي: `AccountResponseDto` — الأعمدة منه لا من خيال. وقد قِيس
// أنه **بلا حقل مبلغ** (كنسة على `amount|balance|debit|credit|total|cost|price`
// وعلى أي نمط عشري ⟵ صفر)، فحالة `<MoneyDisplay>` مؤجَّلة دَيناً مرقَّماً (الفجوة ٨،
// الحالة `G07`)، و`G05` تحرس **غياب المنافس** بدلاً منها.

type AccountRow = components["schemas"]["AccountResponseDto"];

const BASE = {
  companyId: "0199a1f0-0000-7000-8000-000000000001",
  accountType: 1,
  normalBalance: 0,
  isPostable: true,
  isActive: true
} as const;

// ‏غير مرتّبة عمداً: بلا ذلك لا يُميَّز الفرز عن ترتيب الإدخال
const ROWS: AccountRow[] = [
  { ...BASE, id: "0199a1f0-0000-7000-8000-0000000000a3", code: "2010", name: "المورّدون" },
  { ...BASE, id: "0199a1f0-0000-7000-8000-0000000000a1", code: "1010", name: "الصندوق" },
  { ...BASE, id: "0199a1f0-0000-7000-8000-0000000000a2", code: "1020", name: "المصرف" }
];

const COLUMNS: GridColumn<AccountRow>[] = [
  { accessorKey: "code", header: "رمز الحساب" },
  { accessorKey: "name", header: "اسم الحساب" }
];

function headerTexts(): string[] {
  return screen.getAllByRole("columnheader").map((cell) => cell.textContent ?? "");
}

// ‏الرأس أول صف، فيُسقَط. والقراءة بالأدوار لا بالوسوم: تصف ما يراه المستخدم
// وقارئ الشاشة، ولا تنكسر إن تغيّرت بنية الوسوم الداخلية
function bodyRowTexts(): string[][] {
  return screen
    .getAllByRole("row")
    .slice(1)
    .map((row) => within(row).getAllByRole("cell").map((cell) => cell.textContent ?? ""));
}

describe("DataGrid — G (الشبكة العامة، أول مصدر: شجرة الحسابات)", () => {
  it("G01: يعرض الصفوف المُمرَّرة كلها بأعمدتها، بلا فقدان ولا تكرار", () => {
    render(<DataGrid rows={ROWS} columns={COLUMNS} emptyMessage="لا حسابات." />);

    // ‏مطابقة واحدة تغطي العدد والترتيب والمحتوى معاً: صفٌّ ساقط أو مكرَّر يُسقطها
    expect(bodyRowTexts()).toEqual([
      ["2010", "المورّدون"],
      ["1010", "الصندوق"],
      ["1020", "المصرف"]
    ]);
  });

  it("G02: رؤوس الأعمدة بعناوينها وبترتيبها", () => {
    render(<DataGrid rows={ROWS} columns={COLUMNS} emptyMessage="لا حسابات." />);

    // ‏الترتيب جزء من العقد لا زينة: قلبه في جدول RTL يجعل القراءة كذباً
    expect(headerTexts()).toEqual(["رمز الحساب", "اسم الحساب"]);
  });

  it("G03: قائمة فارغة ⟵ رسالة صريحة لا جدول صامت", () => {
    render(<DataGrid rows={[]} columns={COLUMNS} emptyMessage="لا حسابات مطابقة." />);

    // ‏سلسلة `Q` جعلت `[]` حالة **مرجَّحة** لا نادرة: `data: null` تُرجَع قائمة فارغة
    // بنجاح. وجسم فارغ صامت لا يُميَّز عن تحميل فاشل
    expect(screen.queryByText("لا حسابات مطابقة.")).not.toBeNull();
    expect(bodyRowTexts()).toEqual([]);
  });

  it("G04: الفرز يغيّر العرض ولا يمسّ المصفوفة المُمرَّرة", () => {
    const passed = [...ROWS];

    render(<DataGrid rows={passed} columns={COLUMNS} emptyMessage="لا حسابات." />);

    fireEvent.click(screen.getAllByRole("columnheader")[0]!);

    expect(bodyRowTexts().map((cells) => cells[0])).toEqual(["1010", "1020", "2010"]);

    // ‏`Array.prototype.sort` **يبدّل في مكانه**، فتنفيذ ساذج يفرز `rows` مباشرةً
    // يُفسد مصفوفة المستهلك بلا أن يظهر شيء على الشاشة
    expect(passed).toEqual(ROWS);
  });

  it("G05: الشبكة لا تنسّق شيئاً — تعرض ما تُرجعه تعريفة العمود حرفياً", () => {
    // ‏مخرَج لا تُنتجه الشبكة من تلقائها بحال. وهو **بديل حالة `<MoneyDisplay>`**
    // التي سقطت بالقياس: يحرس أن لا طبقة تنسيق في الشبكة تنافس المكوّن المالي متى
    // وصل عمود مبلغ في §4.5 (الفجوة ٨)
    const columns: GridColumn<AccountRow>[] = [
      { accessorKey: "code", header: "رمز الحساب", cell: (info) => `«${String(info.getValue())}»` }
    ];

    render(<DataGrid rows={ROWS} columns={columns} emptyMessage="لا حسابات." />);

    expect(bodyRowTexts().map((cells) => cells[0])).toEqual(["«2010»", "«1010»", "«1020»"]);
  });

  it("G06: نقر الصف يُمرّر الصف الأصلي نفسه، بوسيط واحد لا أكثر", () => {
    const onRowClick = vi.fn();

    render(
      <DataGrid rows={ROWS} columns={COLUMNS} emptyMessage="لا حسابات." onRowClick={onRowClick} />
    );

    fireEvent.click(screen.getAllByRole("row")[1]!);

    expect(onRowClick).toHaveBeenCalledTimes(1);

    // ‏درس `F01` معمَّماً: TanStack تلفّ الصفوف في `Row<T>`، وتمريرها — أو تمرير
    // ‏`(row, event)` — يضع داخليات المكتبة في عقد المستهلك
    expect(onRowClick.mock.calls[0]).toHaveLength(1);

    // ‏**الهوية المرجعية** لا التساوي العميق: الأخير يمرّ على نسخة تبنيها الشبكة،
    // والمستهلك قد يوازن بالمرجع (`===`) ليعرف الصف المحدَّد
    expect(onRowClick.mock.calls[0]?.[0]).toBe(ROWS[0]);
  });

  // ‏🔒 `G07` محجوزة (حارس `<MoneyDisplay>` في الشبكة، الفجوة ٨) — لا يُعاد رقمها

  // ‏حقل **غير متحكَّم به** في الخلية هو الكاشف: حالته في عنصر DOM لا في البيانات،
  // فلا يتبع صفّه إلا إن تبعت هويةُ المكوّن هويةَ الصف. والحقل المتحكَّم به يُعاد
  // رسمه من القيمة أياً كان المفتاح، فلا يكشف شيئاً
  it("G08: getRowId ⟵ حذف صفّ من المنتصف لا يُزيح حالة الخلايا إلى صفّ آخر", () => {
    const columns: GridColumn<AccountRow>[] = [
      { accessorKey: "code", header: "رمز الحساب" },
      { id: "note", header: "ملاحظة", cell: () => <input aria-label="ملاحظة" /> }
    ];

    const { rerender } = render(
      <DataGrid rows={ROWS} columns={columns} emptyMessage="لا حسابات." getRowId={(row) => row.id} />
    );

    fireEvent.change(screen.getAllByLabelText("ملاحظة")[2]!, { target: { value: "للصفّ الثالث" } });

    rerender(
      <DataGrid rows={[ROWS[0]!, ROWS[2]!]} columns={columns} emptyMessage="لا حسابات." getRowId={(row) => row.id} />
    );

    const notes = screen.getAllByLabelText("ملاحظة");

    // ‏الشرط الموجب أولاً: الصفّ الثاني الآن هو الثالث سابقاً (1020)
    expect(bodyRowTexts().map((cells) => cells[0])).toEqual(["2010", "1020"]);
    expect(notes.map((note) => (note as HTMLInputElement).value)).toEqual(["", "للصفّ الثالث"]);
  });

  it("G09: getRowId يتلقى الصفّ الأصلي بوسيط واحد", () => {
    const getRowId = vi.fn((row: AccountRow) => row.id);

    render(<DataGrid rows={ROWS} columns={COLUMNS} emptyMessage="لا حسابات." getRowId={getRowId} />);

    // ‏درس `G06`: توقيع المكتبة `(originalRow, index, parent)` — وتمرير الفهرس يغري
    // المستهلك بالرجوع إليه، وهو عين ما وُجد `getRowId` لتجنّبه
    expect(getRowId.mock.calls.length).toBeGreaterThan(0);

    for (const call of getRowId.mock.calls) {
      expect(call).toHaveLength(1);
    }

    expect(getRowId.mock.calls.map((call) => call[0])).toEqual(expect.arrayContaining(ROWS));
  });
});
