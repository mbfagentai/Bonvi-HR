import clsx from 'clsx'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { Button } from './button'
import { Icon, type IconName } from './icon'

/**
 * To'rtta umumiy holat (UX §E.9) shu faylda yashaydi: ular butun
 * ilovada BIR XIL ko'rinadi. Ekranga xos farq faqat MATNDA bo'ladi,
 * naqshda emas.
 */
type Tone = 'info' | 'warning' | 'danger' | 'success'

const TONE_TINT: Record<Tone, string> = {
  info: 'bg-accent-soft',
  warning: 'bg-tint-late',
  danger: 'bg-tint-absent',
  success: 'bg-tint-success',
}

const TONE_ICON: Record<Tone, IconName> = {
  info: 'information-circle',
  warning: 'exclamation-triangle',
  danger: 'x-circle',
  success: 'check-circle',
}

const TONE_ICON_COLOR: Record<Tone, string> = {
  info: 'text-accent-on-soft',
  warning: 'text-hue-late',
  danger: 'text-hue-absent',
  success: 'text-hue-success',
}

interface InlineMessageProps {
  tone: Tone
  title?: string
  children?: ReactNode
  action?: ReactNode
  /** Rad javobi xodimga darhol yetishi kerak (UX §H.3). */
  assertive?: boolean
}

export function InlineMessage({ tone, title, children, action, assertive }: InlineMessageProps) {
  return (
    <div
      className={clsx('rounded-control p-3', TONE_TINT[tone])}
      role="status"
      aria-live={assertive ? 'assertive' : 'polite'}
    >
      <div className="flex min-w-0">
        <Icon name={TONE_ICON[tone]} className={clsx('mr-2 h-5 w-5', TONE_ICON_COLOR[tone])} />
        <div className="min-w-0 flex-1">
          {/* Matn tint ustida ham `--fg` rangida: shunda to'qqizta
              rang sozlanmasdan AA kafolatlanadi (UX §D.1, 6-nuqson). */}
          {title ? <p className="text-subhead font-semibold text-fg server-text">{title}</p> : null}
          {children ? (
            <div className="mt-1 text-footnote text-fg server-text">{children}</div>
          ) : null}
          {action ? <div className="mt-3">{action}</div> : null}
        </div>
      </div>
    </div>
  )
}

/**
 * Xato — SAHIFA o'rniga emas, kartaning ichida (UX §E.9). Sarlavha,
 * tab paneli va qolgan kartalar joyida qoladi.
 */
export function ErrorState({ onRetry }: { onRetry: () => void }) {
  const { t } = useTranslation()

  return (
    <InlineMessage
      tone="danger"
      title={t('state.loadFailed')}
      action={
        <Button variant="secondary" size="compact" onClick={onRetry}>
          {t('common.retry')}
        </Button>
      }
    >
      {t('state.loadFailedText')}
    </InlineMessage>
  )
}

interface EmptyStateProps {
  icon?: IconName
  title: string
  text?: string
  action?: ReactNode
}

export function EmptyState({ icon = 'information-circle', title, text, action }: EmptyStateProps) {
  return (
    <div className="px-4 py-8 text-center">
      <Icon name={icon} className="mx-auto mb-3 h-8 w-8 text-fg-tertiary" />
      <p className="text-body font-medium text-fg">{title}</p>
      {text ? <p className="mt-1 text-footnote text-fg-secondary">{text}</p> : null}
      {action ? <div className="mt-4 flex justify-center">{action}</div> : null}
    </div>
  )
}

/**
 * Skelet — spinner EMAS. Natijaning shakli oldindan ma'lum bo'lgan
 * har bir joyda skelet chiziladi va u yakuniy balandlikni egallaydi,
 * shunda ekran barmoq ostida siljib ketmaydi (UX §E.9).
 */
export function Skeleton({ className }: { className?: string }) {
  return <div className={clsx('animate-pulse rounded-chip bg-bg-sunken', className)} />
}

export function SkeletonLines({ rows = 3 }: { rows?: number }) {
  const items: number[] = []
  for (let index = 0; index < rows; index += 1) items.push(index)

  return (
    <div className="space-y-3">
      {items.map((index) => (
        <div key={index} className="flex items-center">
          <Skeleton className="h-4 w-24" />
          <div className="flex-1" />
          <Skeleton className="h-4 w-14" />
        </div>
      ))}
    </div>
  )
}
