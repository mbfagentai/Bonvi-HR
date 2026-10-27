import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams, useSearchParams } from 'react-router-dom'

import type { ApiError } from '@/api/errors'
import { findDay, flexiblePlannedMinutes, marksNewestFirst, monthTotals } from '@/data/attendance'
import { useTeamPhoto } from '@/data/photo'
import { hoursMinutes } from '@/data/schedule-requests'
import { invalidateTeam, knownMember, phoneHref, useTeamMemberMonth } from '@/data/team'
import { useFormat } from '@/lib/format'
import { monthKey, parseIsoDate, parseMonthKey, shiftMonth, todayIso } from '@/lib/time'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { Avatar } from '@/ui/avatar'
import { Button } from '@/ui/button'
import { Card, CardRow } from '@/ui/card'
import { DayPanel, StatRow } from '@/ui/day-panel'
import { EmptyState, ErrorState, InlineMessage, Skeleton, SkeletonLines } from '@/ui/feedback'
import { Icon } from '@/ui/icon'
import { MarkRow, MarkRowSkeleton } from '@/ui/mark-row'
import { MonthChart } from '@/ui/month-chart'
import { MonthGrid } from '@/ui/month-grid'

/**
 * JAMOA A'ZOSI — boshliq ko'radigan bitta odamning oyi (SPEC-jamoam §F).
 *
 * ⭐ Oy va kun — TAQVIMNING AYNAN O'ZI: o'sha `MonthGrid`, `DayPanel`,
 * o'tishlar va oylik natija, o'sha `?oy=&kun=` manzil qoidasi. Ma'lumot
 * ham `/api/me/attendance` shaklida keladi (§C3) — boshliq ko'rgan kun
 * xodimning o'z ilovasidagi kun bilan bir xil o'qilishi shart.
 *
 * Sarlavha (ism, rasm, telefon) paneldan ma'lum bo'lsa — darhol chiziladi,
 * oyni kutmaydi.
 *
 * ⚠️ Bu yerda «hozir» holati (`current`) YO'Q — u faqat xodimning o'ziga.
 * Boshliqning qamrovidan tashqari kunlar (boshqa bo'limda ishlagan yoki
 * hali ishlamagan) `NotEmployed` bo'lib keladi va SOKIN ko'rsatiladi: «Jamoada
 * emas» yozuvi, jadval ham, o'tishlar ham yo'q.
 */
