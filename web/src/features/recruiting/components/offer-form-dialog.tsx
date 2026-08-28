import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea, TextField } from '@/shared/ui'
import type { Candidate, JobOffer } from '../api/recruiting-api'
import { useSaveOffer } from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { orNull } from '../lib/recruiting-presentation'

const FORM_ID = 'offer-form'

/**
 * Ish taklifini yuborish va tuzatish — BITTA oyna.
 *
 * <b>Oklad MAJBURIY.</b> Summasiz «taklif» — bu og'zaki suhbat va u
 * bosqich xulosasiga yoziladi. Rasmiy taklif esa doim aniq summa
 * bilan beriladi va AYNAN shu summa ishga qabul buyrug'iga ko'chadi
 * (`JobOffer` domen izohi) — taklifning butun ma'nosi shu ko'chishda.
 *
 * <b>Valyuta maydoni YO'Q.</b> Server bo'sh qoldirilganda tizim
 * valyutasini (UZS) qo'yadi. Tanlov qo'shish ikki narsani talab
 * qilardi: valyutalar ro'yxati va kurs — ikkalasi ham tizimda yo'q va
 * bu topshiriqda so'ralmagan. Bir valyutali maydonni «tanlov» qilib
 * ko'rsatish esa yolg'on erkinlik beradi.
 *
 * <b>Javob muddati IXTIYORIY va bu ataylab.</b> Muddatni majburiy
 * qilish HR ni har safar o'ylab sana qo'yishga majbur qilardi;
 * amalda esa taklif ko'pincha «javobini kutamiz» holatida turadi va
 * kerak bo'lganda qo'lda «muddati o'tdi» deb yopiladi.
 */
export function OfferFormDialog({
  candidate,
  item,
  open,
  onClose,
}: {
  candidate: Candidate
  /** `null` — yangi taklif. */
  item: JobOffer | null
  open: boolean
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveOffer(candidate.id)

  const [salaryAmount, setSalaryAmount] = useState('')
  const [startDate, setStartDate] = useState('')
  const [expiresAt, setExpiresAt] = useState('')
  const [note, setNote] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)
    save.reset()

    setSalaryAmount(item === null ? '' : String(item.salary.amount))
    setStartDate(item?.startDate ?? '')
    setExpiresAt(item?.expiresAt ?? '')
    setNote(item?.note ?? '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  if (!open) return null

  const amount = Number(salaryAmount.replace(',', '.'))
  const incomplete = salaryAmount.trim() === '' || !Number.isFinite(amount) || amount <= 0

  const fieldMessage = (field: string): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    save.mutate(
      {
        id: item?.id ?? null,
        body: {
          salaryAmount: amount,
          // Valyuta yuborilmaydi — server tizim valyutasini qo'yadi.
          salaryCurrency: null,
          startDate: orNull(startDate),
          expiresAt: orNull(expiresAt),
          note: orNull(note),
        },
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          const code = recruitingErrorCode(error)
          const field = RECRUITING_ERROR_FIELD[code]
          if (field === undefined) setFailure(code)
          else setFieldError({ field, code })
        },
      },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!save.isPending}
      title={item === null ? t('recruiting.offerCreate') : t('recruiting.offerEdit')}
      description={candidate.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={save.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={save.isPending || incomplete}
          >
            {save.isPending && <LoaderCircle className="animate-spin" />}
            {item === null ? t('recruiting.offerSend') : t('common.save')}
          </Button>
        </>
      }
    >
      <form
        id={FORM_ID}
        noValidate
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        <TextField
          label={t('recruiting.offerSalary')}
          hint={t('recruiting.offerSalaryHint')}
          type="number"
          min={0}
          step="1000"
          inputMode="decimal"
          value={salaryAmount}
          error={fieldMessage('salaryAmount')}
          disabled={save.isPending}
          onChange={(event) => setSalaryAmount(event.target.value)}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('recruiting.offerStartDate')}
            hint={t('recruiting.offerStartDateHint')}
            type="date"
            value={startDate}
            error={fieldMessage('startDate')}
            disabled={save.isPending}
            onChange={(event) => setStartDate(event.target.value)}
          />

          <TextField
            label={t('recruiting.offerExpiresAt')}
            type="date"
            value={expiresAt}
            error={fieldMessage('expiresAt')}
            disabled={save.isPending}
            onChange={(event) => setExpiresAt(event.target.value)}
          />
        </div>

        <TextArea
          label={t('recruiting.offerNote')}
          placeholder={t('recruiting.offerNotePlaceholder')}
          value={note}
          disabled={save.isPending}
          onChange={(event) => setNote(event.target.value)}
        />
      </form>
    </Dialog>
  )
}
