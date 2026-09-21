import { useEffect, useState } from 'react'

import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import type { AbsenceRequest, AbsenceRequestInput } from '../api/attendance-api'
import { useCreateAbsenceRequest, useUpdateAbsenceRequest } from '../api/attendance-queries'

/**
 * Yo'qlik so'rovini yaratish va tahrirlash oynasi (SPEC-so'rovlar §B5).
 *
 * <b>Birinchi bosqichda so'rovni HR kiritadi</b> — xodim sababni telefonda
 * aytadi. Ikkinchi bosqichda xuddi shu yozuvni xodimning o'z ilovasi
 * yuboradi (§C) va bu oyna HR uchun «xodim nomidan kiritish» yo'li bo'lib
 * qoladi.
 *
 * <b>Vaqt oralig'i faqat BIR KUNLIK so'rovda</b>: sanalar har xil bo'lsa
 * maydonlar umuman chizilmaydi. Server ham shu qoidani tekshiradi
 * (`davomat.sorov_vaqti_notogri`) — bu yerdagisi faqat qo'lni tinchitish
 * uchun emas, ekranda ma'nosiz maydon turmasligi uchun.
 */
export function AbsenceRequestDialog({
  open,
  employeeId,
  request,
  onClose,
}: {
  open: boolean
  /** Yangi so'rov uchun xodim — tahrirlashda `request` dan olinadi. */
  employeeId: string | null
  /** Tahrirlanayotgan so'rov; `null` — yangisi. */
  request: AbsenceRequest | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const types = useCatalogList('absence-types', true)
  const create = useCreateAbsenceRequest()
  const update = useUpdateAbsenceRequest()

  const [typeId, setTypeId] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [fromTime, setFromTime] = useState('')
  const [toTime, setToTime] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  /*
    Oyna ochilganda forma to'ldiriladi. `open` ham bog'liqlikda: bitta
    oyna ketma-ket ikki xil so'rov uchun ochilishi mumkin va ikkinchisida
    eski qiymatlar qolib ketmasligi kerak.
  */
  useEffect(() => {
    if (!open) return

    setTypeId(request?.absenceTypeId ?? '')
    setFromDate(request?.fromDate ?? '')
    setToDate(request?.toDate ?? '')
    setFromTime(request?.fromTime?.slice(0, 5) ?? '')
    setToTime(request?.toTime?.slice(0, 5) ?? '')
    setNote(request?.note ?? '')
    setError(null)
  }, [open, request])

  const singleDay = fromDate !== '' && fromDate === toDate
  const saving = create.isPending || update.isPending

  const submit = async () => {
    setError(null)

    const body: AbsenceRequestInput = {
      employeeId: request?.employeeId ?? employeeId ?? '',
      absenceTypeId: typeId,
      fromDate,
      toDate,
      // Vaqt faqat bir kunlik so'rovda yuboriladi: ko'p kunlikda server
      // uni baribir rad etadi va ekranda tushunarsiz xato chiqardi.
      fromTime: singleDay && fromTime !== '' ? `${fromTime}:00` : undefined,
      toTime: singleDay && toTime !== '' ? `${toTime}:00` : undefined,
      note: note.trim() === '' ? undefined : note.trim(),
    }

    try {
      if (request === null) {
        await create.mutateAsync(body)
      } else {
        await update.mutateAsync({ id: request.id, body })
      }

      onClose()
    } catch (failure) {
      setError(errorMessage(problemCode(failure), t('attendance.requestSaveFailed')))
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="md"
      dismissible={!saving}
      title={request === null ? t('attendance.requestAdd') : t('attendance.requestEdit')}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={() => void submit()}
            disabled={saving || typeId === '' || fromDate === '' || toDate === ''}
          >
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {error !== null && <Alert tone="danger">{error}</Alert>}

        <Select
          label={t('attendance.requestKind')}
          value={typeId}
          onChange={(event) => setTypeId(event.target.value)}
          options={(types.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
          placeholder={t('attendance.requestSelectKind')}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('common.dateFrom')}
            type="date"
            value={fromDate}
            onChange={(event) => {
              const next = event.target.value
              setFromDate(next)
              // Bir kunlik so'rov — eng ko'p uchraydigani: oxirgi sana
              // bo'sh bo'lsa, u o'zi to'ladi va HR ikki marta yozmaydi.
              if (toDate === '' || toDate < next) setToDate(next)
            }}
          />
          <TextField
            label={t('common.dateTo')}
            type="date"
            value={toDate}
            min={fromDate === '' ? undefined : fromDate}
            onChange={(event) => setToDate(event.target.value)}
          />
        </div>

        {singleDay && (
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={`${t('attendance.requestPeriod')} — ${t('attendance.directionIn')}`}
              type="time"
              value={fromTime}
              onChange={(event) => setFromTime(event.target.value)}
              hint={t('attendance.requestSingleDayHint')}
            />
            <TextField
              label={`${t('attendance.requestPeriod')} — ${t('attendance.directionOut')}`}
              type="time"
              value={toTime}
              onChange={(event) => setToTime(event.target.value)}
            />
          </div>
        )}

        <TextArea
          label={t('attendance.requestNote')}
          value={note}
          onChange={(event) => setNote(event.target.value)}
          rows={3}
        />
      </div>
    </Dialog>
  )
}
