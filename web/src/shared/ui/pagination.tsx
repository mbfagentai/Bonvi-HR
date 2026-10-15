import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { Check, ChevronDown, ChevronLeft, ChevronRight } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { PAGE_SIZE_ALL, PAGE_SIZE_OPTIONS } from '@/shared/lib/use-page-size'

/**
 * Sahifalash.
 *
 * NEGA raqamlar bor (faqat «oldinga/orqaga» emas): ko'chirishdan keyin
 * 2300 dan ortiq xodim bo'ladi va 50 tadan 46 sahifa chiqadi — HR
 * «taxminan o'rtada edi» deb eslaydi va o'sha yerga bir bosishda
 * tushishi kerak. Chekka sahifalar doim ko'rinadi, o'rtasi uch nuqta
 * bilan qisqaradi — kenglik o'zgarmaydi va tugmalar sakramaydi.
 *
 * `onPageSizeChange` berilsa, yozuvlar soni yonida «Sahifada: 50 ⌄»
 * tanlovi chiqadi (`usePageSize`). Uni beradigan ro'yxat pagination'ni
 * `total > 0` bo'lganda chizishi kerak, `total > pageSize` da emas —
 * aks holda 100 ni tanlagan HR 52 qatorli ro'yxatda tanlovni qaytib
 * topolmaydi.
 */
export function Pagination({
  page,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
  allowAll = false,
  disabled = false,
  className,
}: {
  /** 1 dan boshlanadi. */
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
  /** Berilmasa — sahifa hajmi tanlovi chizilmaydi. */
  onPageSizeChange?: (pageSize: number) => void
  /** Tanlovda «Barchasi» bandi bo'ladimi. */
  allowAll?: boolean
  disabled?: boolean
  className?: string
}) {
  const { t, num } = useLocale()

  const pageCount = Math.max(1, Math.ceil(total / Math.max(1, pageSize)))
  const current = Math.min(Math.max(1, page), pageCount)

  const from = total === 0 ? 0 : (current - 1) * pageSize + 1
  const to = Math.min(current * pageSize, total)

  return (
    <div
      className={cn(
        'flex flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-5',
        className,
      )}
    >
      <div className="flex items-center gap-3">
        <p className="tabular text-caption1 text-[var(--fg-secondary)]">
          {t('employees.pageRange', {
            from: num(from),
            to: num(to),
            total: num(total),
          })}
        </p>

        {onPageSizeChange !== undefined && (
          <PageSizeMenu pageSize={pageSize} allowAll={allowAll} onChange={onPageSizeChange} />
        )}
      </div>

      {pageCount > 1 && (
        <nav aria-label={t('employees.pagination')} className="flex items-center gap-1">
          <Arrow
            direction="prev"
            label={t('employees.prevPage')}
            disabled={disabled || current === 1}
            onClick={() => onPageChange(current - 1)}
          />

          {pageWindow(current, pageCount).map((item, index) =>
            item === null ? (
              <span
                // Uch nuqta ikkitagacha bo'lishi mumkin (chapda va o'ngda),
                // shu sababli kalitda o'rni ham bor
                key={`gap-${index}`}
                aria-hidden
                className="px-1 text-footnote text-[var(--fg-tertiary)]"
              >
                …
              </span>
            ) : (
              <button
                key={item}
                type="button"
                disabled={disabled}
                aria-current={item === current ? 'page' : undefined}
                onClick={() => onPageChange(item)}
                className={cn(
                  'tabular grid h-9 min-w-9 place-items-center rounded-md px-2',
                  'text-footnote font-medium',
                  // Tugma bo'lgani uchun Button bilan bir xil qoida:
                  // bosilganda rang to'qlashadi, o'lchami o'zgarmaydi (iOS HIG «Buttons»).
                  'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
                  'disabled:pointer-events-none disabled:opacity-40',
                  item === current
                    ? 'bg-[var(--accent)] text-[var(--fg-on-accent)] active:bg-[var(--accent-pressed)]'
                    : 'text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)] active:bg-[var(--bg-active)]',
                )}
              >
                {num(item)}
              </button>
            ),
          )}

          <Arrow
            direction="next"
            label={t('employees.nextPage')}
            disabled={disabled || current === pageCount}
            onClick={() => onPageChange(current + 1)}
          />
        </nav>
      )}
    </div>
  )
}

/**
 * «Sahifada: 50 ⌄» — sahifa hajmi tanlovi.
 *
 * Shakli davomat statistikasidagi tanlov (`attendance-dashboard-page.tsx`)
 * va til menyusi bilan bir xil: Radix menyusi, belgilangan band ✓ bilan.
 */
