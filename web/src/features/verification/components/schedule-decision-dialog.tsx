import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea } from '@/shared/ui'

import type { ScheduleChangeRequest } from '../api/attendance-api'
import {
  isStaleRequestError,
  useCancelScheduleChangeRequest,
  useDecideScheduleChangeRequest,
} from '../api/attendance-queries'
import { SCHEDULE_KIND_LABEL_KEY } from '../lib/attendance-presentation'
import { useScheduleRequestText } from '../lib/schedule-request-text'
import type { AbsenceAction } from './absence-decision-dialog'
import { RequestEditedNote } from './request-edited-note'

/** Qaror izohi chegarasi — serverdagi `ScheduleChangeRequest.MaxNoteLength`. */
const NOTE_MAX = 2000

/**
 * Jadval so'rovi bo'yicha qaror oynasi — `AbsenceDecisionDialog` ning egizagi
 * (o'sha uchta amal, o'sha izoh maydoni, o'sha sabab bilan brauzerning
 * `confirm()` i emas).
 *
 * <b>Farqi — kunlarning TO'LIQ ro'yxati.</b> Ro'yxat katagida uchta sana
 * ko'rinadi, so'rovda esa 31 tagacha bo'lishi mumkin. HR nimani tasdiqlayotganini
 * shu yerda butunligicha ko'radi: tasdiqlangan kun xodimning o'sha sanadagi
 * rejasi O'RNIGA turadi (SPEC-jadval-sorovi §C2).
 *
 * Eskirgan nusxa — `AbsenceDecisionDialog` dagi qoidaning o'zi: qaror HR
 * ko'rgan `version` bilan ketadi, 409 da faqat «Yopish» qoladi.
 */
export function ScheduleDecisionDialog({
  action,
  request,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  action: AbsenceAction | null
  request: ScheduleChangeRequest | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const text = useScheduleRequestText()

  const decide = useDecideScheduleChangeRequest()
  const cancel = useCancelScheduleChangeRequest()

  const [note, setNote] = useState('')

  // Har ochilishda izoh va mutatsiya holati tiklanadi (`AbsenceDecisionDialog` izohi)
  useEffect(() => {
    if (action === null) return

    setNote('')
    decide.reset()
    cancel.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [action, request?.id])

  if (action === null || request === null) {
    return null
  }

  const busy = decide.isPending || cancel.isPending
  const failure = decide.isError ? decide.error : cancel.isError ? cancel.error : null
  const stale = failure !== null && isStaleRequestError(failure)

  const look = {
    approve: {
      title: t('attendance.requestApprove'),
      hint: t('attendance.scheduleRequestApproveHint'),
      tone: 'primary' as const,
      withNote: true,
    },
    reject: {
      title: t('attendance.requestReject'),
      hint: t('attendance.scheduleRequestRejectHint'),
      tone: 'danger' as const,
      withNote: true,
    },
    cancel: {
      title: t('attendance.requestCancel'),
      hint: t('attendance.scheduleRequestCancelConfirm'),
      tone: 'danger' as const,
      withNote: false,
    },
  }[action]

  const submit = () => {
    const trimmed = note.trim()

    if (action === 'cancel') {
      cancel.mutate(request.id, { onSuccess: onClose })
      return
    }

    decide.mutate(
      {
        id: request.id,
        body: {
          approve: action === 'approve',
          decisionNote: trimmed === '' ? undefined : trimmed,
          // HR ko'rgan kunlar — xodim shu orada o'zgartirgan bo'lsa server 409 beradi
          version: request.version,
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!busy}
      title={look.title}
      // KIM va QAYSI tur — qaror noto'g'ri qatorga tushib ketmasin
      description={
        <>
          {`${request.employeeName} · ${t(SCHEDULE_KIND_LABEL_KEY[request.kind])}`}
          <RequestEditedNote request={request} className="mt-1" />
        </>
      }
      footer={
        stale ? (
          <Button type="button" onClick={onClose}>
            {t('common.close')}
          </Button>
        ) : (
          <>
            <Button type="button" onClick={onClose} disabled={busy}>
              {t('common.cancel')}
            </Button>
            <Button type="button" variant={look.tone} disabled={busy} onClick={submit}>
              {busy && <LoaderCircle className="animate-spin" />}
              {look.title}
            </Button>
          </>
        )
      }
    >
      <div className="space-y-4">
        {failure !== null &&
          (stale ? (
            <Alert tone="warning" title={errorMessage(problemCode(failure))}>
              {t('attendance.requestStaleHint')}
            </Alert>
          ) : (
            <Alert tone="danger">
              {errorMessage(problemCode(failure), t('attendance.requestSaveFailed'))}
            </Alert>
          ))}

        <p className="text-subhead text-[var(--fg-secondary)]">{look.hint}</p>

        <ul className="divide-y divide-[var(--border)] rounded-md border border-[var(--border)]">
          {text.lines(request).map((line) => (
            <li key={line} className="tabular px-3 py-2 text-footnote">
              {line}
            </li>
          ))}
        </ul>

        {request.note !== null && (
          <p className="rounded-md bg-[var(--bg-sunken)] p-3 text-footnote text-[var(--fg-secondary)]">
            {request.note}
          </p>
        )}

        {look.withNote && (
          <TextArea
            label={t('attendance.requestDecisionNote')}
            value={note}
            maxLength={NOTE_MAX}
            disabled={stale}
            onChange={(event) => setNote(event.target.value)}
            rows={3}
          />
        )}
      </div>
    </Dialog>
  )
}
