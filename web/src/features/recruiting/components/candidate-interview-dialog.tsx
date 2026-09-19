import { LoaderCircle, Send } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, DateField, Dialog, TextArea, TextField } from '@/shared/ui'
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
 *
 * <b>⭐ Izoh maydoni DOIM chiziladi</b> — ko'chirishda ham. Ilgari u
 * bosqichsiz holatda umuman ko'rinmasdi, chunki matn hech qayerga
 * tushmasdi. Endi tushadi va manzilni SERVER tanlaydi: bosqich bor —
 * bosqich xulosasi, bosqich yo'q — `candidate_notes` yozuvi
 * («suhbat ko'chirildi» turi, SPEC §4.2). Tanlovni klientga berish
 * server qarorini brauzerga ko'chirardi.
 *
 * <b>Keyingi aloqa sanasi ham shu yerda.</b> Suhbat vaqti bilan
 * ARALASHTIRILMASIN: suhbat vaqti nomzodga XABAR bo'lib ketadi,
 * keyingi aloqa sanasi esa faqat HR ning o'z ro'yxatida qator hosil
 * qiladi (SPEC §7 — ichki maydonlar nomzodga hech qachon
 * yuborilmaydi).
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
  const [nextContact, setNextContact] = useState('')

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

    /*
      Keyingi aloqa sanasi MAVJUD qiymatdan boshlanadi — ikkala
      tarmoqda ham (suhbat vaqti bor nomzodda ham, yo'qida ham).

      ⚠️ `candidate.nextContactDate` bog'liqliklar ro'yxatida turishi
      SHART: qaytadan ochilgan oynada eski bo'sh quti qolib qolsa, u
      serverga «tozalash» buyrug'ini yuborardi (pastdagi
      `clearNextContact`) va HR qo'ygan sana jimgina o'chib ketardi.
    */
    const seedContact = candidate.nextContactDate ?? ''

    if (current !== null) {
      const local = new Date(current)
      if (!Number.isNaN(local.getTime())) {
        const shifted = new Date(local.getTime() + 5 * 60 * 60 * 1000)
        setDate(shifted.toISOString().slice(0, 10))
        setTime(shifted.toISOString().slice(11, 16))
        setConclusion('')
        setNextContact(seedContact)
        return
      }
    }

    setDate(today())
    setTime(DEFAULT_TIME)
    setConclusion('')
    setNextContact(seedContact)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, candidate.interviewAt, candidate.nextContactDate])

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
        nextContactDate: orNull(nextContact),
        /*
          ⭐ Bo'sh quti O'ZICHA TOZALAMAYDI — shartnomada uch holat
          bor (`recruiting-api.ts`, «keyingi aloqa sanasi: yozish
          qoidasi»). «Tozala» buyrug'i faqat ilgari sana BO'LGAN
          bo'lsa yuboriladi: aks holda har bir suhbat chaqiruvi
          serverga keraksiz buyruq berardi.
        */
        clearNextContact: nextContact === '' && candidate.nextContactDate !== null,
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
          <DateField
            label={t('recruiting.interviewDate')}
            value={date}
            disabled={invite.isPending}
            onChange={setDate}
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
          Izoh XABARGA tushmaydi — matn har qanday holatda ICHKI
          qoladi. Maydon DOIM chiziladi: ilgari bosqich
          o'zgarmaydigan holatda (vaqtni ko'chirish) u umuman
          ko'rinmasdi, chunki matn hech qayerga tushmasdi. Endi
          tushadi — server `stageId` bo'lmaganda uni «suhbat
          ko'chirildi» turidagi izoh qilib yozadi (SPEC §4.2), ya'ni
          maydonning paydo bo'lib-yo'qolishi uchun sabab qolmadi.

          `maxLength` matn QAYSI jadvalga tushishiga qarab: bosqich
          xulosasi 4000, izoh esa 2000 belgi (ustun kengligi). Server
          uzunlikni tekshirmaydi, shu sababli chegarani maydonning
          o'zi ushlaydi — aks holda uzun matn bazada xato bo'lib
          qaytardi.
        */}
        <TextArea
          label={t('recruiting.conclusion')}
          hint={t('recruiting.interviewNoteHint')}
          placeholder={t('recruiting.conclusionPlaceholder')}
          maxLength={stage === null ? 2000 : 4000}
          value={conclusion}
          disabled={invite.isPending}
          onChange={(event) => setConclusion(event.target.value)}
        />

        {/*
          Keyingi aloqa sanasi — suhbat vaqtidan BOSHQA narsa: bu
          nomzod bilan keyin qachon gaplashish rejasi va u nomzodga
          ketmaydi. O'tgan sana ham to'g'ri qiymat va tekshirilmaydi
          (SPEC §2.1.3) — «bugun yoki muddati o'tganlar» ro'yxati
          aynan shundan tuziladi.
        */}
        <DateField
          label={t('recruiting.nextContact')}
          hint={t('recruiting.nextContactHint')}
          value={nextContact}
          disabled={invite.isPending}
          onChange={setNextContact}
        />
      </form>
    </Dialog>
  )
}
