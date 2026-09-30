import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'

import type { AbsenceRequestDto } from '@/api/types'
import { useRequests } from '@/data/queries'
import { REQUEST_STATUS_LOOK } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { TabScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { EmptyState, ErrorState, SkeletonLines } from '@/ui/feedback'
import { StatusChip } from '@/ui/status-chip'

/**
 * SO'ROVLAR ro'yxati (UX §A.5).
 *
 * Ikki bo'lim: qaror kutayotganlar tepada. Sabab oddiy — HR ning
 * qarori bu ilovada xodim boshidan o'tadigan YAGONA asinxron narsa,
 * va u birinchi ko'rinishi kerak.
 */
export function RequestsScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const requests = useRequests()

  const all = requests.data ?? []
  const pending = all.filter((request) => request.status === 'Pending')
  const decided = all.filter((request) => request.status !== 'Pending')

  return (
    <>
      <TabScreen title={t('requests.title')} staleSince={requests.fetchedAt} bottomSpace>
        {requests.isLoading ? (
          <Card>
            <SkeletonLines rows={4} />
          </Card>
        ) : requests.error && !requests.data ? (
          <Card>
            <ErrorState onRetry={requests.refetch} />
          </Card>
        ) : all.length === 0 ? (
          <Card>
            <EmptyState
              icon="document-text"
              title={t('requests.emptyTitle')}
              text={t('requests.emptyText')}
              action={
                <Button onClick={() => navigate('/sorovlar/yangi')}>{t('requests.create')}</Button>
              }
            />
          </Card>
        ) : (
          <>
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
        <Button block onClick={() => navigate('/sorovlar/yangi')}>
          {t('requests.create')}
        </Button>
      </BottomAction>
    </>
  )
}

function RequestList({ items }: { items: readonly AbsenceRequestDto[] }) {
  const format = useFormat()

  return (
    <div>
      {items.map((request) => (
        <Link
          key={request.id}
          to={`/sorovlar/${request.id}`}
          className="flex min-h-[64px] items-center border-t border-border py-2 first:border-t-0 active:bg-bg-pressed"
        >
          <div className="min-w-0 flex-1">
            <div className="truncate text-body text-fg server-text">{request.absenceTypeName}</div>
            <div className="nums mt-0.5 truncate text-footnote text-fg-secondary">
              {request.fromDate === request.toDate
                ? format.date(request.fromDate)
                : `${format.date(request.fromDate)} — ${format.date(request.toDate)}`}
            </div>
          </div>
          <div className="ml-3 shrink-0">
            <StatusChip look={REQUEST_STATUS_LOOK[request.status]} size="sm" />
          </div>
        </Link>
      ))}
    </div>
  )
}
