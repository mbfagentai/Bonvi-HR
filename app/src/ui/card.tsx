import clsx from 'clsx'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

/**
 * Karta (UX §E.2).
 *
 * Chuqurlik — SIRT o'zgarishi, soya emas: iOS qatlam bilan ishlaydi,
 * har bir kartaga soya qo'yish esa Android davridan qolgan odat.
 */
interface CardProps {
  title?: string
  /** O'ng tarafdagi bitta havola — odatda «Barchasi». */
  action?: { label: string; to: string }
  className?: string
  children: ReactNode
}

export function Card({ title, action, className, children }: CardProps) {
  return (
    <section className={clsx('rounded-card bg-bg-elevated p-4', className)}>
      {title || action ? (
        <div className="mb-3 flex items-center justify-between">
          {title ? <h2 className="text-title3 font-semibold text-fg">{title}</h2> : <span />}
          {action ? (
            <Link
              to={action.to}
              className="ml-3 shrink-0 text-footnote font-medium text-accent-text"
            >
              {action.label}
            </Link>
          ) : null}
        </div>
      ) : null}

      {children}
    </section>
  )
}

/** Kartadagi «yorliq → qiymat» qatori. Yorliq bilan qiymat — bitta obyekt. */
export function CardRow({
  label,
  value,
  valueClassName,
}: {
  label: string
  value: ReactNode
  valueClassName?: string
}) {
  return (
    <div className="flex min-w-0 items-baseline justify-between py-1">
      <span className="mr-3 shrink-0 text-footnote text-fg-secondary">{label}</span>
      <span className={clsx('min-w-0 text-right text-subhead text-fg server-text', valueClassName)}>
        {value}
      </span>
    </div>
  )
}
