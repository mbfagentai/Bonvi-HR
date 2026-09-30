import clsx from 'clsx'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

import { Icon } from './icon'

/**
 * iOS uslubidagi guruhlangan ro'yxat (UX §E.2).
 *
 * Qator balandligi kamida 52 px, ikki qatorli matnda 64 px — barmoq
 * uchun 44 px eng kam chegara (UX §D.8). Ajratgich chiziq matndan
 * boshlanadi, chetdan emas: shunda ro'yxat bitta blok bo'lib ko'rinadi.
 */
export function ListGroup({ title, children }: { title?: string; children: ReactNode }) {
  return (
    <section>
      {title ? (
        <h2 className="mb-2 px-1 text-footnote font-medium uppercase tracking-wide text-fg-secondary">
          {title}
        </h2>
      ) : null}
      <div className="overflow-hidden rounded-card bg-bg-elevated">{children}</div>
    </section>
  )
}

interface RowProps {
  label: string
  /** Ikkinchi qator — faqat shu yerda qisqartirishga ruxsat (UX §D.1). */
  secondary?: string
  /** O'ng tarafdagi qiymat. */
  value?: ReactNode
  to?: string
  onClick?: () => void
  danger?: boolean
  disabled?: boolean
  /** Chevron ko'rsatilsinmi — `to`/`onClick` bilan birga keladi. */
  chevron?: boolean
  children?: ReactNode
}

export function ListRow({
  label,
  secondary,
  value,
  to,
  onClick,
  danger = false,
  disabled = false,
  chevron,
  children,
}: RowProps) {
  const interactive = Boolean(to || onClick) && !disabled
  const showChevron = chevron ?? Boolean(to)

  const body = (
    <div
      className={clsx(
        'flex w-full min-w-0 items-center px-4',
        secondary ? 'min-h-[64px] py-3' : 'min-h-[52px] py-2',
        // Ajratgich: qo'shni qatorlar orasida, matn chizig'idan
        'border-t border-border first:border-t-0',
        interactive && 'active:bg-bg-pressed',
        disabled && 'opacity-60',
      )}
    >
      <div className="min-w-0 flex-1 text-left">
        <div
          className={clsx(
            'text-body server-text',
            danger ? 'text-danger-text' : 'text-fg',
          )}
        >
          {label}
        </div>
        {secondary ? (
          <div className="mt-0.5 truncate text-footnote text-fg-secondary">{secondary}</div>
        ) : null}
        {children}
      </div>

      {value !== undefined ? (
        <div className="ml-3 shrink-0 text-subhead text-fg-secondary">{value}</div>
      ) : null}

      {showChevron ? (
        <Icon name="chevron-right" className="ml-2 h-5 w-5 shrink-0 text-fg-tertiary" />
      ) : null}
    </div>
  )

  if (to && !disabled) {
    return (
      <Link to={to} className="block">
        {body}
      </Link>
    )
  }

  if (onClick && !disabled) {
    return (
      <button type="button" onClick={onClick} className="block w-full">
        {body}
      </button>
    )
  }

  return body
}
