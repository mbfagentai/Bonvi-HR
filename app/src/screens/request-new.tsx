import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import { invalidateRequests, useMe } from '@/data/queries'
import { useFormat } from '@/lib/format'
import { addDays, daysBetween, parseIsoDate, todayIso } from '@/lib/time'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { ErrorState, InlineMessage, SkeletonLines } from '@/ui/feedback'
import { DateRow, Field, SegmentedControl, SelectRow, TextArea, TimeRow } from '@/ui/form'
import { ConfirmSheet } from '@/ui/sheet'
import { useToast } from '@/ui/toast'

/**
 * Davomiylik — Verifix'dagi «Часть дня / Весь день / Несколько дней»
 * (SPEC-jadval-sorovi §B). Server shartnomasi O'ZGARMAGAN: rejim simda
 * yo'q, u sana va vaqtlardan kelib chiqadi (vaqt bor → qism kun,
 * bitta sana → butun kun, aks holda — bir necha kun).
 */
type Mode = 'part' | 'whole' | 'multi'

/**
 * YO'QLIK SO'ROVI formasi (UX §A.3, §E.5).
 *
 * Verifix'da «so'rov turini tanlang» varag'ida sakkizta band bor edi;
 * bizda ikkitasi — yo'qlik va jadval (SPEC-jadval-sorovi §C4). Yo'qlik
 * TURI esa shu formadagi maydon, chunki u ma'lumotnomadan keladi
 * (UX §A.1).
 */
