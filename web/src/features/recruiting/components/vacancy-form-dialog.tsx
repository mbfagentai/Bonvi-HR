import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'
import type { Vacancy } from '../api/recruiting-api'
import {
  useEmployeeOptions,
  useSaveVacancy,
  useVacancyTitleOptions,
} from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

const FORM_ID = 'vacancy-form'

/**
 * Vakansiyani ochish va tahrirlash — BITTA oyna
 * (`incident-form-dialog.tsx` dagi bilan bir xil naqsh: forma bir xil,
 * farq faqat sarlavhada va `item` borligida).
 *
 * <b>⭐ Forma ataylab QISQA: nomi, nechta odam va izoh.</b> Ilgari bu
 * yerda lavozim, bo'lim va yuridik shaxs ham tanlanardi. Uchalasi
 * OLIB TASHLANDI: vakansiya — e'lon, kompaniya tuzilmasidan unda hech
 * narsa saqlanmaydi (backend'dagi `Vacancy` domen izohi). Ular endi
 * taklif QABUL QILINGANDA so'raladi (`offer-accept-dialog.tsx`) — ya'ni
 * o'sha paytda, qachonki javob haqiqatan ma'lum bo'ladi.
 *
 * <b>Nom ERKIN MATN emas, ma'lumotnomadan.</b> Erkin sarlavha bo'lganida
 * «Сотувчи (шошилинч!)», «сотувчи консултант» kabi yozuvlar paydo
 * bo'lardi va bitta e'lon ro'yxatda uch xil nom bilan ko'rinardi.
 * Shoshilinchlik va boshqa tafsilotlar IZOHGA tushadi.
 *
 * <b>Holat bu yerda o'zgarmaydi.</b> To'xtatish, davom ettirish va
 * yopish — alohida amallar (`VacancyActionDialog`): ularning har biri
 * o'z sanasi va sababi bilan yoziladi, «Saqlash» tugmasining yon
 * ta'siri sifatida emas.
 */
export function VacancyFormDialog({
  open,
  onClose,
  item,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi vakansiya. */
  item: Vacancy | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveVacancy()
  const employees = useEmployeeOptions()
  // Faqat FAOL nomlar: faolsizlantirilgan e'lon nomiga yangi vakansiya
  // ochilmaydi (ma'lumotnomalarning umumiy qoidasi).
  const titles = useVacancyTitleOptions(true)

  const [vacancyTitleId, setVacancyTitleId] = useState('')
  const [openings, setOpenings] = useState('1')
  const [openedAt, setOpenedAt] = useState(today)
  const [responsibleEmployeeId, setResponsibleEmployeeId] = useState('')
  const [note, setNote] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  /** Maydonga bog'langan server xatosi — masalan «o'rin soni noto'g'ri». */
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  // Oyna har ochilganda forma qayta to'ldiriladi: oldingi yozuvning
  // qiymatlari yangisiga «yopishib» qolmasligi kerak.
  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)
    save.reset()

    setVacancyTitleId(item?.vacancyTitleId ?? '')
    setOpenings(String(item?.openings ?? 1))
    setOpenedAt(item?.openedAt ?? today())
    setResponsibleEmployeeId(item?.responsibleEmployeeId ?? '')
    setNote(item?.note ?? '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  if (!open) return null

  const incomplete = vacancyTitleId === ''

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
          vacancyTitleId,
          /*
            Bo'sh maydon 1 ga tushadi, `0` ga emas: `Number('')` — 0 va
            server uni «o'rin soni noto'g'ri» deb rad etardi
            (`catalog-schemas.ts` dagi `numericText` bilan bir xil
            mulohaza — bo'sh maydon kiritilgan qiymat emas).
          */
          openings: openings.trim() === '' ? 1 : Number(openings),
          openedAt: orNull(openedAt),
          responsibleEmployeeId: responsibleEmployeeId === '' ? null : responsibleEmployeeId,
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
      title={item === null ? t('recruiting.vacancyCreate') : t('recruiting.vacancyEdit')}
      description={item?.title ?? undefined}
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
            {t('common.save')}
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

        {/*
          Vakansiyaning YAGONA ma'lumotnoma havolasi.

          `hint` shu yerda MAJBURIY: HR ilgari bu maydonda lavozimni
          tanlagan va ro'yxat boshqacha ekanini darhol tushunishi kerak
          — aks holda u «lavozimim yo'q» deb o'ylab, ma'lumotnomani
          qidirib yurardi.
        */}
        <Select
          label={t('recruiting.vacancyTitle')}
          hint={t('recruiting.vacancyTitleHint')}
          placeholder={t('recruiting.chooseVacancyTitle')}
          options={titles.options}
          value={vacancyTitleId}
          error={fieldMessage('vacancyTitleId')}
          disabled={save.isPending || titles.isPending}
          onChange={(event) => setVacancyTitleId(event.target.value)}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('recruiting.openings')}
            hint={t('recruiting.openingsHint')}
            type="number"
            min={1}
            inputMode="numeric"
            value={openings}
            error={fieldMessage('openings')}
            disabled={save.isPending}
            onChange={(event) => setOpenings(event.target.value)}
          />

          <TextField
            label={t('recruiting.openedAt')}
            type="date"
            value={openedAt}
            disabled={save.isPending}
            onChange={(event) => setOpenedAt(event.target.value)}
          />
        </div>

        <Select
          label={t('recruiting.responsible')}
          placeholder={t('safety.noResponsible')}
          options={employees.options}
          value={responsibleEmployeeId}
          error={fieldMessage('responsibleEmployeeId')}
          disabled={save.isPending || employees.isPending}
          onChange={(event) => setResponsibleEmployeeId(event.target.value)}
        />

        <TextArea
          label={t('recruiting.note')}
          placeholder={t('recruiting.vacancyNotePlaceholder')}
          value={note}
          disabled={save.isPending}
          onChange={(event) => setNote(event.target.value)}
        />
      </form>
    </Dialog>
  )
}
