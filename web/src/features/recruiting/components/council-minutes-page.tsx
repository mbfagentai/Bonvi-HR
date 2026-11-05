import { getRouteApi } from '@tanstack/react-router'
import { Gavel, Printer, Users } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Button, DateField, EmptyState, IconTile, PageHeader, Skeleton } from '@/shared/ui'
import { useCouncilDays, useCouncilMeeting } from '../api/council-queries'
import { defaultCouncilDay, tashkentToday } from '../lib/council-days'
import { CouncilCandidateCard } from './council-candidate-card'
import { CouncilMembersCard } from './council-members-card'
import { CouncilPrintSheet } from './council-print-sheet'

const route = getRouteApi('/_app/kengash-xulosasi')

const RECRUITING_MANAGE = 'recruiting.manage'

/** Days shown as quick chips: the latest past ones and everything coming. */
const QUICK_DAYS = 6

/** Council's minutes: pick a day, tick attendance, write each invited candidate's decision, print. */
export function CouncilMinutesPage() {
  const { t, date: formatDate } = useLocale()
  const { hasPermission } = useAuth()
  const navigate = route.useNavigate()
  const search = route.useSearch()
  const days = useCouncilDays()

  const date = search.date ?? (days.data ? defaultCouncilDay(days.data, tashkentToday()) : null)
  const open = (next: string) => void navigate({ search: { date: next } })

  return (
    <div className="space-y-5">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={Gavel} tone="brand" size="lg" />
            {t('council.title')}
          </span>
        }
        actions={
          <Button variant="secondary" onClick={() => window.print()} disabled={date === null}>
            <Printer />
            {t('council.printButton')}
          </Button>
        }
      />

      <div className="flex flex-wrap items-end gap-3">
        <DateField
          label={t('council.day')}
          value={date ?? ''}
          onChange={(value) => {
            if (value !== '') open(value)
          }}
        />
        <div className="flex flex-wrap gap-2">
          {days.data?.slice(-QUICK_DAYS).map((day) => (
            <button
              key={day.date}
              type="button"
              onClick={() => open(day.date)}
              className={cn(
                'rounded-full border px-3 py-2 text-sm transition-colors',
                day.date === date
                  ? 'border-transparent bg-[var(--accent-soft)] font-medium text-[var(--accent-on-soft)]'
                  : 'border-[var(--border)] bg-[var(--bg-elevated)] text-[var(--fg-secondary)] hover:text-[var(--fg)]',
              )}
            >
              {t('council.dayChip', { date: formatDate(day.date), count: day.invited })}
            </button>
          ))}
        </div>
      </div>

      {date !== null && <CouncilDayView key={date} date={date} canManage={hasPermission(RECRUITING_MANAGE)} />}
    </div>
  )
}

function CouncilDayView({ date, canManage }: { date: string; canManage: boolean }) {
  const { t } = useLocale()
  const meeting = useCouncilMeeting(date)

  if (meeting.isPending) {
    return <Skeleton className="h-64 w-full rounded-2xl" />
  }

  if (meeting.isError) {
    return <Alert tone="danger">{t('council.loadError')}</Alert>
  }

  const { data } = meeting
  const decided = data.candidates.filter((row) => row.decided).length

  return (
    <div className="space-y-4">
      <p className="text-sm text-[var(--fg-secondary)]">
        {data.number !== null && `${t('council.number', { number: data.number })} · `}
        {t('council.progress', { decided, total: data.candidates.length })}
      </p>

      <CouncilMembersCard meeting={data} canManage={canManage} />

      {data.candidates.length === 0 ? (
        <EmptyState icon={Users} title={t('council.empty')} />
      ) : (
        data.candidates.map((row) => (
          <CouncilCandidateCard
            key={row.candidateId}
            date={date}
            row={row}
            canManage={canManage}
            canSeeSalary={data.canSeeSalary}
          />
        ))
      )}

      <CouncilPrintSheet meeting={data} />
    </div>
  )
}
