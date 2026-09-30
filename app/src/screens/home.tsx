import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'

import { findDay, marksNewestFirst } from '@/data/attendance'
import { useMyPhoto } from '@/data/photo'
import { useMe, useMonth, useRequests } from '@/data/queries'
import { DAY_STATUS_LOOK, REQUEST_STATUS_LOOK } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { parseIsoDate, todayIso } from '@/lib/time'
import { TabScreen } from '@/ui/app-shell'
import { Avatar } from '@/ui/avatar'
import { Card, CardRow } from '@/ui/card'
import { EmptyState, ErrorState, Skeleton, SkeletonLines } from '@/ui/feedback'
import { InstallCard } from '@/ui/install-card'
import { MarkRow, MarkRowSkeleton } from '@/ui/mark-row'
import { StatusChip } from '@/ui/status-chip'
import { CheckInCard } from './checkin-card'

/**
 * ASOSIY ekran (UX §A.5).
 *
 * Tartib — «xodim ilovani nima uchun ochdi» degan savolga javob,
 * eng ehtimolli javob birinchi: belgilash, bugungi holat, bugungi
 * o'tishlar, kutayotgan so'rovlar.
 *
 * ⭐ «Bugun» — JORIY SMENANING katagi, server aytgani (`current.date`):
 * tungi smenada soat 06:00 da bu kechagi kun — kirish va chiqish o'sha
 * katakda, tabeldagidek. Katak ko'rinayotgan oyda bo'lmasa (oyning birinchi
 * tongi) — kalendar kuni.
 */
export function HomeScreen() {
  const { t } = useTranslation()
  const format = useFormat()

  const today = todayIso()
  const parts = parseIsoDate(today)
  const year = parts ? parts.year : new Date().getUTCFullYear()
  const month = parts ? parts.month : 1

  const me = useMe()
  const monthQuery = useMonth(year, month)
  const requests = useRequests()

  const current = monthQuery.data ? monthQuery.data.current : null
  const todayCell =
    findDay(monthQuery.data?.days, current ? current.date : today) ?? findDay(monthQuery.data?.days, today)
  const photoUrl = useMyPhoto(me.data ? me.data.photoKey : null)
  const marks = marksNewestFirst(todayCell)
  const pending = (requests.data ?? []).filter((request) => request.status === 'Pending')

  const reload = (): void => {
    me.refetch()
    monthQuery.refetch()
    requests.refetch()
  }

  const scheduleLine =
    todayCell && todayCell.planStartAt && todayCell.planEndAt
      ? t('home.workday', {
          range: `${format.time(todayCell.planStartAt)}–${format.time(todayCell.planEndAt)}`,
        })
      : null

  return (
    <TabScreen
      title={t('home.title')}
      staleSince={monthQuery.fetchedAt}
      subtitle={
        <>
          <div>{format.dateLong(today)}</div>
          {scheduleLine ? <div className="nums mt-0.5">{scheduleLine}</div> : null}
        </>
      }
      trailing={
        <Link
          to="/profil"
          aria-label={t('home.profileAria')}
          className="flex h-11 w-11 items-center justify-center rounded-pill active:bg-bg-pressed"
        >
          {me.data ? (
            <Avatar photoUrl={photoUrl} fullName={me.data.fullName} />
          ) : (
            <Skeleton className="h-8 w-8 rounded-pill" />
          )}
        </Link>
      }
    >
      <CheckInCard
        me={me.data}
        today={todayCell}
        current={current}
        loading={me.isLoading || monthQuery.isLoading}
        loadFailed={me.error !== null && me.data === null}
        onReload={reload}
      />

      {/* Bugungi holat — birinchi belgidan OLDIN ham turadi, shunda
          kun davomida kartalarning o'rni siljimaydi. */}
      <Card title={t('home.todayTitle')}>
        {monthQuery.isLoading ? (
          <SkeletonLines rows={3} />
        ) : monthQuery.error && !monthQuery.data ? (
          <ErrorState onRetry={monthQuery.refetch} />
        ) : (
          <>
            <div className="mb-3">
              <StatusChip look={DAY_STATUS_LOOK[todayCell ? todayCell.status : 'Planned']} />
            </div>
            <CardRow
              label={t('home.entry')}
              value={<span className="nums">{format.time(todayCell?.firstInAt)}</span>}
            />
            <CardRow
              label={t('home.exit')}
              value={<span className="nums">{format.time(todayCell?.lastOutAt)}</span>}
            />
            <CardRow
              label={t('home.worked')}
              value={<span className="nums">{format.duration(todayCell?.workedMinutes)}</span>}
            />
            {todayCell && todayCell.lateMinutes > 0 ? (
              <CardRow
                label={t('home.late')}
                value={
                  <span className="nums">{t('calendar.late', { n: todayCell.lateMinutes })}</span>
                }
              />
            ) : null}
          </>
        )}
      </Card>

      <Card
        title={t('home.marksTitle')}
        action={{ label: t('common.all'), to: `/taqvim?kun=${today}` }}
      >
        {monthQuery.isLoading ? (
          <div>
            <MarkRowSkeleton />
            <MarkRowSkeleton />
          </div>
        ) : monthQuery.error && !monthQuery.data ? (
          <ErrorState onRetry={monthQuery.refetch} />
        ) : marks.length === 0 ? (
          <EmptyState
            icon="clock"
            title={t('home.marksEmpty')}
            text={t('home.marksEmptyText')}
          />
        ) : (
          <div>
            {marks.map((mark) => (
              <MarkRow key={`${mark.at}-${mark.direction}`} mark={mark} />
            ))}
          </div>
        )}
      </Card>

      {/* Kutayotgan so'rov bo'lmasa karta UMUMAN chizilmaydi: eng band
          ekrandagi bo'sh karta — shovqin, tab esa bir tegish narida. */}
      {pending.length > 0 ? (
        <Card
          title={t('home.requestsTitle')}
          action={pending.length > 2 ? { label: t('common.all'), to: '/sorovlar' } : undefined}
        >
          <div>
            {pending.slice(0, 2).map((request) => (
              <Link
                key={request.id}
                to={`/sorovlar/${request.id}`}
                className="flex min-h-[52px] items-center border-t border-border py-2 first:border-t-0 active:bg-bg-pressed"
              >
                <div className="min-w-0 flex-1">
                  <div className="truncate text-body text-fg server-text">
                    {request.absenceTypeName}
                  </div>
                  <div className="nums mt-0.5 text-footnote text-fg-secondary">
                    {request.fromDate === request.toDate
                      ? format.date(request.fromDate)
                      : `${format.date(request.fromDate)} — ${format.date(request.toDate)}`}
                  </div>
                </div>
                <StatusChip look={REQUEST_STATUS_LOOK[request.status]} size="sm" />
              </Link>
            ))}
          </div>
        </Card>
      ) : null}

      <InstallCard />
    </TabScreen>
  )
}
