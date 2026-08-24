import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react'
import type { KeyboardEvent as ReactKeyboardEvent, ReactNode } from 'react'
import { useTable, type RowData, type SortingState } from '@tanstack/react-table'

import { cn } from '@/shared/lib/cn'
import { dataTableFeatures, type DataColumn } from './data-table-columns'
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

  return (
    <div className={cn('overflow-x-auto', className)}>
      {/* Tor ekranda `hideOnMobile` ustunlari yashiringani uchun jadval
          o'zi sig'adi; keng ekranda esa eng kichik kenglik qo'yiladi,
          aks holda ustunlar bir-biriga yopishib qoladi. */}
      <table className="w-full border-collapse text-left md:min-w-[620px]">
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
                    style={meta?.width === undefined ? undefined : { width: meta.width }}
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
  )
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