function PageSizeMenu({
  pageSize,
  allowAll,
  onChange,
}: {
  pageSize: number
  allowAll: boolean
  onChange: (pageSize: number) => void
}) {
  const { t, num } = useLocale()

  const sizes: number[] = allowAll ? [...PAGE_SIZE_OPTIONS, PAGE_SIZE_ALL] : [...PAGE_SIZE_OPTIONS]
  const label = (size: number) => (size === PAGE_SIZE_ALL ? t('common.pageSizeAll') : num(size))

  return (
    <DropdownMenu.Root modal={false}>
      <DropdownMenu.Trigger asChild>
        <button
          type="button"
          aria-label={`${t('common.pageSize')}: ${label(pageSize)}`}
          className={cn(
            'group inline-flex h-8 items-center gap-1 rounded-md px-2 text-caption1',
            'text-[var(--fg-secondary)] transition-colors duration-[120ms] ease-[var(--ease-ios)]',
            'hover:bg-[var(--bg-hover)] hover:text-[var(--fg)] active:bg-[var(--bg-active)]',
            'data-[state=open]:bg-[var(--bg-hover)] data-[state=open]:text-[var(--fg)]',
          )}
        >
          <span>{t('common.pageSize')}:</span>
          <span className="tabular font-semibold text-[var(--fg)]">{label(pageSize)}</span>
          <ChevronDown
            className="size-3.5 text-[var(--fg-tertiary)] transition-transform duration-200 group-data-[state=open]:rotate-180"
            strokeWidth={2.5}
          />
        </button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="start"
          side="top"
          sideOffset={6}
          className={cn(
            'pop-panel z-50 min-w-[148px] rounded-xl border p-1.5 outline-none',
            'border-[var(--border)] bg-[var(--bg-elevated)] shadow-floating',
          )}
        >
          <DropdownMenu.RadioGroup
            value={String(pageSize)}
            onValueChange={(value) => onChange(Number(value))}
          >
            {sizes.map((size) => (
              <DropdownMenu.RadioItem
                key={size}
                value={String(size)}
                className={cn(
                  'flex h-9 cursor-pointer items-center gap-2.5 rounded-md px-2.5 text-subhead outline-none select-none',
                  'transition-colors duration-100 data-[highlighted]:bg-[var(--bg-hover)]',
                )}
              >
                <span className="tabular flex-1">{label(size)}</span>
                <DropdownMenu.ItemIndicator>
                  <Check className="size-4 text-[var(--accent-on-soft)]" strokeWidth={3} />
                </DropdownMenu.ItemIndicator>
              </DropdownMenu.RadioItem>
            ))}
          </DropdownMenu.RadioGroup>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

function Arrow({
  direction,
  label,
  disabled,
  onClick,
}: {
  direction: 'prev' | 'next'
  label: string
  disabled: boolean
  onClick: () => void
}) {
  const Icon = direction === 'prev' ? ChevronLeft : ChevronRight

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={label}
      aria-label={label}
      className={cn(
        'grid size-9 place-items-center rounded-md text-[var(--fg-secondary)]',
        'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
        'hover:bg-[var(--bg-hover)] hover:text-[var(--fg)] active:bg-[var(--bg-active)]',
        'disabled:pointer-events-none disabled:opacity-30',
      )}
    >
      <Icon className="size-[18px]" strokeWidth={2.5} />
    </button>
  )
}

/**
 * Ko'rinadigan sahifa raqamlari. `null` — uch nuqta.
 *
 * Doim eng ko'pi bilan 7 ta katak: birinchi, oxirgi, joriy va uning
 * ikki qo'shnisi. Shu sababli tugmalar qatori kengligi o'zgarmaydi.
 */
function pageWindow(current: number, count: number): Array<number | null> {
  if (count <= 7) {
    return Array.from({ length: count }, (_, index) => index + 1)
  }

  const pages = new Set<number>([1, count, current])
  for (const offset of [-1, 1]) {
    const neighbour = current + offset
    if (neighbour > 1 && neighbour < count) pages.add(neighbour)
  }

  // Chekkalarda oyna qisqarib qolmasligi uchun to'ldiriladi
  if (current <= 3) {
    pages.add(2).add(3).add(4)
  }
  if (current >= count - 2) {
    pages.add(count - 1).add(count - 2).add(count - 3)
  }

  const sorted = [...pages].filter((page) => page >= 1 && page <= count).sort((a, b) => a - b)

  const result: Array<number | null> = []
  let previous = 0

  for (const page of sorted) {
    if (previous !== 0 && page - previous > 1) result.push(null)
    result.push(page)
    previous = page
  }

  return result
}
