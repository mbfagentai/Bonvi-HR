import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { CSSProperties, KeyboardEvent as ReactKeyboardEvent, ReactNode } from 'react'
import { useTable, type RowData, type SortingState } from '@tanstack/react-table'
import { useVirtualizer } from '@tanstack/react-virtual'

import { cn } from '@/shared/lib/cn'
import { dataTableFeatures, type DataColumn, type DataColumnMeta } from './data-table-columns'
import { Skeleton } from './skeleton'
import { VirtualRowContext, type VirtualRowState } from './virtual-row'

/**
 * Shundan ko'p qatorda (faqat `fill` rejimida) qatorlar virtual chiziladi.
 *
 * NEGA chegara bor: 20/50/100 qatorli oddiy sahifa avvalgidek to'liq
 * chiziladi — virtual qatorlarning o'z narxi bor (ustun kengligi ko'rinib
 * turgan qatorlarga qarab hisoblanadi, brauzerning «sahifadan qidirish»i
 * ekrandan tashqaridagi qatorni topmaydi). U faqat «Barchasi» tanlanib,
 * ming-minglab qator kelganda o'zini oqlaydi: 5 000 qatorni birdaniga
 * chizish HR kompyuterida bir necha soniya «qotish» degani.
 */
const VIRTUALIZE_FROM = 150

/** Qator balandligining taxmini — haqiqiy balandlik chizilgach o'lchanadi. */
const ESTIMATED_ROW_HEIGHT = 45

