import { LoaderCircle, Send } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea, TextField } from '@/shared/ui'
import type { Candidate, PipelineStage } from '../api/recruiting-api'
import { useInviteToInterview } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

const FORM_ID = 'candidate-interview-form'

/** Sukut bo'yicha suhbat soati — ish kunining o'rtasi. */
const DEFAULT_TIME = '10:00'

/**
 * ⭐ Suhbatga chaqirish oynasi — nomzodga XABAR ketadigan yagona amal.
 *
 * <b>Nima uchun bor.</b> Foydalanuvchi talabi (2026-09-08): nomzod
 * kengashga chaqirilganda unga Telegram orqali sana, vaqt, manzil va
 * mas'ul raqami bilan taklif borishi kerak. Sana va vaqtni faqat HR
 * biladi — shu sababli bosqich tugmasi to'g'ridan-to'g'ri o'tkazmaydi,
 * avval shu oyna ochiladi.
 *
 * <b>Nega sana va vaqt ALOHIDA maydon.</b> Bitta `datetime-local`
 * maydoni brauzerlarda har xil ko'rinadi va telefonda ikkita alohida
 * tanlagichdan ko'ra qiyinroq to'ldiriladi. Ikkitaga bo'lish ustiga
 * sukut qiymatlarni ham ma'noli qiladi: sana — bugun, vaqt — 10:00.
 *
 * ⚠️ <b>Siljish QO'LDA qo'shiladi.</b> Maydonlardan «2026-09-10» va
 * «15:00» keladi, ular esa siljishsiz — serverga shundayligicha
 * yuborilsa UTC deb o'qilardi va nomzod besh soat oldingi vaqtga
 * chaqirilardi. Toshkent siljishi butun loyihada qat'iy (+05:00, yozgi
 * vaqt yo'q), shu sababli u shu yerda qo'shiladi.
 *
 * <b>Vaqtni KO'CHIRISH ham shu oyna.</b> Nomzod allaqachon chaqirilgan
 * bo'lsa, `stageId` yuborilmaydi: bosqich o'zgarmaydi, faqat vaqt
 * yangilanadi va nomzod yangi xabar oladi.
 */
export function CandidateInterviewDialog({
  candidate,
  stage,
  open,
  onClose,
}: {
  candidate: Candidate
  /**
   * Qaysi bosqichga o'tkaziladi. `null` — bosqich o'zgarmaydi (vaqtni
   * ko'chirish).
   */
  stage: PipelineStage | null
  open: boolean
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const invite = useInviteToInterview()

  const [date, setDate] = useState(today)
  const [time, setTime] = useState(DEFAULT_TIME)
  const [conclusion, setConclusion] = useState('')

  useEffect(() => {
    if (!open) return
    invite.reset()

    /*
      Mavjud vaqt bo'lsa — o'shandan boshlanadi: ko'chirishda HR odatda
      soatni bir-ikki soatga suradi, butun sanani qaytadan terish emas.
      Qiymat serverdan UTC da keladi, maydonlar esa MAHALLIY vaqtni
      kutadi — shu sababli u brauzer siljishi bilan emas, Toshkent
      siljishi bilan ochiladi.
    */
    const current = candidate.interviewAt

    if (current !== null) {
      const local = new Date(current)
      if (!Number.isNaN(local.getTime())) {
        const shifted = new Date(local.getTime() + 5 * 60 * 60 * 1000)
        setDate(shifted.toISOString().slice(0, 10))
        setTime(shifted.toISOString().slice(11, 16))
        setConclusion('')
        return
      }
    }

    setDate(today())
    setTime(DEFAULT_TIME)
    setConclusion('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, candidate.interviewAt])

  if (!open) return null

  const incomplete = date === '' || time === ''

  const submit = () => {
    if (incomplete) return

    invite.mutate(
      {
        id: candidate.id,
        interviewAt: `${date}T${time}:00+05:00`,
        stageId: stage?.id ?? null,
        conclusion: orNull(conclusion),
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!invite.isPending}
      title={t('recruiting.interviewTitle')}
      description={candidate.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={invite.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={invite.isPending || incomplete}
          >
            {invite.isPending ? <LoaderCircle className="animate-spin" /> : <Send />}
            {t('recruiting.interviewConfirm')}
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
        {invite.isError && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(recruitingErrorCode(invite.error))}
          </Alert>
        )}

        {/*
          Xabar ketishi HAQIDA ogohlantirish — tasdiqlash tugmasidan
          OLDIN. Bu oynadagi amal yagona qaytarib bo'lmaydigan qadam:
          bosqichni orqaga qaytarish mumkin, yuborilgan xabarni esa
          yo'q. HR buni bosishdan oldin bilishi kerak.
        */}
        <p className="text-subhead text-[var(--fg-secondary)]">
          {candidate.source === 'TelegramBot'
            ? t('recruiting.interviewText')
            : t('recruiting.interviewNoTelegram')}
        </p>

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('recruiting.interviewDate')}
            type="date"
            value={date}
            disabled={invite.isPending}
            onChange={(event) => setDate(event.target.value)}
          />

          <TextField
            label={t('recruiting.interviewTime')}
            type="time"
            value={time}
            disabled={invite.isPending}
            onChange={(event) => setTime(event.target.value)}
          />
        </div>

        {/*
          Izoh BOSQICH yozuviga tushadi, xabarga EMAS — matn ichki
          qoladi. Bosqich o'zgarmaydigan holatda (vaqtni ko'chirish)
          maydon umuman chizilmaydi: yozilgan matn hech qayerga
          tushmasdi.
        */}
        {stage !== null && (
          <TextArea
            label={t('recruiting.conclusion')}
            hint={t('recruiting.interviewNoteHint')}
            placeholder={t('recruiting.conclusionPlaceholder')}
            value={conclusion}
            disabled={invite.isPending}
            onChange={(event) => setConclusion(event.target.value)}
          />
        )}
      </form>
    </Dialog>
  )
}
