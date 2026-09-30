import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea } from '@/shared/ui'

import type { AbsenceRequest } from '../api/attendance-api'
import { useCancelAbsenceRequest, useDecideAbsenceRequest } from '../api/attendance-queries'

/** Qaysi amal tasdiqlanmoqda. */
export type AbsenceAction = 'approve' | 'reject' | 'cancel'

/**
 * Yo'qlik so'rovi bo'yicha qarorni tasdiqlash oynasi.
 *
 * <b>Nega brauzerning <c>confirm()</c> i emas</b> (egasi, 2026-09-19): u
 * tizimning oynasi — o'z shrifti, o'z tugmalari va «localhost:7273 says»
 * degan sarlavhasi bilan. Ekranning qolgan qismi iOS uslubida turganda bu
 * yamoq bo'lib ko'rinadi, tarjima qilinmaydi va uning ichiga IZOH
 * maydonini ham qo'yib bo'lmaydi.
 *
 * <b>Izoh aynan shu yerda</b>: Verifix'dagi «Примечание руководителя» —
 * qaror bilan BIRGA yoziladigan gap («ertaga hujjat keltiradi», «sababi
 * yetarli emas»). Ilgari u umuman yuborilmasdi va HR qarorini
 * tushuntirolmasdi.
 *
 * Uchala amal bitta oynada: ular bir xil savolga javob beradi («shu
 * qarorni qabul qilasizmi?») va ikkita alohida komponent yozilsa,
 * bittasiga izoh qo'shilib, ikkinchisiga qo'shilmay qolishi mumkin edi.
 */
export function AbsenceDecisionDialog({
  action,
  request,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  action: AbsenceAction | null
  request: AbsenceRequest | null
  onClose: () => void
}) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()

  const decide = useDecideAbsenceRequest()
  const cancel = useCancelAbsenceRequest()

  const [note, setNote] = useState('')

  /*
    Oyna har ochilganda izoh tozalanadi: bitta oyna ketma-ket ikki xil
    so'rov uchun ochiladi va oldingi qarorning izohi yangisiga o'tib
    ketmasligi kerak. Mutatsiya holati ham tiklanadi — aks holda eski
    xato yangi oynada chiqib turardi.
  */
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

  const look = {
    approve: {
      title: t('attendance.requestApprove'),
      hint: t('attendance.requestApproveHint'),
      tone: 'primary' as const,
      label: t('attendance.requestApprove'),
      withNote: true,
    },
    reject: {
      title: t('attendance.requestReject'),
      hint: t('attendance.requestRejectHint'),
      tone: 'danger' as const,
      label: t('attendance.requestReject'),
      withNote: true,
    },
    cancel: {
      title: t('attendance.requestCancel'),
      hint: t('attendance.requestCancelConfirm'),
      tone: 'danger' as const,
      label: t('attendance.requestCancel'),
      withNote: false,
    },
  }[action]

  const period =
    request.fromDate === request.toDate
      ? date(request.fromDate)
      : `${date(request.fromDate)} – ${date(request.toDate)}`

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
      // Sarlavha ostida — KIM va QAYSI kunlar haqida gap ketayotgani.
      // Navbatda o'nlab qator bo'lishi mumkin va qaror noto'g'ri qatorga
      // tushib ketmasligi kerak.
      description={`${request.employeeName} · ${request.absenceTypeName} · ${period}`}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </Button>
          <Button type="button" variant={look.tone} disabled={busy} onClick={submit}>
            {busy && <LoaderCircle className="animate-spin" />}
            {look.label}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && (
          <Alert tone="danger">
            {errorMessage(problemCode(failure), t('attendance.requestSaveFailed'))}
          </Alert>
        )}

        <p className="text-subhead text-[var(--fg-secondary)]">{look.hint}</p>

        {request.note !== null && (
          <p className="rounded-md bg-[var(--bg-sunken)] p-3 text-footnote text-[var(--fg-secondary)]">
            {request.note}
          </p>
        )}

        {look.withNote && (
          <TextArea
            label={t('attendance.requestDecisionNote')}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            rows={3}
          />
        )}
      </div>
    </Dialog>
  )
}
