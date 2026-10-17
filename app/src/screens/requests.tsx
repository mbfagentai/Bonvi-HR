import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'

import { useRequests, useScheduleRequests } from '@/data/queries'
import {
  mergeRequests,
  scheduleSummaryLines,
  scheduleTitleKey,
  type AnyRequest,
} from '@/data/schedule-requests'
import { REQUEST_STATUS_LOOK } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { TabScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { EmptyState, ErrorState, SkeletonLines } from '@/ui/feedback'
import { Icon, type IconName } from '@/ui/icon'
import { Sheet } from '@/ui/sheet'
import { StatusChip } from '@/ui/status-chip'

/**
 * SO'ROVLAR ro'yxati (UX §A.5).
 *
 * Ikki bo'lim: qaror kutayotganlar tepada. Sabab oddiy — HR ning
 * qarori bu ilovada xodim boshidan o'tadigan YAGONA asinxron narsa,
 * va u birinchi ko'rinishi kerak.
 *
 * Ikki xil so'rov (yo'qlik va jadval, SPEC-jadval-sorovi §C4) ikki
 * endpoint'dan keladi, ekranda esa BITTA ro'yxat — yuborilgan vaqti
 * bo'yicha. Biri yuklanmasa ikkinchisi baribir ko'rsatiladi, yuklanmagani
 * o'rnida xato kartasi turadi (UX §E.9 — xato kartaning o'rnida).
 */
export function RequestsScreen() {
  const { t } = useTranslation()
  const absence = useRequests()
  const schedule = useScheduleRequests()

  const [typeOpen, setTypeOpen] = useState(false)

  // Birinchi yuklashda ikkalasi ham kutiladi: biri kelib, ikkinchisi
  // keyin qo'shilsa, ro'yxat barmoq ostida siljib ketardi.
  const loading = (absence.isLoading && !absence.data) || (schedule.isLoading && !schedule.data)
  const absenceFailed = absence.error !== null && absence.data === null
  const scheduleFailed = schedule.error !== null && schedule.data === null

  const all = mergeRequests(absence.data ?? [], schedule.data ?? [])
  const pending = all.filter((request) => request.status === 'Pending')
  const decided = all.filter((request) => request.status !== 'Pending')

  const retryFailed = (): void => {
    if (absenceFailed) absence.refetch()
    if (scheduleFailed) schedule.refetch()
  }

  // Oflayn chizig'idagi vaqt — ikkalasidan ESKIROG'I: ekrandagi eng
  // eski ma'lumot shu lahzadagi.
  const fetchedAt =
    absence.fetchedAt && schedule.fetchedAt
      ? absence.fetchedAt < schedule.fetchedAt
        ? absence.fetchedAt
        : schedule.fetchedAt
      : absence.fetchedAt ?? schedule.fetchedAt

  return (
    <>
      <TabScreen title={t('requests.title')} staleSince={fetchedAt} bottomSpace>
        {loading ? (
          <Card>
            <SkeletonLines rows={4} />
          </Card>
        ) : absenceFailed && scheduleFailed ? (
          <Card>
            <ErrorState onRetry={retryFailed} />
          </Card>
        ) : (
          <>
            {absenceFailed || scheduleFailed ? (
              <Card>
                <ErrorState onRetry={retryFailed} />
              </Card>
            ) : null}

            {all.length === 0 && !absenceFailed && !scheduleFailed ? (
              <Card>
                <EmptyState
                  icon="document-text"
                  title={t('requests.emptyTitle')}
                  text={t('requests.emptyText')}
                  action={<Button onClick={() => setTypeOpen(true)}>{t('requests.create')}</Button>}
                />
              </Card>
            ) : null}

            {pending.length > 0 ? (
              <Card title={t('requests.pending')}>
                <RequestList items={pending} />
              </Card>
            ) : null}

            {decided.length > 0 ? (
              <Card title={t('requests.decided')}>
                <RequestList items={decided} />
              </Card>
            ) : null}
          </>
        )}
      </TabScreen>

      {/* Tugma pastda qadalgan: ro'yxat uzayganda ham «So'rov
          yuborish» aylantirish oxirida qolib ketmaydi. */}
      <BottomAction overTabBar>
        <Button block onClick={() => setTypeOpen(true)}>
          {t('requests.create')}
        </Button>
      </BottomAction>

      <RequestTypeSheet open={typeOpen} onClose={() => setTypeOpen(false)} />
    </>
  )
}

/**
 * «So'rov turini tanlang» (INTAKE-verifix-ilova §3.6). Verifix'da sakkizta
 * band; bizda — ma'lumoti bor ikkitasi (UX sarlavhasidagi qamrov qoidasi).
 * Yorliqlar to'liq so'z: Verifix'dagi «Jadval o'zg.» kabi qisqartma yo'q.
 */
function RequestTypeSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const go = (to: string): void => {
    onClose()
    navigate(to)
  }

  return (
    <Sheet open={open} title={t('requests.typeTitle')} onClose={onClose}>
      <div className="overflow-hidden rounded-card bg-bg-elevated">
        <TypeRow
          icon="document-text"
          label={t('requests.typeAbsence')}
          text={t('requests.typeAbsenceText')}
          onClick={() => go('/sorovlar/yangi')}
        />
        <TypeRow
          icon="calendar"
          label={t('requests.typeSchedule')}
          text={t('requests.typeScheduleText')}
          onClick={() => go('/sorovlar/jadval/yangi')}
        />
      </div>

      <div className="mt-4">
        <Button variant="plain" block onClick={onClose}>
          {t('common.close')}
        </Button>
      </div>
    </Sheet>
  )
}

