import { cn } from '@/shared/lib/cn'

export interface SegmentOption<T extends string> {
  value: T
  label: string
  title?: string
}

/**
 * iOS segmented control: kulrang chuqurchada sirg'aluvchi oq "til".
 *
 * Qachon ishlatiladi: 2–5 ta o'zaro istisno variant orasidan tanlash
 * (til, davr, ko'rinish rejimi). Undan ko'p bo'lsa — select.
 */
export function SegmentedControl<T extends string>({
  options,
  value,
  onChange,
  size = 'md',
  className,
  'aria-label': ariaLabel,
}: {
  options: readonly SegmentOption<T>[]
  value: T
  onChange: (value: T) => void
  size?: 'sm' | 'md'
  className?: string
  'aria-label'?: string
}) {
  const activeIndex = Math.max(
    0,
    options.findIndex((o) => o.value === value),
  )

  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      className={cn(
        'relative isolate inline-grid rounded-full p-[3px]',
        'bg-[var(--bg-sunken)]',
        size === 'sm' ? 'h-8' : 'h-10',
        className,
      )}
      style={{ gridTemplateColumns: `repeat(${options.length}, minmax(0, 1fr))` }}
    >
      {/* Sirg'aluvchi til — CSS transform bilan, qayta render'siz */}
      <span
        aria-hidden
        className={cn(
          'absolute inset-y-[3px] left-[3px] -z-10 rounded-full',
          'bg-[var(--bg-elevated)] shadow-card',
          'transition-transform duration-250 ease-[var(--ease-ios-out)]',
        )}
        style={{
          width: `calc((100% - 6px) / ${options.length})`,
          transform: `translateX(${activeIndex * 100}%)`,
        }}
      />

      {options.map((option) => {
        const selected = option.value === value
        return (
          <button
            key={option.value}
            type="button"
            role="tab"
            aria-selected={selected}
            title={option.title ?? option.label}
            onClick={() => onChange(option.value)}
            className={cn(
              'z-10 rounded-full px-3 font-medium whitespace-nowrap',
              'transition-colors duration-200',
              size === 'sm' ? 'text-caption1' : 'text-footnote',
              selected
                ? 'text-[var(--fg)]'
                : 'text-[var(--fg-secondary)] hover:text-[var(--fg)]',
            )}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}