export function DataTable<TData extends RowData>({
  columns,
  data,
  getRowId,
  loading = false,
  skeletonRows = 6,
  empty,
  onRowClick,
  rowClickable,
  rowLabel,
  sorting,
  onSortingChange,
  fill = false,
  className,
}: {
  columns: Array<DataColumn<TData>>
  data: TData[]
  getRowId: (row: TData, index: number) => string
  loading?: boolean
  skeletonRows?: number
  /** Yozuv yo'qligida ko'rsatiladigan blok (`EmptyState`). */
  empty?: ReactNode
  /** Berilmasa — qatorlar bosilmaydi (masalan ruxsat yo'q). */
  onRowClick?: (row: TData) => void
  /**
   * Qator bosiladimi — `onRowClick` berilganda ham ALOHIDA qator uchun
   * o'chirish mumkin (masalan tahrirlab bo'lmaydigan tizim qatori).
   * Berilmasa — hammasi bosiladi.
   */
  rowClickable?: (row: TData) => boolean
  /** Skrinriderlar uchun qator nomi. */
  rowLabel?: (row: TData) => string
  /**
   * SERVER TOMONDA saralash.
   *
   * Berilsa, jadval ma'lumotni O'ZI SARALAMAYDI (`manualSorting`) — u
   * serverdan allaqachon tartiblangan holda keladi. Berilmasa hammasi
   * avvalgidek: kichik ro'yxatlar klient tomonda saralanadi.
   *
   * NEGA kerak: sahifalangan ro'yxatda klient saralashi YOLG'ON natija
   * beradi — u faqat joriy 25 ta qatorni tartiblaydi, foydalanuvchi esa
   * butun ro'yxat saralandi deb o'ylaydi.
   */
  sorting?: SortingState
  onSortingChange?: (sorting: SortingState) => void
  /**
   * Jadval ota konteynerning qolgan balandligini egallaydi va qatorlar
   * O'Z ICHIDA aylanadi — ustun nomlari tepada qadalib turadi.
   *
   * Faqat keng va baland oynada (`frame` varianti, globals.css) va faqat
   * ota elementlar zanjiri balandlik bergan sahifada ishlaydi
   * (`data-list-frame`, `app-shell.tsx`). Boshqa joyda jadval avvalgidek
   * oddiy oqimda chiziladi.
   *
   * `[contain:size]` — jadval kartaning «eng kichik balandligi» ga
   * qo'shilmasin: aks holda karta hamma qatorni sig'dirish uchun cho'zilib,
   * ichki scroll umuman paydo bo'lmasdi. `min-h-40` esa past oynada ham
   * bir necha qatorlik joy qoldiradi — joy yetmasa, sahifa butunligicha
   * aylanadi, sahifalash kesilib qolmaydi.
   */
  fill?: boolean
  className?: string
}) {
  const controlled = sorting !== undefined

  const table = useTable({
    features: dataTableFeatures,
    columns,
    data,
    getRowId,
    ...(controlled
      ? {
          state: { sorting },
          manualSorting: true,
          // Serverga «tartibsiz» degan holat yubormaymiz: bosish faqat
          // yo'nalishni almashtiradi (o'sish ⇄ kamayish)
          enableSortingRemoval: false,
          onSortingChange: (updater) => {
            const next = typeof updater === 'function' ? updater(sorting) : updater
            onSortingChange?.(next)
          },
        }
      : {}),
  })

  const headerGroups = table.getHeaderGroups()
  const rows = table.getRowModel().rows
  const columnCount = table.getAllLeafColumns().length

  const onRowKeyDown = (event: ReactKeyboardEvent<HTMLTableRowElement>, row: TData) => {
    // Klaviatura bilan yuruvchi uchun qator tugma kabi ishlaydi
    if (event.key !== 'Enter' && event.key !== ' ') return
    event.preventDefault()
    if (rowClickable?.(row) === false) return
    onRowClick?.(row)
  }

  /**
   * Yozuv yo'q — ustunlar eng kichik kengligini TALAB QILMAYDI.
   *
   * Aks holda bo'sh ro'yxat xabari 1300 pikselli katak ichida markazga
   * tekislanardi, ya'ni ekrandan chiqib ketardi va uni ko'rish uchun
   * o'ngga aylantirish kerak bo'lardi.
   */
  const noRows = !loading && rows.length === 0 && empty !== undefined

  const { railRef, bodyRef, railWidth, onRailScroll, onBodyScroll } = useScrollRail()

  /*
    Virtual qatorlar: faqat ko'rinib turganlari (+ har tomonda 12 ta)
    DOM'da bo'ladi, qolganining o'rnini ikki bo'sh «oraliq» qator egallaydi.
    Scroll konteyneri — jadval o'rami (`bodyRef`), `main` emas: `fill`
    rejimida aynan u aylanadi.

    Kichik ekranda o'ram balandlik olmaydi va hamma qatorni sig'diradi —
    shunda virtualizator ham hamma qatorni «ko'rinadi» deb chizadi, ya'ni
    natija to'g'ri, faqat tejash yo'q.
  */
  const virtualized = fill && !loading && rows.length > VIRTUALIZE_FROM
  const virtualizer = useVirtualizer({
    count: virtualized ? rows.length : 0,
    getScrollElement: () => bodyRef.current,
    estimateSize: () => ESTIMATED_ROW_HEIGHT,
    // O'lchangan balandlik qator INDEKSIGA emas, yozuvga bog'lanadi —
    // boshqa sahifa yoki filtrda 7-qator boshqa odam
    getItemKey: (index) => rows[index]?.id ?? index,
    overscan: 12,
    enabled: virtualized,
  })
  const virtualItems = virtualized ? virtualizer.getVirtualItems() : null
  const visibleRows =
    virtualItems === null
      ? rows.map((row, index) => ({ row, index }))
      : virtualItems.flatMap((item) => {
          const row = rows[item.index]
          return row === undefined ? [] : [{ row, index: item.index }]
        })
  const rowState: VirtualRowState = !virtualized
    ? 'off'
    : virtualizer.isScrolling
      ? 'scrolling'
      : 'idle'
  const firstItem = virtualItems?.[0]
  const lastItem = virtualItems?.[virtualItems.length - 1]
  const padTop = firstItem?.start ?? 0
  const padBottom = lastItem === undefined ? 0 : virtualizer.getTotalSize() - lastItem.end

  return (
    <div className={cn('min-w-0', fill && 'frame:flex frame:min-h-40 frame:flex-1 frame:flex-col frame:[contain:size]', className)}>
      {/*
        Gorizontal scroll «relsi» — ro'yxatning TEPASIDA.

        NEGA takroriy scrollbar: jadval pastda ham aylanadi, lekin uning
        scrollbar'i 50 ta qatordan keyin — ekrandan tashqarida turadi.
        Touchpad'siz kompyuterda esa gorizontal aylantirishning yagona
        yo'li — scrollbar'ni sichqoncha bilan tortish. Rels tepada
        turgani uchun u bir vaqtning o'zida ikki ish qiladi: aylantiradi
        va «o'ngda yana ustunlar bor» deb ogohlantiradi.

        Sig'ib turgan jadvalda rels umuman chizilmaydi (`railWidth === 0`).
      */}
      {railWidth > 0 && (
        <div
          ref={railRef}
          onScroll={onRailScroll}
          aria-hidden
          className="scroll-rail h-[13px] overflow-x-scroll overflow-y-hidden border-b border-[var(--border)] bg-[var(--bg-sunken)]"
        >
          <div style={{ width: railWidth }} className="h-px" />
        </div>
      )}

      <div
        ref={bodyRef}
        onScroll={onBodyScroll}
        // Yangi sahifa/filtrda ro'yxat tepadan boshlanadi (`app-shell.tsx`)
        data-list-scroll={fill ? '' : undefined}
        className={cn(
          'overflow-x-auto',
          fill && 'frame:min-h-0 frame:flex-1 frame:overflow-y-auto',
          /*
            Brauzerning «scroll langari» o'chiriladi: tepadagi oraliq qator
            o'lchangan balandlik bilan o'zgarganda virtualizator joyni O'ZI
            tuzatadi, brauzer ham tuzatsa — ro'yxat ikki marta sakraydi.
          */
          virtualized && '[overflow-anchor:none]',
        )}
      >
        <table className="w-full border-collapse text-left">
          <thead>
            {headerGroups.map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => {
                  const meta = header.column.columnDef.meta
                  const sortable = header.column.getCanSort()
                  const sorted = header.column.getIsSorted()

                  return (
                    <th
                      key={header.id}
                      scope="col"
                      aria-sort={
                        !sortable
                          ? undefined
                          : sorted === 'asc'
                            ? 'ascending'
                            : sorted === 'desc'
                              ? 'descending'
                              : 'none'
                      }
                      style={headerWidthStyle(meta, noRows)}
                      className={cn(
                        'border-b border-[var(--border)] bg-[var(--bg-sunken)] px-3 py-2.5 first:pl-4 last:pr-4',
                        'text-caption2 font-semibold tracking-wider whitespace-nowrap',
                        'text-[var(--fg-secondary)] uppercase',
                        meta?.align === 'end' && 'text-right',
                        meta?.hideOnMobile === true && 'hidden md:table-cell',
                        /*
                          Qadalgan sarlavha. Chiziq SOYA bilan: `border-collapse`
                          jadvalida chegara katakka emas, jadval to'riga tegishli
                          va `sticky` katak bilan birga yurmaydi — ro'yxat
                          aylanganda chiziq tepaga chiqib ketardi.
                        */
                        fill &&
                          'frame:sticky frame:top-0 frame:z-[1] frame:shadow-[inset_0_-1px_0_var(--border)]',
                      )}
                    >
                      {header.isPlaceholder ? null : sortable ? (
                        <button
                          type="button"
                          onClick={header.column.getToggleSortingHandler()}
                          className={cn(
                            // `uppercase` qayta: Tailwind asosi tugmada `text-transform: none`
                            // qiladi — saralanadigan sarlavha qolganlaridan farq qilardi
                            'inline-flex items-center gap-1.5 rounded-xs uppercase transition-colors',
                            'hover:text-[var(--fg)]',
                            meta?.align === 'end' && 'flex-row-reverse',
                            sorted !== false && 'text-[var(--fg)]',
                          )}
                        >
                          <table.FlexRender header={header} />
                          <SortIcon direction={sorted} />
                        </button>
                      ) : (
                        <table.FlexRender header={header} />
                      )}
                    </th>
                  )
                })}
              </tr>
            ))}
          </thead>

          <tbody>
            {loading &&
              Array.from({ length: skeletonRows }, (_, index) => (
                <tr key={`skeleton-${index}`} className="border-b border-[var(--border)]">
                  {Array.from({ length: columnCount }, (__, cell) => (
                    <td key={`skeleton-cell-${cell}`} className="px-3 py-3 first:pl-4 last:pr-4">
                      {/* Kenglik ustundan ustunga o'zgaradi — bir xil to'rtburchaklar
                          qatori jadvalga emas, yuklanish chizig'iga o'xshab qoladi */}
                      <Skeleton
                        className={cn('h-4', cell === 0 ? 'w-[62%]' : 'w-[38%]')}
                      />
                    </td>
                  ))}
                </tr>
              ))}

            {!loading && rows.length === 0 && empty !== undefined && (
              <tr>
                <td colSpan={columnCount}>{empty}</td>
              </tr>
            )}

            {padTop > 0 && <SpacerRow height={padTop} columnCount={columnCount} />}

            {!loading &&
              visibleRows.map(({ row, index }) => {
                const original = row.original
                const clickable = onRowClick !== undefined && rowClickable?.(original) !== false

                return (
                  <tr
                    key={row.id}
                    ref={virtualized ? virtualizer.measureElement : undefined}
                    data-index={index}
                    {...(clickable
                      ? {
                          role: 'button',
                          tabIndex: 0,
                          'aria-label': rowLabel?.(original),
                          onClick: () => onRowClick(original),
                          onKeyDown: (event: ReactKeyboardEvent<HTMLTableRowElement>) =>
                            onRowKeyDown(event, original),
                        }
                      : {})}
                    className={cn(
                      'border-b border-[var(--border)] transition-colors last:border-b-0',
                      clickable && 'cursor-pointer hover:bg-[var(--bg-hover)]',
                      'focus-visible:bg-[var(--bg-hover)]',
                    )}
                  >
                    {row.getAllCells().map((cell) => {
                      const meta = cell.column.columnDef.meta

                      return (
                        <td
                          key={cell.id}
                          /*
                            ⭐ BIR QATOR (egasi 2026-10-22): katakdagi matn qatorga
                            bo'linmaydi — ism, lavozim, yo'nalish bir chiziqda.
                            Ustun matnga yetguncha kengayadi, sig'magan jadval
                            esa gorizontal aylanadi (tepadagi rels). HR katta
                            ekranda ishlaydi: eni muammo emas, 2 qatorga bo'lingan
                            yozuvlar esa ekranga ikki barobar kam odam sig'dirardi.
                            Uzun erkin matn (izoh, sabab) ustunning o'zida
                            `max-w-* truncate` bilan kesiladi.
                          */
                          className={cn(
                            'px-3 py-2 text-subhead whitespace-nowrap align-middle first:pl-4 last:pr-4',
                            meta?.align === 'end' && 'text-right',
                            meta?.hideOnMobile === true && 'hidden md:table-cell',
                          )}
                        >
                          {/* Virtual qatorda og'ir so'rov kechiktiriladi (`virtual-row.ts`) */}
                          <VirtualRowContext value={rowState}>
                            {meta?.maxWidth === undefined ? (
                              <table.FlexRender cell={cell} />
                            ) : (
                              <div
                                className="truncate"
                                style={{ maxWidth: meta.maxWidth }}
                                title={titleOf(cell.getValue())}
                              >
                                <table.FlexRender cell={cell} />
                              </div>
                            )}
                          </VirtualRowContext>
                        </td>
                      )
                    })}
                  </tr>
                )
              })}

            {padBottom > 0 && <SpacerRow height={padBottom} columnCount={columnCount} />}
          </tbody>
        </table>
      </div>
    </div>
  )
}

