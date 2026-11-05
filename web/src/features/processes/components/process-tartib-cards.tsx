import { useNavigate } from '@tanstack/react-router'
import { ChevronRight, ImageOff, Link2Off, CalendarOff, type LucideIcon } from 'lucide-react'
import { Fragment, type ReactNode } from 'react'

import type { AppNotification } from '@/features/notifications/api/notifications-api'
import { navigateToNotification } from '@/features/notifications/lib/notification-navigation'
import {
  notificationMeta,
  notificationTextKey,
  notificationValues,
  SEVERITY_LOOK,
  TYPE_ICON,
} from '@/features/notifications/lib/notification-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Card } from '@/shared/ui'

import type { ProcessTartib, ProcessTartibCount } from '../api/processes-api'
import { tartibCountHref } from '../lib/my-work'

const COUNT_ICON: Readonly<Record<string, LucideIcon>> = {
  unmatchedTerminalPersons: Link2Off,
  employeesWithoutPhoto: ImageOff,
  employeesWithoutSchedule: CalendarOff,
}

function TartibCard({
  icon: Icon,
  iconClassName,
  title,
  text,
  meta,
  onOpen,
}: {
  icon: LucideIcon
  iconClassName: string
  title: ReactNode
  text: ReactNode
  meta?: ReactNode
  onOpen: () => void
}) {
  return (
    <Card className="transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]">
      <button
        type="button"
        onClick={onOpen}
        className="press flex h-full w-full items-start gap-3 rounded-lg p-3.5 text-left"
      >
        <Icon
          aria-hidden
          strokeWidth={2}
          className={cn('mt-0.5 size-[18px] shrink-0', iconClassName)}
        />

        <span className="min-w-0 flex-1">
          <span className="block text-subhead font-semibold">{title}</span>
          <span className="mt-0.5 block text-footnote text-[var(--fg-secondary)]">{text}</span>
          {meta !== undefined && (
            <span className="mt-1 block text-caption1 text-[var(--fg-tertiary)]">{meta}</span>
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
      iconClassName="text-ios-orange"
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
      iconClassName={SEVERITY_LOOK[item.severity].icon}
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

/** «Tartib» (SPEC §11.8): read on request, never stored. Count cards drop out for a key this build cannot place. */
export function ProcessTartibCards({ tartib }: { tartib: ProcessTartib }) {
  return (
    <div className="grid gap-2.5 md:grid-cols-2 xl:grid-cols-3">
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
