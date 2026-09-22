import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import { requestDayCount } from '@/data/attendance'
import { invalidateRequests, useRequests } from '@/data/queries'
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
 * BITTA SO'ROV (UX §A.3).
 *
 * Marshrutdagi identifikator — MENING yozuvimniki, hech qachon
 * odamniki (UX §0.2): bu ilovada boshqa xodim umuman mavjud emas.
 */
export function RequestDetailScreen() {
  const { t } = useTranslation()
  const format = useFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const errorText = useErrorText()
  const { id } = useParams<{ id: string }>()

  const requests = useRequests()
  const request = (requests.data ?? []).filter((item) => item.id === id)[0] ?? null

  const [confirmOpen, setConfirmOpen] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState<ApiError | null>(null)

  const cancel = async (): Promise<void> => {
    if (!id) return

    setCancelling(true)
    setCancelError(null)

    try {
      await api.cancelRequest(id)
      invalidateRequests()
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
    <PushedScreen title={t('requests.detailTitle')}>
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

            <CardRow label={t('requests.kind')} value={request.absenceTypeName} />
            <CardRow
              label={t('requests.period')}
              value={
                <span className="nums">
                  {request.fromDate === request.toDate
                    ? format.date(request.fromDate)
                    : t('requests.periodRange', {
                        from: format.date(request.fromDate),
                        to: format.date(request.toDate),
                      })}
                </span>
              }
            />
            {requestDayCount(request) > 1 ? (
              <CardRow
                label={t('requests.wholeDay')}
                value={t('requests.periodDays', { n: requestDayCount(request) })}
              />
            ) : null}
            {request.fromTime && request.toTime ? (
              <CardRow
                label={t('requests.fromTime')}
                value={<span className="nums">{`${request.fromTime}–${request.toTime}`}</span>}
              />
            ) : null}
            <CardRow
              label={t('requests.createdAt')}
              value={<span className="nums">{format.date(request.createdAt)}</span>}
            />
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

          {/* Bekor qilish — faqat qaror kutayotgan so'rovda. Qaror
              qabul qilingan so'rovni xodim o'zgartira olmaydi. */}
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