export function RequestNewScreen() {
  const { t } = useTranslation()
  const format = useFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const errorText = useErrorText()
  const [params] = useSearchParams()

  const me = useMe()

  const prefill = parseIsoDate(params.get('kun')) ? `${params.get('kun')}` : todayIso()

  // Sukut — «Qism kun»: Verifix formasi ham «Soatlik» bilan ochiladi va
  // MBF dagi so'rovlarning ko'pi bir necha soatlik.
  const [mode, setMode] = useState<Mode>('part')
  const [absenceTypeId, setAbsenceTypeId] = useState<string | null>(null)
  const [fromDate, setFromDate] = useState(prefill)
  // Faqat «Bir necha kun» da o'qiladi — doim boshlanishdan KEYIN turadi.
  const [toDate, setToDate] = useState(addDays(prefill, 1))
  const [fromTime, setFromTime] = useState('')
  const [toTime, setToTime] = useState('')
  const [note, setNote] = useState('')

  const [touched, setTouched] = useState(false)
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<ApiError | null>(null)
  const [leaveOpen, setLeaveOpen] = useState(false)

  // «Iflos» forma — yozilgan narsa bor. Tasdiq faqat shu holatda
  // so'raladi: bo'sh formadan chiqishda savol berish — bezovta qilish.
  // Rejimni almashtirishning o'zi «yozish» emas.
  const dirty =
    absenceTypeId !== null ||
    note.length > 0 ||
    fromDate !== prefill ||
    toDate !== addDays(prefill, 1) ||
    fromTime !== '' ||
    toTime !== ''

  const kindError = touched && !absenceTypeId ? t('requests.kindRequired') : null

  // Qism kun: ikkala vaqt MAJBURIY va bir kun ichida (yarim tundan o'tgan
  // yo'qlikni server rad etadi — `davomat.sorov_vaqti_notogri`).
  const fromTimeError = touched && mode === 'part' && fromTime === '' ? t('requests.timeRequired') : null
  const toTimeError = !touched || mode !== 'part'
    ? null
    : toTime === ''
      ? t('requests.timeRequired')
      : fromTime !== '' && toTime <= fromTime
        ? t('requests.timeOrder')
        : null

  const dateError = touched && mode === 'multi' && toDate <= fromDate ? t('requests.dateAfter') : null
  const dayCount = daysBetween(fromDate, toDate)

  const changeMode = (next: Mode): void => {
    setMode(next)
    if (next === 'multi' && toDate <= fromDate) setToDate(addDays(fromDate, 1))
  }

  const changeFromDate = (iso: string): void => {
    setFromDate(iso)
    // Oxiri boshidan oldin qolib ketmasin — eng ko'p uchraydigan xatoni
    // forma o'zi tuzatadi.
    if (toDate <= iso) setToDate(addDays(iso, 1))
  }

  const submit = async (): Promise<void> => {
    setTouched(true)
    if (!absenceTypeId) return
    if (mode === 'part' && (fromTime === '' || toTime === '' || toTime <= fromTime)) return
    if (mode === 'multi' && toDate <= fromDate) return

    setSending(true)
    setSendError(null)

    try {
      // Vaqt faqat «Qism kun» da yuboriladi: boshqa rejimda tanlangan
      // vaqt holatda qoladi (xodim qaytsa yo'qolmaydi), lekin simga
      // chiqmaydi — server uni ko'p kunlik so'rovda rad etardi.
      await api.createRequest({
        absenceTypeId,
        fromDate,
        toDate: mode === 'multi' ? toDate : fromDate,
        fromTime: mode === 'part' ? `${fromTime}:00` : null,
        toTime: mode === 'part' ? `${toTime}:00` : null,
        note: note.trim() === '' ? null : note.trim(),
      })

      invalidateRequests()
      toast.show(t('requests.sent'), 'success')
      navigate('/sorovlar', { replace: true })
    } catch (cause: unknown) {
      setSendError(ApiError.from(cause))
    } finally {
      setSending(false)
    }
  }

  const goBack = (): void => {
    if (dirty) {
      setLeaveOpen(true)
      return
    }
    navigate(-1)
  }

  const summary =
    mode === 'part'
      ? fromTime !== '' && toTime !== ''
        ? `${format.dateLong(fromDate)}, ${fromTime}–${toTime}`
        : format.dateLong(fromDate)
      : mode === 'whole'
        ? format.dateLong(fromDate)
        : `${t('requests.periodRange', { from: format.date(fromDate), to: format.date(toDate) })}${
            dayCount > 1 ? ` · ${t('requests.periodDays', { n: dayCount })}` : ''
          }`

  return (
    <>
      <PushedScreen title={t('requests.newTitle')} onBack={goBack} bottomSpace>
        <Card>
          {me.isLoading ? (
            <SkeletonLines rows={4} />
          ) : me.error && !me.data ? (
            <ErrorState onRetry={me.refetch} />
          ) : (
            <div className="space-y-4">
              <SegmentedControl<Mode>
                label={t('requests.modeLabel')}
                value={mode}
                options={[
                  { value: 'part', label: t('requests.modePart') },
                  { value: 'whole', label: t('requests.modeWhole') },
                  { value: 'multi', label: t('requests.modeMulti') },
                ]}
                onChange={changeMode}
              />

              <Field label={t('requests.kind')} required error={kindError}>
                <SelectRow
                  sheetTitle={t('requests.selectKind')}
                  value={absenceTypeId}
                  options={me.data?.absenceTypes ?? []}
                  placeholder={t('common.select')}
                  emptyText={t('requests.kindsEmpty')}
                  invalid={kindError !== null}
                  onChange={(id) => setAbsenceTypeId(id)}
                />
              </Field>

              <Field label={mode === 'multi' ? t('requests.fromDate') : t('requests.date')} required>
                <DateRow
                  sheetTitle={mode === 'multi' ? t('requests.fromDate') : t('requests.date')}
                  value={fromDate}
                  onChange={changeFromDate}
                />
              </Field>

              {mode === 'multi' ? (
                <Field
                  label={t('requests.toDate')}
                  required
                  error={dateError}
                  helper={dayCount > 1 ? t('requests.periodDays', { n: dayCount }) : undefined}
                >
                  <DateRow
                    sheetTitle={t('requests.toDate')}
                    value={toDate}
                    min={addDays(fromDate, 1)}
                    invalid={dateError !== null}
                    onChange={(iso) => setToDate(iso)}
                  />
                </Field>
              ) : null}

              {mode === 'part' ? (
                <>
                  <Field label={t('requests.fromTime')} required error={fromTimeError}>
                    <TimeRow
                      sheetTitle={t('requests.fromTime')}
                      value={fromTime}
                      placeholder={t('common.select')}
                      invalid={fromTimeError !== null}
                      onChange={setFromTime}
                    />
                  </Field>

                  <Field label={t('requests.toTime')} required error={toTimeError}>
                    <TimeRow
                      sheetTitle={t('requests.toTime')}
                      value={toTime}
                      placeholder={t('common.select')}
                      invalid={toTimeError !== null}
                      onChange={setToTime}
                    />
                  </Field>
                </>
              ) : null}

              <Field label={t('requests.note')}>
                <TextArea
                  value={note}
                  onChange={setNote}
                  placeholder={t('requests.notePlaceholder')}
                />
              </Field>
            </div>
          )}
        </Card>

        {sendError ? (
          <InlineMessage tone="danger" title={t('requests.sendFailed')} assertive>
            {errorText(sendError).body}
          </InlineMessage>
        ) : null}

        <p className="nums px-1 text-footnote text-fg-tertiary">{summary}</p>
      </PushedScreen>

      <BottomAction>
        <Button block busy={sending} onClick={() => void submit()}>
          {t('common.submit')}
        </Button>
      </BottomAction>

      <ConfirmSheet
        open={leaveOpen}
        title={t('requests.dirtyTitle')}
        confirmLabel={t('requests.dirtyLeave')}
        cancelLabel={t('common.stay')}
        danger
        onConfirm={() => {
          setLeaveOpen(false)
          navigate(-1)
        }}
        onClose={() => setLeaveOpen(false)}
      />
    </>
  )
}