/** Kesilgan katakning to'liq matni — faqat qiymat matn bo'lsa. */
function titleOf(value: unknown): string | undefined {
  return typeof value === 'string' && value !== '' ? value : undefined
}

/** Virtual ro'yxatda ko'rinmayotgan qatorlar o'rnidagi bo'sh joy. */
function SpacerRow({ height, columnCount }: { height: number; columnCount: number }) {
  return (
    <tr aria-hidden style={{ height }}>
      <td colSpan={columnCount} className="p-0" />
    </tr>
  )
}

/**
 * Jadval tepasidagi gorizontal scroll relsi.
 *
 * Ikki element bitta o'qda yuradi: rels (tepada, faqat scrollbar) va
 * jadval o'rami (pastda, haqiqiy kontent). Ular bir-birini surib turadi.
 *
 * NEGA `scrollLeft !== ` sharti: qiymatni o'rnatish qarshi tomonda yana
 * `scroll` hodisasini uyg'otadi. Tenglik tekshiruvi shu zanjirni birinchi
 * qadamdayoq uzadi — brauzer o'zgarmagan qiymatga hodisa yubormaydi.
 *
 * `railWidth` — jadvalning to'liq kengligi, u o'ramga SIG'MAGANDAGINA.
 * Nol bo'lsa rels chizilmaydi: sig'ib turgan jadval tepasidagi bo'sh
 * scrollbar faqat chalg'itardi.
 *
 * Bog'liqliklar ro'yxati bo'sh: `ResizeObserver` ikkala tomonni ham
 * kuzatadi, ya'ni ustun qo'shilishi, oyna kengayishi va til almashishi —
 * hammasi o'lchovni o'zi qayta chaqiradi.
 */
