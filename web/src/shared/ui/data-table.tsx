import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { CSSProperties, KeyboardEvent as ReactKeyboardEvent, ReactNode } from 'react'
import { useTable, type RowData, type SortingState } from '@tanstack/react-table'

import { cn } from '@/shared/lib/cn'
import { dataTableFeatures, type DataColumn, type DataColumnMeta } from './data-table-columns'
import { Skeleton } from './skeleton'

export function DataTable<TData extends RowData>({
  columns,
  data,
  getRowId,
  loading = false,
  skeletonRows = 6,
  empty,
  onRowClick,
  rowLabel,
  sorting,
  onSortingChange,
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

  return (
    <div className={cn('min-w-0', className)}>
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

      <div ref={bodyRef} onScroll={onBodyScroll} className="overflow-x-auto">
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
                        'border-b border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-2.5',
                        'text-caption2 font-semibold tracking-wider whitespace-nowrap',
                        'text-[var(--fg-secondary)] uppercase',
                        meta?.align === 'end' && 'text-right',
                        meta?.hideOnMobile === true && 'hidden md:table-cell',
                      )}
                    >
                      {header.isPlaceholder ? null : sortable ? (
                        <button
                          type="button"
                          onClick={header.column.getToggleSortingHandler()}
                          className={cn(
                            'inline-flex items-center gap-1.5 rounded-xs transition-colors',
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
                    <td key={`skeleton-cell-${cell}`} className="px-4 py-3.5">
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

            {!loading &&
              rows.map((row) => {
                const original = row.original
                const clickable = onRowClick !== undefined

                return (
                  <tr
                    key={row.id}
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
                          className={cn(
                            'px-4 py-3 text-subhead align-middle',
                            meta?.align === 'end' && 'text-right',
                            meta?.hideOnMobile === true && 'hidden md:table-cell',
                          )}
                        >
                          <table.FlexRender cell={cell} />
                        </td>
                      )
                    })}
                  </tr>
                )
              })}
          </tbody>
        </table>
      </div>
    </div>
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
