import clsx from 'clsx'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'

import {
  findDay,
  flexiblePlannedMinutes,
  marksNewestFirst,
  monthTotals,
  requestsOnDay,
  statusesInMonth,
} from '@/data/attendance'
import { useMonth, useRequests } from '@/data/queries'
import { hoursMinutes } from '@/data/schedule-requests'
import { DAY_STATUS_LOOK, REQUEST_STATUS_LOOK } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { monthKey, parseIsoDate, parseMonthKey, shiftMonth, todayIso } from '@/lib/time'
import { TabScreen } from '@/ui/app-shell'
import { Card, CardRow } from '@/ui/card'
import { DayPanel, StatRow } from '@/ui/day-panel'
import { EmptyState, ErrorState, SkeletonLines } from '@/ui/feedback'
import { Icon } from '@/ui/icon'
import { MarkRow, MarkRowSkeleton } from '@/ui/mark-row'
import { MonthChart } from '@/ui/month-chart'
import { MonthGrid } from '@/ui/month-grid'
import { StatusChip } from '@/ui/status-chip'

/**
 * TAQVIM (UX §A.5).
 *
 * Oy va kun MANZILDA yashaydi (`?oy=2026-09&kun=2026-09-22`), shunda
 * havolani suhbatga tashlab, telefonda ochsa bo'ladi. Noto'g'ri yoki
 * kelajakdagi oy — xato ekrani emas, shunchaki joriy oy.
 */
