import { useTranslation } from 'react-i18next'

import type { AttendanceDayDto } from '@/api/types'
import { DAY_STATUS_LOOK } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { Skeleton } from './feedback'
import { StatusChip } from './status-chip'

/**
 * Tanlangan kunning qisqacha yakuni (UX §E.4): ikkita katta soat,
 * ostida `kirish` / `chiqish` yorlig'i, ishlangan vaqt va kechikish.
 *
 * Raqamlar tabular (`nums`): `08:35` va `11:11` bir xil kenglikda
 * turishi kerak, aks holda kun almashganda ustun titraydi.
 */
export function DayPanel({ day, loading }: { day: AttendanceDayDto | null; loading: boolean }) {
  const { t } = useTranslation()
  const format = useFormat()

  if (loading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-6 w-28" />
        <div className="flex">
          <Skeleton className="h-10 w-24" />
          <div className="w-8" />
          <Skeleton className="h-10 w-24" />
        </div>
      </div>
    )
  }

  const look = day ? DAY_STATUS_LOOK[day.status] : null

  return (
    <div>
      {look ? (
        <div className="mb-3">
          <StatusChip look={look} />
        </div>
      ) : null}

      <div className="flex">
        <TimeBlock label={t('calendar.entry')} value={format.time(day?.firstInAt)} />
        <div className="w-8" />
        <TimeBlock label={t('calendar.exit')} value={format.time(day?.lastOutAt)} />
      </div>

      <div className="mt-4 space-y-1">
        <div className="flex items-baseline justify-between">
          <span className="text-footnote text-fg-secondary">{t('calendar.worked')}</span>
          <span className="nums text-subhead text-fg">{format.duration(day?.workedMinutes)}</span>
        </div>

        {day && day.lateMinutes > 0 ? (
          <div className="flex items-baseline justify-between">
            <span className="text-footnote text-fg-secondary">{t('home.late')}</span>
            <span className="nums text-subhead text-fg">
              {t('calendar.late', { n: day.lateMinutes })}
            </span>
          </div>
        ) : null}
      </div>
    </div>
  )
}

function TimeBlock({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="nums text-hero font-semibold text-fg">{value}</div>
      <div className="mt-0.5 text-footnote text-fg-secondary">{label}</div>
    </div>
  )
}

/** Oylik yakundagi bitta son (UX §E.4). */
export function StatRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between py-1.5">
      <span className="mr-3 text-footnote text-fg-secondary">{label}</span>
      <span className="nums text-subhead font-medium text-fg">{value}</span>
    </div>
  )
}
