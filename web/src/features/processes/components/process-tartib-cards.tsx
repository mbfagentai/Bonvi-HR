import { useNavigate } from '@tanstack/react-router'
import { CalendarOff, ChevronRight, ImageOff, Link2Off, type LucideIcon } from 'lucide-react'
import { Fragment, type ReactNode } from 'react'

import type {
  AppNotification,
  NotificationSeverity,
} from '@/features/notifications/api/notifications-api'
import { navigateToNotification } from '@/features/notifications/lib/notification-navigation'
import {
  notificationMeta,
  notificationTextKey,
  notificationValues,
  TYPE_ICON,
} from '@/features/notifications/lib/notification-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { Card, IconTile } from '@/shared/ui'

import type { ProcessTartib, ProcessTartibCount } from '../api/processes-api'
import { tartibCountHref } from '../lib/my-work'

type TartibTone = 'blue' | 'orange' | 'red'

const COUNT_ICON: Readonly<Record<string, LucideIcon>> = {
  unmatchedTerminalPersons: Link2Off,
  employeesWithoutPhoto: ImageOff,
  employeesWithoutSchedule: CalendarOff,
}

const SEVERITY_TONE: Readonly<Record<NotificationSeverity, TartibTone>> = {
  Info: 'blue',
  Warning: 'orange',
  Critical: 'red',
}

function TartibCard({
  icon,
  tone,
  title,
  text,
  meta,
  onOpen,
}: {
  icon: LucideIcon
  tone: TartibTone
  title: ReactNode
  text: ReactNode
  meta?: ReactNode
  onOpen: () => void
}) {
  return (
    <Card className="rounded-lg transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]">
      <button
        type="button"
        onClick={onOpen}
        className="press flex h-full w-full items-start gap-3.5 rounded-lg p-5 text-left"
      >
        <IconTile icon={icon} tone={tone} className="shrink-0" />

        <span className="min-w-0 flex-1">
          <span className="block text-callout leading-snug font-semibold">{title}</span>
          <span className="mt-1 block text-subhead text-[var(--fg-secondary)]">{text}</span>
          {meta !== undefined && (
            <span className="mt-1.5 block text-caption1 text-[var(--fg-tertiary)]">{meta}</span>
          )}
        </span>

        <ChevronRight
          aria-hidden
          strokeWidth={2.5}
          className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]"
        />
      </button>
    </Card>
  )
}

function CountCard({ item, href }: { item: ProcessTartibCount; href: string }) {
  const { t, num } = useLocale()
  const navigate = useNavigate()

  return (
    <TartibCard
      icon={COUNT_ICON[item.key] ?? ChevronRight}
      tone="orange"
      title={t(`processes.my.tartib.${item.key}.title`)}
      text={t(`processes.my.tartib.${item.key}.text`, { n: num(item.count) })}
      onOpen={() => void navigate({ href })}
    />
  )
}

/** Worded the way the bell words it: both read the same `notifications.type.*` strings. */
function NotificationCard({ item }: { item: AppNotification }) {
  const { t, num, date } = useLocale()
  const navigate = useNavigate()

  const textKey = notificationTextKey(item)
  const meta = notificationMeta(item, { num, date })

  return (
    <TartibCard
      icon={TYPE_ICON[item.type]}
      tone={SEVERITY_TONE[item.severity]}
      title={t(`notifications.type.${textKey}.title`)}
      text={t(`notifications.type.${textKey}.text`, notificationValues(item, { num, date }))}
      meta={
        <>
          <span className="tabular">{date(item.date)}</span>
          {meta.map((entry, index) => (
            <Fragment key={index}>
              {' · '}
              {entry.kind === 'text' ? entry.value : t(entry.key, entry.values)}
            </Fragment>
          ))}
        </>
      }
      onOpen={() => navigateToNotification(navigate, item)}
    />
  )
}

/** «Tartib» (SPEC §11.8): read on request, never stored. A count card drops out for a key this build cannot place. */
export function ProcessTartibCards({ tartib }: { tartib: ProcessTartib }) {
  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {tartib.counts.map((item) => {
        const href = tartibCountHref(item.key)
        return href === null ? null : <CountCard key={item.key} item={item} href={href} />
      })}
      {tartib.notifications.map((item) => (
        <NotificationCard key={item.key} item={item} />
      ))}
    </div>
  )
}