const OUTSIDE_LABELS = { NotEmployed: 'team.outsideDay' } as const
export function TeamMemberScreen() {
  const { t } = useTranslation()
  const format = useFormat()
  const { employeeId = '' } = useParams()
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

  const query = useTeamMemberMonth(employeeId, year, month)
  const member = query.data ? query.data.employee : knownMember(employeeId)
  const photoUrl = useTeamPhoto(member ? member.employeeId : null, member ? member.hasPhoto : false)
  const call = member ? phoneHref(member.phone) : null

  // Boshliqlik shu orada olib tashlangan — keshlar tozalanadi, tab yo'qoladi
  const revoked = query.error !== null && query.error.code === 'jamoa.boshliq_emas'
  useEffect(() => {
    if (revoked) invalidateTeam()
  }, [revoked])

  const failed = query.error !== null && query.data === null
  const days = query.data ? query.data.days : []
  const day = findDay(days, selected)
  // Jamoada bo'lmagan kun — kun kartasidan tashqari hech narsa chizilmaydi
  const outside = day !== null && day.status === 'NotEmployed'
  const marks = marksNewestFirst(day)
  const totals = monthTotals(days, query.data ? query.data.totals : null)
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
    <PushedScreen title={member ? member.fullName : t('team.memberTitle')}>
      {member ? (
        <Card>
          <div className="flex items-center">
            <Avatar photoUrl={photoUrl} fullName={member.fullName} size="lg" className="shrink-0" />
            <div className="ml-4 min-w-0">
              <p className="text-title3 font-semibold text-fg server-text">{member.fullName}</p>
              {member.positionName ? (
                <p className="mt-1 text-footnote text-fg-secondary server-text">{member.positionName}</p>
              ) : null}
              <p className="mt-0.5 text-footnote text-fg-secondary server-text">
                {member.departmentName ?? t('profile.noDepartment')}
              </p>
            </div>
          </div>

          {member.phone ? (
            <div className="mt-3 border-t border-border pt-2">
              <CardRow label={t('team.phone')} value={<span className="nums">{member.phone}</span>} />
            </div>
          ) : null}

          {call ? (
            <a
              href={call}
              className="mt-3 flex h-12 w-full items-center justify-center rounded-control bg-accent-soft px-5 text-body font-medium text-accent-on-soft active:bg-bg-pressed"
            >
              <Icon name="phone" className="mr-2 h-5 w-5" />
              <span className="truncate">{t('team.call')}</span>
            </a>
          ) : null}
        </Card>
      ) : query.isLoading ? (
        <Card>
          <div className="flex items-center">
            <Skeleton className="h-16 w-16 rounded-pill" />
            <div className="ml-4 flex-1 space-y-2">
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-4 w-28" />
            </div>
          </div>
        </Card>
      ) : null}

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

        {failed && query.error ? (
          <MemberError error={query.error} onRetry={query.refetch} />
        ) : (
          <MonthGrid
            year={year}
            month={month}
            days={days}
            selected={selected}
            loading={query.isLoading}
            onSelect={selectDay}
            statusLabels={OUTSIDE_LABELS}
          />
        )}
      </Card>

      {/* Oy yuklanmagan bo'lsa qolgan kartalar YO'Q: bo'sh «—» lar
          «bu kuni hech narsa bo'lmagan» deb noto'g'ri o'qilardi */}
      {failed ? null : (
        <>
          <Card title={format.dateLong(selected)}>
            {query.isLoading ? (
              <DayPanel day={null} loading />
            ) : !day ? (
              <EmptyState icon="calendar" title={t('calendar.emptyTitle')} text={t('calendar.emptyText')} />
            ) : outside ? (
              <EmptyState icon="calendar" title={t('team.outsideDay')} text={t('team.outsideDayText')} />
            ) : (
              <DayPanel day={day} loading={false} />
            )}
          </Card>

          {outside ? null : (
            <Card title={t('calendar.scheduleTitle')}>
              {query.isLoading ? (
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
          )}

          {outside ? null : (
            <Card title={t('calendar.marksTitle')}>
              {query.isLoading ? (
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
          )}

          <Card title={t('calendar.totalsTitle')}>
            {query.isLoading ? (
              <SkeletonLines rows={4} />
            ) : (
              <>
                <MonthChart totals={totals} />
                {totals.shortfallMinutes > 0 || totals.personalMinutes > 0 ? (
                  <div className="mt-3">
                    {totals.shortfallMinutes > 0 ? (
                      <StatRow label={t('calendar.totalShortfall')} value={format.duration(totals.shortfallMinutes)} />
                    ) : null}
                    {totals.personalMinutes > 0 ? (
                      <StatRow label={t('calendar.totalPersonal')} value={format.duration(totals.personalMinutes)} />
                    ) : null}
                  </div>
                ) : null}
              </>
            )}
          </Card>
        </>
      )}
    </PushedScreen>
  )
}

/**
 * Oy o'rnidagi xato. `jamoa.xodim_topilmadi` — xodim shu oyda jamoada
 * bo'lmagan (yoki boshqa bo'limga o'tgan): bu nosozlik emas, «qayta urinish»
 * tugmasi ham kerak emas — oyni almashtirish strelkalari tepada turibdi.
 */
function MemberError({ error, onRetry }: { error: ApiError; onRetry: () => void }) {
  const { t } = useTranslation()
  const errorText = useErrorText()

  if (error.kind === 'network' || error.kind === 'server') return <ErrorState onRetry={onRetry} />

  const text = errorText(error)

  if (error.code === 'jamoa.xodim_topilmadi') {
    return (
      <InlineMessage tone="warning" title={text.title ?? undefined}>
        {text.body}
      </InlineMessage>
    )
  }

  return (
    <InlineMessage
      tone="danger"
      title={text.title ?? undefined}
      action={
        <Button variant="secondary" size="compact" onClick={onRetry}>
          {t('common.retry')}
        </Button>
      }
    >
      {text.body}
    </InlineMessage>
  )
}
