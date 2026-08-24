import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

/**
 * Bo'sh holat.
 *
 * «Ma'lumot yo'q» — bu xato emas, bu boshlanish nuqtasi. Shu sababli
 * ekran bo'sh qolmaydi: katta yumshoq ikona, bir qatorlik tushuntirish
 * va darhol bajarish mumkin bo'lgan harakat.
 *
 * Matn ranglari token orqali beriladi (shaffoflik emas) — tungi rejimda
 * ham to'liq o'qiladi.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
}: {
  icon: LucideIcon
  title: ReactNode
  description?: ReactNode
  action?: ReactNode
  className?: string
}) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center px-6 py-16 text-center',
        className,
      )}
    >
      {/* Ikona ikki qatlamli doira ichida — iOS'dagi «katta belgi» naqshi */}
      <span
        aria-hidden
        className="grid size-20 place-items-center rounded-full bg-[var(--bg-sunken)]"
      >
        <span className="grid size-14 place-items-center rounded-full bg-[var(--bg-elevated)] shadow-card">
          <Icon className="size-7 text-[var(--fg-tertiary)]" strokeWidth={1.6} />
        </span>
      </span>

      <p className="mt-5 text-callout font-semibold">{title}</p>

      {description !== undefined && (
        <p className="mt-2 max-w-[42ch] text-subhead text-[var(--fg-secondary)]">
          {description}
        </p>
      )}

      {action !== undefined && <div className="mt-6">{action}</div>}
    </div>
  )
}
