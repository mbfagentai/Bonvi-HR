import { useEffect, useState } from 'react'

import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  DateField,
  Dialog,
  SegmentedControl,
  Select,
  TextArea,
  TextField,
} from '@/shared/ui'

import {
  ABSENCE_REQUEST_MODES,
  type AbsenceRequest,
  type AbsenceRequestInput,
  type AbsenceRequestMode,
} from '../api/attendance-api'
import { useCreateAbsenceRequest, useUpdateAbsenceRequest } from '../api/attendance-queries'
import { REQUEST_MODE_LABEL_KEY, absenceRequestMode } from '../lib/attendance-presentation'

/**
 * Yo'qlik so'rovini yaratish va tahrirlash oynasi (SPEC-so'rovlar §B5).
 *
 * <b>Birinchi bosqichda so'rovni HR kiritadi</b> — xodim sababni telefonda
 * aytadi. Ikkinchi bosqichda xuddi shu yozuvni xodimning o'z ilovasi
 * yuboradi (§C) va bu oyna HR uchun «xodim nomidan kiritish» yo'li bo'lib
 * qoladi.
 *
 * <b>Shakl — Verifix'dagi kabi uchta</b> (SPEC-jadval-sorovi §B): «Qism kun»
 * (bitta sana, boshlanish va tugash — ikkalasi shart), «Butun kun» (bitta
 * sana) va «Bir necha kun» (oraliq). Ilgari ikki sana va ixtiyoriy vaqt
 * yonma-yon turardi va HR «bir kunlik, lekin vaqtsiz» so'rovni qanday
 * kiritishni taxmin qilishi kerak edi. Server shartnomasi O'ZGARMADI —
 * shakl faqat qaysi maydonlar yuborilishini belgilaydi.
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
  const [mode, setMode] = useState<AbsenceRequestMode>('PartDay')
  /** Bitta sanali shakllarda — o'sha sana; «Bir necha kun» da — oraliq boshi. */
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

    Yangi so'rov — «Qism kun», bugungi sana bilan: Verifix'dagi so'rovlarning
    deyarli hammasi soatbay («ish bilan bojxonaga bordi») va HR ularni
    odatda shu kuni kiritadi. Tahrirlashda shakl so'rovning o'zidan hosil
    qilinadi (server qoidasi — `absenceRequestMode`).
  */
  useEffect(() => {
    if (!open) return

    setTypeId(request?.absenceTypeId ?? '')
    setMode(request === null ? 'PartDay' : absenceRequestMode(request))
    setFromDate(request?.fromDate ?? todayIso())
    setToDate(request?.toDate ?? '')
    setFromTime(request?.fromTime?.slice(0, 5) ?? '')
    setToTime(request?.toTime?.slice(0, 5) ?? '')
    setNote(request?.note ?? '')
    setError(null)
  }, [open, request])

  const saving = create.isPending || update.isPending

  const changeMode = (next: AbsenceRequestMode) => {
    setMode(next)
    // Bir kunlik so'rovdan «Bir necha kun» ga o'tganda oxirgi sana bo'sh yoki
    // boshi bilan teng qoladi — u keyingi kunga to'ldiriladi (boshlanish
    // sanasidagi qoida bilan bir xil), HR faqat kerak bo'lsa suradi
    if (next === 'MultiDay' && fromDate !== '' && (toDate === '' || toDate <= fromDate)) {
      setToDate(nextDay(fromDate))
    }
  }

  /*
    Tartib xatolari maydon ostida, ikkala qiymat kiritilgandan keyin:
    bo'sh maydonga «noto'g'ri» deyish ma'nosiz, saqlash tugmasi esa baribir
    o'chiq turadi. Server ham xuddi shu qoidalarni tekshiradi
    (`davomat.sorov_vaqti_notogri`, `davomat.sorov_davri_notogri`).
  */
  const timeOrderError =
    mode === 'PartDay' && fromTime !== '' && toTime !== '' && toTime <= fromTime
      ? t('attendance.requestTimeOrder')
      : undefined
  const rangeOrderError =
    mode === 'MultiDay' && fromDate !== '' && toDate !== '' && toDate <= fromDate
      ? t('attendance.requestRangeOrder')
      : undefined

  const complete =
    typeId !== '' &&
    fromDate !== '' &&
    (mode !== 'PartDay' || (fromTime !== '' && toTime !== '')) &&
    (mode !== 'MultiDay' || toDate !== '') &&
    timeOrderError === undefined &&
    rangeOrderError === undefined

  const submit = async () => {
    setError(null)

    const single = mode !== 'MultiDay'

    const body: AbsenceRequestInput = {
      employeeId: request?.employeeId ?? employeeId ?? '',
      absenceTypeId: typeId,
      fromDate,
      toDate: single ? fromDate : toDate,
      // Vaqt faqat «Qism kun» da yuboriladi: boshqa shaklda u ko'rinmaydi
      // va eski qiymati (shakl almashtirilgandan keyin) jimgina ketib qolmasin
      fromTime: mode === 'PartDay' ? `${fromTime}:00` : undefined,
      toTime: mode === 'PartDay' ? `${toTime}:00` : undefined,
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
            disabled={saving || !complete}
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
          disabled={saving}
          onChange={(event) => setTypeId(event.target.value)}
          options={(types.data ?? []).map((item) => ({ value: item.id, label: item.name }))}
          placeholder={t('attendance.requestSelectKind')}
          // Ma'lumotnoma bo'sh bo'lsa ro'yxat jimgina bo'sh ochilardi va
          // HR nima qilishni bilmasdi (2026-10-17, prod'da shunday bo'ldi).
          hint={types.data?.length === 0 ? t('attendance.requestNoKinds') : undefined}
        />

        <SegmentedControl
          className="w-full"
          value={mode}
          onChange={changeMode}
          aria-label={t('attendance.requestMode')}
          options={ABSENCE_REQUEST_MODES.map((value) => ({
            value,
            label: t(REQUEST_MODE_LABEL_KEY[value]),
          }))}
        />

        {mode === 'MultiDay' ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <DateField
              label={t('common.dateFrom')}
              value={fromDate}
              disabled={saving}
              onChange={(next) => {
                setFromDate(next)
                // Oxirgi sana boshidan oldin yoki u bilan teng qolib ketmasin —
                // keyingi kunga suriladi (eng qisqa «bir necha kun» — ikki kun)
                if (next !== '' && (toDate === '' || toDate <= next)) setToDate(nextDay(next))
              }}
            />
            <DateField
              label={t('common.dateTo')}
              value={toDate}
              disabled={saving}
              min={fromDate === '' ? undefined : nextDay(fromDate)}
              onChange={setToDate}
              error={rangeOrderError}
            />
          </div>
        ) : (
          <DateField
            label={t('attendance.requestDate')}
            value={fromDate}
            disabled={saving}
            onChange={setFromDate}
          />
        )}

        {mode === 'PartDay' && (
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('attendance.startTime')}
              type="time"
              required
              value={fromTime}
              disabled={saving}
              onChange={(event) => setFromTime(event.target.value)}
            />
            <TextField
              label={t('attendance.endTime')}
              type="time"
              required
              value={toTime}
              disabled={saving}
              onChange={(event) => setToTime(event.target.value)}
              error={timeOrderError}
            />
          </div>
        )}

        <TextArea
          label={t('attendance.requestNote')}
          value={note}
          disabled={saving}
          onChange={(event) => setNote(event.target.value)}
          rows={3}
        />
      </div>
    </Dialog>
  )
}

/**
 * Keyingi kun: «2026-10-17» → «2026-10-18».
 *
 * UTC'da hisoblanadi — `DateOnly` satri lahza emas, brauzer mintaqasi uni
 * bir kunga siljitib yubormasin (`formatBirthday` izohi).
 */
function nextDay(iso: string): string {
  const parsed = new Date(`${iso}T00:00:00Z`)
  if (Number.isNaN(parsed.getTime())) return iso

  parsed.setUTCDate(parsed.getUTCDate() + 1)
  return parsed.toISOString().slice(0, 10)
}