function useScrollRail() {
  const railRef = useRef<HTMLDivElement>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const [railWidth, setRailWidth] = useState(0)

  useEffect(() => {
    const body = bodyRef.current
    if (body === null) return

    const measure = () => {
      // 1 piksel — yumaloqlash xatosi (zoom, kasrli kenglik) uchun bag'ri
      setRailWidth(body.scrollWidth > body.clientWidth + 1 ? body.scrollWidth : 0)
    }

    measure()

    // Kuzatuv o'ramda ham, jadvalning o'zida ham: birinchisi oyna
    // o'lchamini, ikkinchisi ustun tarkibi o'zgarishini tutadi
    const observer = new ResizeObserver(measure)
    observer.observe(body)
    if (body.firstElementChild !== null) observer.observe(body.firstElementChild)

    return () => observer.disconnect()
  }, [])

  const onRailScroll = useCallback(() => {
    const rail = railRef.current
    const body = bodyRef.current
    if (rail === null || body === null) return
    if (body.scrollLeft !== rail.scrollLeft) body.scrollLeft = rail.scrollLeft
  }, [])

  const onBodyScroll = useCallback(() => {
    const rail = railRef.current
    const body = bodyRef.current
    if (rail === null || body === null) return
    if (rail.scrollLeft !== body.scrollLeft) rail.scrollLeft = body.scrollLeft
  }, [])

  return { railRef, bodyRef, railWidth, onRailScroll, onBodyScroll }
}

