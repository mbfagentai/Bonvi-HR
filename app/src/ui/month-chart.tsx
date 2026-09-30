import clsx from 'clsx'
import { useTranslation } from 'react-i18next'

import type { MonthTotals } from '@/data/attendance'
import { useFormat } from '@/lib/format'

/**
 * Month totals as two small charts (calendar "Oylik natija" card).
 *
 * Hours: worked against plan — a single progress bar, because the job is
 * one ratio. Days: a segmented bar, because worked / excused / absent are
 * parts of one whole. Both are plain divs, no chart library: two bars do
 * not justify a dependency in a PWA that has to load on a phone.
 *
 * Every number stays visible as text next to its colour swatch, so the
 * colour is never the only carrier of meaning (UX §D.1). Segment colours
 * are the same hues the calendar cells use — a red segment here means
 * the same thing as a red day above.
 */
export function MonthChart({ totals }: { totals: MonthTotals }) {
  const { t } = useTranslation()
  const format = useFormat()

  const planned = totals.plannedMinutes
  const worked = totals.workedMinutes
  const percent = planned > 0 ? Math.round((worked / planned) * 100) : null

  const segments = [
    { key: 'present', label: t('calendar.totalPresent'), value: totals.presentDays, hue: 'bg-hue-success' },
    { key: 'excused', label: t('calendar.totalExcused'), value: totals.excusedDays, hue: 'bg-hue-excused' },
    { key: 'absent', label: t('calendar.totalAbsent'), value: totals.absentDays, hue: 'bg-hue-absent' },
  ]
  const dayTotal = segments.reduce((sum, segment) => sum + segment.value, 0)

  return (
    <div className="space-y-5">
      <section>
        <div className="text-footnote text-fg-secondary">{t('calendar.chartHours')}</div>
        <div className="mt-1 flex items-baseline justify-between">
          <span className="nums text-title2 font-bold text-fg">{format.duration(worked)}</span>
          {percent !== null ? (
            <span className="nums ml-3 text-footnote text-fg-secondary">
              {t('calendar.planShare', { percent: format.number(percent) })}
            </span>
          ) : null}
        </div>

        <div
          role="img"
          aria-label={`${t('calendar.totalWorkedHours')}: ${format.duration(worked)}, ${t('calendar.totalPlannedHours')}: ${format.duration(planned)}`}
          className="mt-2 h-2 w-full overflow-hidden rounded-pill bg-bg-sunken"
        >
          <div
            className="h-full rounded-pill bg-accent transition-[width] duration-200 ease-ios"
            style={{ width: `${percent === null ? 0 : Math.min(percent, 100)}%` }}
          />
        </div>

        <div className="mt-1.5 flex justify-between text-caption1 text-fg-tertiary">
          <span>{t('calendar.totalWorkedHours')}</span>
          <span className="nums">
            {t('calendar.totalPlannedHours')}: {format.duration(planned)}
          </span>
        </div>
      </section>

      <section>
        <div className="text-footnote text-fg-secondary">{t('calendar.chartDays')}</div>

        <div
          role="img"
          aria-label={segments.map((segment) => `${segment.label}: ${segment.value}`).join(', ')}
          className="mt-2 flex h-3 w-full gap-[2px] overflow-hidden rounded-pill bg-bg-sunken"
        >
          {dayTotal > 0
            ? segments
                .filter((segment) => segment.value > 0)
                .map((segment) => (
                  <div
                    key={segment.key}
                    className={clsx('h-full first:rounded-l-pill last:rounded-r-pill', segment.hue)}
                    style={{ flexGrow: segment.value, flexBasis: 0 }}
                  />
                ))
            : null}
        </div>

        <div className="mt-3 space-y-2">
          {segments.map((segment) => (
            <LegendRow key={segment.key} hue={segment.hue} label={segment.label} value={segment.value} />
          ))}
          {/* Late days are a SUBSET of worked days, not a fourth part of
              the whole — so they get a row, not a segment. */}
          <LegendRow hue="bg-hue-late" label={t('calendar.totalLate')} value={totals.lateDays} />
        </div>
      </section>
    </div>
  )
}

function LegendRow({ hue, label, value }: { hue: string; label: string; value: number }) {
  const format = useFormat()

  return (
    <div className="flex items-center">
      <span aria-hidden="true" className={clsx('h-2.5 w-2.5 shrink-0 rounded-pill', hue)} />
      <span className="ml-2 min-w-0 flex-1 truncate text-footnote text-fg-secondary">{label}</span>
      <span className="nums ml-3 text-subhead font-medium text-fg">{format.number(value)}</span>
    </div>
  )
}