export function CalendarScreen() {
  const { t } = useTranslation()
  const format = useFormat()
  const [params, setParams] = useSearchParams()

  const today = todayIso()
  const todayParts = parseIsoDate(today)
  const currentYear = todayParts ? todayParts.year : new Date().getUTCFullYear()
  const currentMonth = todayParts ? todayParts.month : 1

  const requestedMonth = parseMonthKey(params.get('oy'))
  const requestedDay = parseIsoDate(params.get('kun'))

  const inFuture =
    requestedMonth !== null &&
    (requestedMonth.year > currentYear ||
      (requestedMonth.year === currentYear && requestedMonth.month > currentMonth))

  const year = requestedMonth && !inFuture ? requestedMonth.year : currentYear
  const month = requestedMonth && !inFuture ? requestedMonth.month : currentMonth

  const selected =
    requestedDay && requestedDay.year === year && requestedDay.month === month
      ? `${params.get('kun')}`
      : year === currentYear && month === currentMonth
        ? today
        : `${monthKey(year, month)}-01`

  const monthQuery = useMonth(year, month)
  const requests = useRequests()

  const days = monthQuery.data?.days ?? []
  const day = findDay(days, selected)
  const marks = marksNewestFirst(day)
  const totals = monthTotals(days, monthQuery.data ? monthQuery.data.totals : null)
  const dayRequests = requestsOnDay(requests.data ?? [], selected)
  const legend = statusesInMonth(days)
  const lateDays = days.filter((item) => item.lateMinutes > 0).length
  // Erkin kun: reja — «Ish vaqti» (norma), boshlanish soati emas; oyna faqat ma'lumot uchun
  const flexibleMinutes = flexiblePlannedMinutes(day)

  const canGoForward = year < currentYear || (year === currentYear && month < currentMonth)

  const goMonth = (delta: number): void => {
    const next = shiftMonth(year, month, delta)
    setParams({ oy: monthKey(next.year, next.month) }, { replace: true })
  }

  const selectDay = (dateIso: string): void => {
    setParams({ oy: monthKey(year, month), kun: dateIso }, { replace: true })
  }

  return (
    <TabScreen title={t('calendar.title')} staleSince={monthQuery.fetchedAt}>
      <Card>
        <div className="mb-3 flex items-center justify-between">
          <button
            type="button"
            aria-label={t('calendar.prevMonth')}
            onClick={() => goMonth(-1)}
            className="flex h-11 w-11 items-center justify-center rounded-control text-accent-text active:bg-bg-pressed"
          >
            <Icon name="chevron-left" />
          </button>

          <h2 className="text-title3 font-semibold text-fg">{format.monthTitle(year, month)}</h2>

          <button
            type="button"
            aria-label={t('calendar.nextMonth')}
            disabled={!canGoForward}
            onClick={() => goMonth(1)}
            className="flex h-11 w-11 items-center justify-center rounded-control text-accent-text active:bg-bg-pressed disabled:text-fg-tertiary"
          >
            <Icon name="chevron-right" />
          </button>
        </div>

        {monthQuery.error && !monthQuery.data ? (
          <ErrorState onRetry={monthQuery.refetch} />
        ) : (
          <MonthGrid
            year={year}
            month={month}
            days={days}
            selected={selected}
            loading={monthQuery.isLoading}
            onSelect={selectDay}
          />
        )}
      </Card>

      <Card title={format.dateLong(selected)}>
        {monthQuery.isLoading ? (
          <DayPanel day={null} loading />
        ) : !day ? (
          <EmptyState
            icon="calendar"
            title={t('calendar.emptyTitle')}
            text={t('calendar.emptyText')}
          />
        ) : (
          <DayPanel day={day} loading={false} />
        )}
      </Card>

      <Card title={t('calendar.scheduleTitle')}>
        {monthQuery.isLoading ? (
          <SkeletonLines rows={2} />
        ) : day && day.planStartAt && day.planEndAt ? (
          <>
            {flexibleMinutes !== null ? (
              <>
                <p className="nums text-subhead text-fg">
                  {t('calendar.scheduleFlexible', { hours: hoursMinutes(flexibleMinutes) })}
                </p>
                <p className="nums mt-0.5 text-footnote text-fg-secondary">
                  {t('calendar.scheduleFlexibleRange', {
                    range: `${format.time(day.planStartAt)}–${format.time(day.planEndAt)}`,
                  })}
                </p>
              </>
            ) : (
              <p className="nums text-subhead text-fg">
                {t('calendar.scheduleRange', {
                  range: `${format.time(day.planStartAt)}–${format.time(day.planEndAt)}`,
                })}
              </p>
            )}
            {/* ⚠️ Tanaffus OYNASI (masalan 13:00–14:00) tabel katagida
                yo'q — katak faqat ayirilgan daqiqalarni biladi. Shu
                sababli bu yerda oyna emas, AYIRILGAN vaqt ko'rsatiladi
                (hisobotdagi bo'shliq sifatida qayd etilgan). */}
            {day.breakDeductionMinutes > 0 ? (
              <div className="mt-2">
                <CardRow
                  label={t('calendar.break')}
                  value={<span className="nums">{format.duration(day.breakDeductionMinutes)}</span>}
                />
              </div>
            ) : null}
          </>
        ) : (
          <p className="text-footnote text-fg-secondary">{t('calendar.noSchedule')}</p>
        )}
      </Card>

      <Card title={t('calendar.marksTitle')}>
        {monthQuery.isLoading ? (
          <div>
            <MarkRowSkeleton />
            <MarkRowSkeleton />
          </div>
        ) : marks.length === 0 ? (
          <EmptyState icon="clock" title={t('calendar.marksEmpty')} />
        ) : (
          <div>
            {marks.map((mark) => (
              <MarkRow key={`${mark.at}-${mark.direction}`} mark={mark} />
            ))}
          </div>
        )}
      </Card>

      {dayRequests.length > 0 ? (
        <Card title={t('calendar.requestsTitle')}>
          <div>
            {dayRequests.map((request) => (
              <Link
                key={request.id}
                to={`/sorovlar/${request.id}`}
                className="flex min-h-[52px] items-center border-t border-border py-2 first:border-t-0 active:bg-bg-pressed"
              >
                <span className="min-w-0 flex-1 truncate text-body text-fg server-text">
                  {request.absenceTypeName}
                </span>
                <StatusChip look={REQUEST_STATUS_LOOK[request.status]} size="sm" />
              </Link>
            ))}
          </div>
        </Card>
      ) : null}

      <Card title={t('calendar.totalsTitle')}>
        {monthQuery.isLoading ? (
          <SkeletonLines rows={4} />
        ) : (
          <>
            <MonthChart totals={totals} />
            {/* Faqat erkin kunlarda bo'ladi — qolgan xodimlarda qator umuman chiqmaydi */}
            {totals.shortfallMinutes > 0 ? (
              <div className="mt-3">
                <StatRow label={t('calendar.totalShortfall')} value={format.duration(totals.shortfallMinutes)} />
              </div>
            ) : null}
          </>
        )}
      </Card>

      {/* Belgilar — faqat SHU oyda uchragan holatlar. To'liq ro'yxat
          har oy bir xil bo'lib, o'qilmay qolardi. */}
      {legend.length > 0 ? (
        <Card title={t('calendar.legendTitle')}>
          {/* Each swatch is a miniature of the calendar cell itself, so the
              legend reads as "this square means that" — not as a list of
              chips that look nothing like the grid above. */}
          <div className="grid grid-cols-2 gap-x-4 gap-y-3">
            {legend.map((status) => (
              <LegendItem
                key={status}
                swatchClass={DAY_STATUS_LOOK[status].tintClass}
                label={t(DAY_STATUS_LOOK[status].labelKey)}
                count={days.filter((item) => item.status === status).length}
              />
            ))}
            {lateDays > 0 ? (
              <LegendItem swatchClass="bg-bg-elevated" late label={t('home.late')} count={lateDays} />
            ) : null}
          </div>
        </Card>
      ) : null}
    </TabScreen>
  )
}

function LegendItem({
  swatchClass,
  label,
  count,
  late = false,
}: {
  swatchClass: string
  label: string
  count: number
  /** Draw the lateness stripe the grid puts under a late day. */
  late?: boolean
}) {
  return (
    <div className="flex min-w-0 items-center">
      <span
        aria-hidden="true"
        className={clsx('relative h-5 w-5 shrink-0 rounded-[6px] border border-border', swatchClass)}
      >
        {late ? (
          <span className="absolute bottom-[3px] left-[3px] right-[3px] h-[2px] rounded-pill bg-hue-late" />
        ) : null}
      </span>
      <span className="ml-2 min-w-0 flex-1 truncate text-footnote text-fg">{label}</span>
      <span className="nums ml-1 shrink-0 text-footnote text-fg-tertiary">{count}</span>
    </div>
  )
}