/**
 * `<th>` uchun kenglik uslubi.
 *
 * `width` va `minWidth` — ATAYLAB ikki xil narsa:
 *   - `width` QAT'IY o'lcham beradi (`{ width, minWidth: width }`) — ustun
 *     brauzerning `table-layout: auto` ortgan-joy taqsimotidan CHIQIB
 *     ketadi va har doim aynan shu kenglikda chiziladi (keng ekranda ham
 *     kengaymaydi).
 *   - `minWidth` esa faqat PASTKI chegara (`{ minWidth }`, `width` YO'Q) —
 *     ustun moslashuvchan bo'lib qoladi: `width`li qo'shni ustunlar hech
 *     qachon o'zgarmaydi, ortgan joyning HAMMASINI esa moslashuvchan
 *     ustun(lar) o'zaro oladi. Shu farq tufayli, masalan, katalog
 *     jadvalidagi «nom» ustuniga `minWidth` berilsa ham, uning yonidagi
 *     piksellik `actions`/`dutyCount` ustunlari hech qachon shishmaydi
 *     (tekshirilgan: headless Chrome, 700/1100/1600px).
 *
 * Ikkalasi ham berilsa `minWidth` ustun turadi (`width` e'tiborga
 * olinmaydi) — chaqiruvchi ataylab ikkalasini bermasligi kerak.
 *
 * Yozuv yo'q bo'lsa (`noRows`) — ustun eng kichik kengligini TALAB
 * QILMAYDI, aks holda bo'sh ro'yxat xabari katakning o'rtasiga emas,
 * to'liq kengligiga tekislanib, ekrandan chiqib ketardi.
 */
function headerWidthStyle(
  meta: DataColumnMeta | undefined,
  noRows: boolean,
): CSSProperties | undefined {
  if (noRows) return undefined
  if (meta?.minWidth !== undefined) return { minWidth: meta.minWidth }
  if (meta?.width !== undefined) return { width: meta.width, minWidth: meta.width }
  return undefined
}

/**
 * Saralash yo'nalishi. Saralanmagan ustunda belgi xira — lekin ko'rinadi.
 *
 * `--accent` emas, `--accent-text`: `--accent` — bu OQ matn ostidagi
 * to'ldirilgan fon rangi. Bu yerda ikonkaning o'zi yorug' fonda turadi,
 * shu sababli tint'ning to'qroq pog'onasi kerak.
 */
function SortIcon({ direction }: { direction: false | 'asc' | 'desc' }) {
  if (direction === 'asc') {
    return <ChevronUp className="size-3.5 text-[var(--accent-text)]" strokeWidth={3} />
  }
  if (direction === 'desc') {
    return <ChevronDown className="size-3.5 text-[var(--accent-text)]" strokeWidth={3} />
  }
  return <ChevronsUpDown className="size-3.5 text-[var(--fg-tertiary)]" strokeWidth={2.5} />
}
