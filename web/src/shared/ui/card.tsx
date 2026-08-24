import type { ComponentPropsWithoutRef, ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

/**
 * Asosiy sirt. iOS'da chuqurlik soya bilan emas, qatlam bilan beriladi:
 * fon kulrang — karta oq. Soya faqat "ozgina ko'tarilgan" degan ishora.
 */
export function Card({ className, ...props }: ComponentPropsWithoutRef<'div'>) {
  return (
    <div
      className={cn(
        'rounded-lg border shadow-card',
        'bg-[var(--bg-elevated)] border-[var(--border)]',
        className,
      )}
      {...props}
    />
  )
}

export function CardHeader({
  title,
  subtitle,
  action,
  className,
}: {
  title: ReactNode
  subtitle?: ReactNode
  action?: ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex items-start justify-between gap-4 px-5 pt-5 pb-4', className)}>
      <div className="min-w-0">
        <h2 className="text-callout leading-tight font-semibold">{title}</h2>
        {subtitle && (
          <p className="mt-1 text-footnote text-[var(--fg-secondary)]">{subtitle}</p>
        )}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  )
}

export function CardContent({ className, ...props }: ComponentPropsWithoutRef<'div'>) {
  return <div className={cn('px-5 pb-5', className)} {...props} />
}

/** Kartalar orasidagi nozik ajratgich. */
export function CardDivider({ className }: { className?: string }) {
  return <div className={cn('h-px bg-[var(--border)]', className)} />
}
