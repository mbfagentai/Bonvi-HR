import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import { invalidateRequests, useMe } from '@/data/queries'
import { useFormat } from '@/lib/format'
import { daysBetween, parseIsoDate, todayIso } from '@/lib/time'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { ErrorState, InlineMessage, SkeletonLines } from '@/ui/feedback'
import { DateRow, Field, SelectRow, TextArea } from '@/ui/form'
import { ConfirmSheet } from '@/ui/sheet'
import { useToast } from '@/ui/toast'

/**
 * SO'ROV YUBORISH formasi (UX §A.3, §E.5).
 *
 * Verifix'da «so'rov turini tanlang» degan varaqda sakkizta band bor
 * edi — ularning har biri alohida MODUL. Bizda so'rov bitta: yo'qlik.
 * Uning TURI esa shu formadagi maydon, chunki u ma'lumotnomadan
 * keladi (UX §A.1).
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

  const [absenceTypeId, setAbsenceTypeId] = useState<string | null>(null)
  const [fromDate, setFromDate] = useState(prefill)
  const [toDate, setToDate] = useState(prefill)
  const [note, setNote] = useState('')

  const [touched, setTouched] = useState(false)
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<ApiError | null>(null)
  const [leaveOpen, setLeaveOpen] = useState(false)

  // «Iflos» forma — yozilgan narsa bor. Tasdiq faqat shu holatda
  // so'raladi: bo'sh formadan chiqishda savol berish — bezovta qilish.
  const dirty = absenceTypeId !== null || note.length > 0 || fromDate !== prefill || toDate !== prefill

  const kindError = touched && !absenceTypeId ? t('requests.kindRequired') : null
  const dateError = touched && toDate < fromDate ? t('requests.dateOrder') : null
  const dayCount = daysBetween(fromDate, toDate)

  const submit = async (): Promise<void> => {
    setTouched(true)
    if (!absenceTypeId || toDate < fromDate) return

    setSending(true)
    setSendError(null)

    try {
      await api.createRequest({
        absenceTypeId,
        fromDate,
        toDate,
        fromTime: null,
        toTime: null,
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

  return (
    <>
      <PushedScreen title={t('requests.newTitle')} onBack={goBack} bottomSpace>
        <Card>
          {me.isLoading ? (
            <SkeletonLines rows={3} />
          ) : me.error && !me.data ? (
            <ErrorState onRetry={me.refetch} />
          ) : (
            <div className="space-y-4">
              <Field label={t('requests.kind')} required error={kindError}>
                <SelectRow
                  sheetTitle={t('requests.selectKind')}
                  value={absenceTypeId}
                  options={me.data?.absenceTypes ?? []}
                  placeholder={t('common.select')}
                  invalid={kindError !== null}
                  onChange={(id) => setAbsenceTypeId(id)}
                />
              </Field>

              <Field label={t('requests.fromDate')} required>
                <DateRow
                  sheetTitle={t('requests.fromDate')}
                  value={fromDate}
                  onChange={(iso) => {
                    setFromDate(iso)
                    // Oxiri boshidan oldin qolib ketmasin — eng ko'p
                    // uchraydigan xatoni forma o'zi tuzatadi.
                    if (toDate < iso) setToDate(iso)
                  }}
                />
              </Field>

              <Field
                label={t('requests.toDate')}
                required
                error={dateError}
                helper={dayCount > 1 ? t('requests.periodDays', { n: dayCount }) : undefined}
              >
                <DateRow
                  sheetTitle={t('requests.toDate')}
                  value={toDate}
                  min={fromDate}
                  invalid={dateError !== null}
                  onChange={(iso) => setToDate(iso)}
                />
              </Field>

              <Field label={t('requests.note')}>
                <TextArea
                  value={note}
                  onChange={setNote}
                  placeholder={t('requests.notePlaceholder')}
                />
              </Field>

              <p className="text-footnote text-fg-tertiary">{t('requests.timeHint')}</p>
            </div>
          )}
        </Card>

        {sendError ? (
          <InlineMessage tone="danger" title={t('requests.sendFailed')} assertive>
            {errorText(sendError).body}
          </InlineMessage>
        ) : null}

        <p className="nums px-1 text-footnote text-fg-tertiary">
          {fromDate === toDate
            ? format.dateLong(fromDate)
            : `${format.date(fromDate)} — ${format.date(toDate)}`}
        </p>
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
