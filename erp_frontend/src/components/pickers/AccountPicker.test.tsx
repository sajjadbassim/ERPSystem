import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { AccountPicker } from "./AccountPicker";
import type { AccountItem } from "../../api/useAccounts";
import { AuthProvider } from "../../auth/AuthProvider";
import { clearTokens, setTokens } from "../../auth/token-store";

// ‏مصفوفة الحالات S (المنتقيات).
//
// **مبنيّة بمعزل تامّ عن أي نداء حيّ** — الدَّين ٥ (المصادقة) مفتوح، و`fetch` مثبَّت
// هنا بقرار معلن حدوده مكتوبة في `FRONTEND-STATE.md`: يُثبَت أن المنتقي يعرض ويفلتر
// ويُصدر ما تعاقد عليه، **ولا يُثبت أن نقطة النهاية تعمل**.
//
// وقرار الدَّين ٣ مطبَّق: **تحميل كامل + فلترة محلية**. فـ`S02` تحرس أن الكتابة لا
// تُطلق نداءً، و`S08` تحرس أن «الكامل» يعني كل الصفحات لا الأولى (`MaxPageSize = 100`).

const BASE = {
  companyId: "0199a1f0-0000-7000-8000-000000000001",
  accountType: 1,
  normalBalance: 0,
  isPostable: true,
  isActive: true
} as const;

const ACCOUNTS: AccountItem[] = [
  { ...BASE, id: "0199a1f0-0000-7000-8000-0000000000a1", code: "1010", name: "الصندوق" },
  { ...BASE, id: "0199a1f0-0000-7000-8000-0000000000a2", code: "1020", name: "المصرف" },
  { ...BASE, id: "0199a1f0-0000-7000-8000-0000000000a3", code: "2010", name: "المورّدون" }
];

const LABELS = ACCOUNTS.map((account) => `${account.code} — ${account.name}`);

function createWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  // ‏`AuthProvider` أُضيف بعد `L08`: `useAllAccounts` صار مبوَّباً بـ
  // ‏`enabled: status === "authenticated"`، فبلا مزوّد يبقى «غير مسجَّل» ولا يجلب.
  // **تركيب سياق فقط — صفر تعديل على أي ادعاء في `S01`..`S09`**
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <AuthProvider>{children}</AuthProvider>
      </QueryClientProvider>
    );
  };
}

type Page = { items: AccountItem[]; hasNextPage: boolean };

// ‏يردّ بحسب `PageNumber` في الرابط لا بحسب ترتيب النداء: فيثبت `S08` أن الحلقة
// تطلب الصفحة الثانية فعلاً، وأن اسم المعامل `PageNumber` بحرفه المقيس من العقد
function stubPages(pages: Page[]) {
  const fetchMock = vi.fn(async (input: unknown) => {
    const requested = /PageNumber=(\d+)/u.exec(String(input));
    const page = pages[requested === undefined || requested === null ? 0 : Number(requested[1]) - 1];

    return {
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        data: { data: page?.items ?? [], hasNextPage: page?.hasNextPage ?? false }
      })
    };
  });

  vi.stubGlobal("fetch", fetchMock);

  return fetchMock;
}

function stubPendingFetch() {
  let release: () => void = () => {};

  const fetchMock = vi.fn(
    async () =>
      await new Promise<{ ok: boolean; status: number; json: () => Promise<unknown> }>((resolve) => {
        release = () =>
          resolve({
            ok: true,
            status: 200,
            json: async () => ({ success: true, data: { data: ACCOUNTS, hasNextPage: false } })
          });
      })
  );

  vi.stubGlobal("fetch", fetchMock);

  return { release: () => release() };
}

function renderPicker(value: string | null, onChange: (accountId: string | null) => void) {
  render(<AccountPicker value={value} onChange={onChange} />, { wrapper: createWrapper() });

  return screen.getByLabelText("الحساب");
}

// ‏`mouseDown` ثم `click`: Autocomplete يفتح على `mouseDown` لا على `click` وحده،
// و`fireEvent.click` لا يُطلق الأول. تفصيل أداة لا ادعاء — والحالات تقيس ما بعده
function openPopup(input: HTMLElement) {
  fireEvent.mouseDown(input);
  fireEvent.click(input);
}

async function openAndReadOptions(input: HTMLElement): Promise<string[]> {
  openPopup(input);

  const options = await screen.findAllByRole("option");

  return options.map((option) => option.textContent ?? "");
}

