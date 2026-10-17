import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import { invalidateScheduleRequests, useScheduleRequests } from '@/data/queries'
import { shortTime } from '@/data/attendance'
import { scheduleTitleKey } from '@/data/schedule-requests'
import { REQUEST_STATUS_LOOK } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { Button } from '@/ui/button'
import { Card, CardRow } from '@/ui/card'
import { EmptyState, ErrorState, InlineMessage, SkeletonLines } from '@/ui/feedback'
import { ConfirmSheet } from '@/ui/sheet'
import { StatusChip } from '@/ui/status-chip'
import { useToast } from '@/ui/toast'

/**
 * BITTA JADVAL SO'ROVI (SPEC-jadval-sorovi §C4) — `request-detail` ning
 * egizagi: kunlar, izoh, HR qarori va qaror kutayotganda bekor qilish.
 *
 * Marshrutdagi identifikator — MENING yozuvimniki (UX §0.2).
 */
export function ScheduleRequestDetailScreen() {
  const { t } = useTranslation()
  const format = useFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const errorText = useErrorText()
  const { id } = useParams<{ id: string }>()

  const requests = useScheduleRequests()
  const request = (requests.data ?? []).filter((item) => item.id === id)[0] ?? null

  const [confirmOpen, setConfirmOpen] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState<ApiError | null>(null)

  const cancel = async (): Promise<void> => {
    if (!id) return

    setCancelling(true)
    setCancelError(null)

    try {
      await api.cancelScheduleRequest(id)
      invalidateScheduleRequests()
      requests.refetch()
      setConfirmOpen(false)
      toast.show(t('requests.cancelled'), 'success')
    } catch (cause: unknown) {
      setCancelError(ApiError.from(cause))
      setConfirmOpen(false)
    } finally {
      setCancelling(false)
    }
  }

  return (
    <PushedScreen title={t('schedule.detailTitle')}>
      {requests.isLoading ? (
        <Card>
          <SkeletonLines rows={5} />
        </Card>
      ) : requests.error && !requests.data ? (
        <Card>
          <ErrorState onRetry={requests.refetch} />
        </Card>
      ) : !request ? (
        <Card>
          <EmptyState
            icon="document-text"
            title={t('requests.notFound')}
            text={t('requests.notFoundText')}
            action={<Button onClick={() => navigate('/sorovlar')}>{t('requests.title')}</Button>}
          />
        </Card>
      ) : (
        <>
          <Card>
            <div className="mb-3">
              <StatusChip look={REQUEST_STATUS_LOOK[request.status]} />
            </div>

            <CardRow label={t('schedule.kind')} value={t(scheduleTitleKey(request))} />
            <CardRow
              label={t('requests.createdAt')}
              value={<span className="nums">{format.date(request.createdAt)}</span>}
            />
          </Card>

          {/* Har bir sana — so'rov TASDIQLANSA amal qiladigan reja.
              Almashtirishda bu juftining eski rejasi (server yaratishda
              nusxa olgan), shu sababli juft sanasi ham yoziladi. */}
          <Card title={t('schedule.days')}>
            <div>
              {request.days.map((day) => (
                <div key={day.date} className="border-t border-border py-2.5 first:border-t-0 first:pt-0">
                  <div className="flex min-w-0 items-baseline justify-between">
                    <span className="mr-3 min-w-0 text-subhead text-fg">{format.dateLong(day.date)}</span>
                    <span className="nums shrink-0 text-right text-subhead text-fg">
                      {day.isWorking
                        ? `${shortTime(day.startTime) ?? '—'}–${shortTime(day.endTime) ?? '—'}`
                        : t('schedule.dayOff')}
                    </span>
                  </div>
                  {day.isWorking && day.breakStart && day.breakEnd ? (
                    <p className="nums mt-0.5 text-right text-footnote text-fg-secondary">
                      {t('calendar.breakRange', {
                        range: `${shortTime(day.breakStart) ?? ''}–${shortTime(day.breakEnd) ?? ''}`,
                      })}
                    </p>
                  ) : null}
                  {day.swapWith ? (
                    <p className="nums mt-0.5 text-footnote text-fg-secondary">
                      {t('schedule.swapWith', { date: format.date(day.swapWith) })}
                    </p>
                  ) : null}
                </div>
              ))}
            </div>
          </Card>

          {request.note ? (
            <Card title={t('requests.note')}>
              <p className="text-subhead text-fg server-text">{request.note}</p>
            </Card>
          ) : null}

          {request.decisionNote || request.decidedByName ? (
            <Card title={t('requests.decisionNote')}>
              {request.decisionNote ? (
                <p className="text-subhead text-fg server-text">{request.decisionNote}</p>
              ) : null}
              {request.decidedByName ? (
                <p className="mt-2 text-footnote text-fg-secondary server-text">
                  {request.decidedByName}
                  {request.decidedAt ? ` · ${format.date(request.decidedAt)}` : ''}
                </p>
              ) : null}
            </Card>
          ) : null}

          {cancelError ? (
            <InlineMessage tone="danger" title={t('requests.cancelFailed')} assertive>
              {errorText(cancelError).body}
            </InlineMessage>
          ) : null}

          {/* Bekor qilish — faqat qaror kutayotgan so'rovda. */}
          {request.status === 'Pending' ? (
            <Button variant="danger" block onClick={() => setConfirmOpen(true)}>
              {t('requests.cancel')}
            </Button>
          ) : null}
        </>
      )}

      <ConfirmSheet
        open={confirmOpen}
        title={t('requests.cancelTitle')}
        text={t('requests.cancelText')}
        confirmLabel={t('requests.cancel')}
        danger
        busy={cancelling}
        onConfirm={() => void cancel()}
        onClose={() => setConfirmOpen(false)}
      />
    </PushedScreen>
  )
}