function TypeRow({
  icon,
  label,
  text,
  onClick,
}: {
  icon: IconName
  label: string
  text: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex min-h-[64px] w-full items-center border-t border-border px-1 py-3 text-left first:border-t-0 active:bg-bg-pressed"
    >
      <Icon name={icon} className="mr-3 h-6 w-6 text-fg-secondary" />
      <span className="min-w-0 flex-1">
        <span className="block text-body text-fg">{label}</span>
        <span className="mt-0.5 block text-footnote text-fg-secondary">{text}</span>
      </span>
      <Icon name="chevron-right" className="ml-2 h-5 w-5 text-fg-tertiary" />
    </button>
  )
}

function RequestList({ items }: { items: readonly AnyRequest[] }) {
  const { t } = useTranslation()
  const format = useFormat()

  return (
    <div>
      {items.map((item) =>
        item.type === 'absence' ? (
          <RequestRow
            key={`a-${item.id}`}
            to={`/sorovlar/${item.id}`}
            title={item.absence.absenceTypeName}
            lines={[
              item.absence.fromDate === item.absence.toDate
                ? format.date(item.absence.fromDate)
                : `${format.date(item.absence.fromDate)} — ${format.date(item.absence.toDate)}`,
            ]}
            status={item}
          />
        ) : (
          <RequestRow
            key={`j-${item.id}`}
            to={`/sorovlar/jadval/${item.id}`}
            title={t(scheduleTitleKey(item.schedule))}
            lines={scheduleSummaryLines(t, format, item.schedule)}
            status={item}
          />
        ),
      )}
    </div>
  )
}

function RequestRow({
  to,
  title,
  lines,
  status,
}: {
  to: string
  title: string
  /** Ikkinchi satrlar — faqat shular qisqartiriladi (UX §F.5). */
  lines: readonly string[]
  status: AnyRequest
}) {
  return (
    <Link
      to={to}
      className="flex min-h-[64px] items-center border-t border-border py-2 first:border-t-0 active:bg-bg-pressed"
    >
      <div className="min-w-0 flex-1">
        <div className="text-body text-fg server-text">{title}</div>
        {lines.map((line, index) => (
          <div key={index} className="nums mt-0.5 truncate text-footnote text-fg-secondary">
            {line}
          </div>
        ))}
      </div>
      <div className="ml-3 shrink-0">
        <StatusChip look={REQUEST_STATUS_LOOK[status.status]} size="sm" />
      </div>
    </Link>
  )
}