// ‏البذر تهيئة لا ادعاء: `AuthProvider` يشتقّ حالته الابتدائية من المخزن، فرمزٌ
// مبذور يجعله «مسجَّلاً» عند التركيب. وحذف هذا السطر يُرسب الحالات التسع — فشل
// **مغلق ومرئي** لا صامت
beforeEach(() => {
  setTokens({
    accessToken: "s-access",
    refreshToken: "s-refresh",
    accessTokenExpiresAt: "2026-09-09T10:00:00.0000000Z"
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe("AccountPicker — S (منتقي الحساب، بمعزل عن أي نداء حيّ)", () => {
  it("S01: يعرض الحسابات المُحمَّلة برمزها واسمها", async () => {
    stubPages([{ items: ACCOUNTS, hasNextPage: false }]);

    // ‏الرمز وحده غامض والاسم وحده أغمض — والحقلان مقيسان في `AccountResponseDto`
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);
  });

  it("S02: الكتابة تُصفّي المعروض بلا نداء ثانٍ", async () => {
    const fetchMock = stubPages([{ items: ACCOUNTS, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    await openAndReadOptions(input);
    const callsAfterLoad = fetchMock.mock.calls.length;

    fireEvent.change(input, { target: { value: "المصرف" } });

    await waitFor(async () =>
      expect((await screen.findAllByRole("option")).map((o) => o.textContent)).toEqual([LABELS[1]])
    );

    // ‏هذا التأكيد هو ما يفرّق «فلترة محلية» عن «بحث خادم» — بلاه تمرّ الحالتان
    expect(fetchMock.mock.calls.length).toBe(callsAfterLoad);
  });

  it("S03: مسح نصّ البحث يعيد القائمة كاملة", async () => {
    stubPages([{ items: ACCOUNTS, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    await openAndReadOptions(input);

    fireEvent.change(input, { target: { value: "المصرف" } });
    fireEvent.change(input, { target: { value: "" } });

    // ‏يثبت أن الفلترة **عرضٌ لا إتلاف**: تنفيذ يقلّص المصدر نفسه يمرّ من `S02`
    // وحدها ويسقط هنا
    await waitFor(async () =>
      expect((await screen.findAllByRole("option")).map((o) => o.textContent)).toEqual(LABELS)
    );
  });

  it("S04: اختيار حساب يُصدر معرّفه نصّاً لا الكائن", async () => {
    stubPages([{ items: ACCOUNTS, hasNextPage: false }]);
    const onChange = vi.fn();
    const input = renderPicker(null, onChange);

    await openAndReadOptions(input);
    fireEvent.click(await screen.findByRole("option", { name: LABELS[1]! }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange.mock.calls[0]?.[0]).toBe(ACCOUNTS[1]!.id);
  });

  it("S05: onChange بوسيط واحد لا أكثر", async () => {
    stubPages([{ items: ACCOUNTS, hasNextPage: false }]);
    const onChange = vi.fn();
    const input = renderPicker(null, onChange);

    await openAndReadOptions(input);
    fireEvent.click(await screen.findByRole("option", { name: LABELS[1]! }));

    // ‏هنا الخطر **افتراضيّ لا عَرَضيّ**: توقيع `onChange` في MUI Autocomplete هو
    // ‏`(event, value, reason, details)` — أربعة وسائط. فتمريره مباشرةً يسرّب حدث
    // ‏DOM وداخليات المكتبة إلى عقد المستهلك. درس `F01` و`G06` معمَّماً
    expect(onChange.mock.calls[0]).toHaveLength(1);
  });

  it("S06: فلترة بلا مطابقة ⟵ «لا نتائج مطابقة»", async () => {
    stubPages([{ items: ACCOUNTS, hasNextPage: false }]);
    const input = renderPicker(null, vi.fn());

    await openAndReadOptions(input);
    fireEvent.change(input, { target: { value: "لا يوجد حساب بهذا الاسم" } });

    // ‏قائمة صامتة لا تُميَّز عن عطل. ومع الفلترة المحلية هذه حالة يومية لا استثنائية
    await waitFor(() => expect(screen.queryByText("لا نتائج مطابقة.")).not.toBeNull());
  });

  it("S07: حالة تحميل صريحة أثناء الجلب الأول", async () => {
    const { release } = stubPendingFetch();
    const input = renderPicker(null, vi.fn());

    openPopup(input);

    // ‏بلا إعلان صريح يبدو المنتقي **فارغاً** فيستنتج المستخدم «لا حسابات» — نمط
    // الفشل نفسه الذي وُجدت `Q06` له
    await waitFor(() => expect(screen.queryByText("جارٍ تحميل الحسابات…")).not.toBeNull());

    release();

    await waitFor(() => expect(screen.queryByText("جارٍ تحميل الحسابات…")).toBeNull());
  });

  it("S08: التحميل يجمع كل الصفحات لا الأولى وحدها", async () => {
    const fetchMock = stubPages([
      { items: [ACCOUNTS[0]!, ACCOUNTS[1]!], hasNextPage: true },
      { items: [ACCOUNTS[2]!], hasNextPage: false }
    ]);

    // ‏`MaxPageSize = 100`. بلا الحلقة يختفي الحساب رقم 101 **بلا أي خطأ** — نتيجة
    // خاطئة صامتة، وهي الثمن المكتوب في الدَّين ٣
    expect(await openAndReadOptions(renderPicker(null, vi.fn()))).toEqual(LABELS);

    expect(fetchMock.mock.calls.map((call) => String(call[0]))).toEqual([
      expect.stringContaining("PageNumber=1"),
      expect.stringContaining("PageNumber=2")
    ]);
  });

  it("S09: تفريغ الاختيار يُصدر null", async () => {
    stubPages([{ items: ACCOUNTS, hasNextPage: false }]);
    const onChange = vi.fn();

    const input = renderPicker(ACCOUNTS[1]!.id, onChange);

    // ‏زرّ المسح في MUI مخفيّ بـ`visibility` ما لم يكن الحقل مركَّزاً، و`getByRole`
    // يستبعد ما ليس في شجرة الإتاحة. فالتركيز جزء من الرحلة الحقيقية لا حيلة اختبار
    fireEvent.focus(input);

    fireEvent.click(await screen.findByRole("button", { name: "مسح الاختيار" }));

    // ‏`null` لا `undefined` ولا `""`: المستهلك يفرّق «لا اختيار» عن «لم يُحمَّل بعد»
    expect(onChange).toHaveBeenCalledWith(null);
  });
});
