import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, DateField, Dialog, TextArea, TextField } from '@/shared/ui'
import type { Vacancy } from '../api/recruiting-api'
import { useSaveVacancy } from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

const FORM_ID = 'vacancy-form'

/**
 * Nom uzunligi chegarasi — e'lon sarlavhasi uchun yetarlidan ko'p.
 *
 * Serverda bunday chegara YO'Q (ma'lumotnomalardagi kabi) — bu faqat
 * forma himoyasi: nom maydoniga tasodifan butun e'lon matni joylansa,
 * u ro'yxatda qatorni buzardi. Tafsilotlar IZOHGA tushadi.
 */
const TITLE_MAX_LENGTH = 200

/**
 * Vakansiyani ochish va tahrirlash — BITTA oyna
 * (`incident-form-dialog.tsx` dagi bilan bir xil naqsh: forma bir xil,
 * farq faqat sarlavhada va `item` borligida).
 *
 * <b>⭐ Forma ataylab QISQA: nomi, nechta odam, sanasi va izoh.</b>
 * Ilgari bu yerda lavozim, bo'lim va yuridik shaxs ham tanlanardi —
 * uchalasi OLIB TASHLANDI: vakansiya — e'lon, kompaniya tuzilmasidan
 * unda hech narsa saqlanmaydi (backend'dagi `Vacancy` domen izohi). Ular
 * nomzod ISHGA OLINAYOTGANDA so'raladi (`candidate-hire-dialog.tsx`).
 * «Mas'ul xodim» ham shu sababdan olib tashlandi — egasining qarori:
 * vakansiya eng sodda shaklda bo'lsin.
 *
 * <b>Nom — ERKIN MATN.</b> Ilgari u alohida «Vakansiya nomlari»
 * ma'lumotnomasidan tanlanardi, faolligi ham o'sha yerda boshqarilardi.
 * Natijada bitta ish ikki sahifaga bo'lingan edi: nomni bir joyda
 * yaratish, vakansiyani boshqa joyda ochish. Egasining qarori — hammasi
 * shu sahifada: nomni shu yerda yozasiz, faolligi esa vakansiyaning
 * ochiq/yopiq holati. Bir xil nom ikki marta paydo bo'lishini endi
 * ma'lumotnoma emas, SERVER to'sadi («vakansiya.nom_band» — xato nom
 * maydoni ostida chiqadi). Shoshilinchlik va boshqa tafsilotlar esa
 * avvalgidek IZOHGA tushadi, nomga emas.
 *
 * <b>Holat bu yerda o'zgarmaydi.</b> Yopish va qayta ochish — ro'yxatdagi
 * alohida tugma (`vacancies-page.tsx`), «Saqlash» ning yon ta'siri emas.
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

  const [title, setTitle] = useState('')
  const [openings, setOpenings] = useState('1')
  const [openedAt, setOpenedAt] = useState(today)
  const [note, setNote] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  /** Maydonga bog'langan server xatosi — masalan «bu nom band». */
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  // Oyna har ochilganda forma qayta to'ldiriladi: oldingi yozuvning
  // qiymatlari yangisiga «yopishib» qolmasligi kerak.
  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)
    save.reset()

    setTitle(item?.title ?? '')
    setOpenings(String(item?.openings ?? 1))
    setOpenedAt(item?.openedAt ?? today())
    setNote(item?.note ?? '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  if (!open) return null

  const incomplete = title.trim() === ''

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
          title: title.trim(),
          /*
            Bo'sh maydon 1 ga tushadi, `0` ga emas: `Number('')` — 0 va
            server uni «o'rin soni noto'g'ri» deb rad etardi
            (`catalog-schemas.ts` dagi `numericText` bilan bir xil
            mulohaza — bo'sh maydon kiritilgan qiymat emas).
          */
          openings: openings.trim() === '' ? 1 : Number(openings),
          openedAt: orNull(openedAt),
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
      description={item?.title}
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
          `hint` shu yerda MAJBURIY: HR ilgari bu maydonda lavozimni
          tanlagan va nom kompaniya lavozimi EMASligini darhol tushunishi
          kerak — aks holda u shtat jadvalidagi rasmiy nomni yozib
          qo'yardi.

          Avtofokus faqat YANGI vakansiyada: ochishning birinchi ishi —
          nom yozish. Tahrirlashda esa ko'pincha o'rin soni yoki izoh
          o'zgaradi va kursor nomga sakrasa, tasodifiy harf nomni buzardi.
        */}
        <TextField
          label={t('recruiting.vacancyTitle')}
          hint={t('recruiting.vacancyTitleHint')}
          placeholder={t('recruiting.vacancyTitlePlaceholder')}
          autoComplete="off"
          autoFocus={item === null}
          maxLength={TITLE_MAX_LENGTH}
          value={title}
          error={fieldMessage('title')}
          disabled={save.isPending}
          onChange={(event) => setTitle(event.target.value)}
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

          <DateField
            label={t('recruiting.openedAt')}
            value={openedAt}
            disabled={save.isPending}
            onChange={setOpenedAt}
          />
        </div>

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
