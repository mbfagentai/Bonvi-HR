import type { ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

/**
 * Sahifa boshi: katta sarlavha, ostida izoh, o'ngda harakatlar.
 * iOS'ning "Large Title" naqshi.
 */
export function PageHeader({
  title,
  subtitle,
  actions,
  className,
}: {
  title: ReactNode
  subtitle?: ReactNode
  actions?: ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex flex-wrap items-start justify-between gap-4', className)}>
      <div className="min-w-0">
        <h1 className="text-title1 leading-tight font-semibold tracking-tight">{title}</h1>
        {subtitle && (
          <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">{subtitle}</p>
        )}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  )
}
