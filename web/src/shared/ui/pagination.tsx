import { ChevronLeft, ChevronRight } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

/**
 * Sahifalash.
 *
 * NEGA raqamlar bor (faqat «oldinga/orqaga» emas): ko'chirishdan keyin
 * 2300 dan ortiq xodim bo'ladi va 50 tadan 46 sahifa chiqadi — HR
 * «taxminan o'rtada edi» deb eslaydi va o'sha yerga bir bosishda
 * tushishi kerak. Chekka sahifalar doim ko'rinadi, o'rtasi uch nuqta
 * bilan qisqaradi — kenglik o'zgarmaydi va tugmalar sakramaydi.
 */
export function Pagination({
  page,
  pageSize,
  total,
  onPageChange,
  disabled = false,
  className,
}: {
  /** 1 dan boshlanadi. */
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
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
      <p className="tabular text-caption1 text-[var(--fg-secondary)]">
        {t('employees.pageRange', {
          from: num(from),
          to: num(to),
          total: num(total),
        })}
      </p>

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
