import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'
import type { Candidate } from '../api/recruiting-api'
import {
  useCloseCandidate,
  useReassignCandidate,
  useReturnCandidateToFunnel,
  useVacancyOptions,
} from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

/** Nomzod kartasidagi amallar — qaysi oyna ochilishini shu belgilaydi. */
export type CandidateAction =
  | 'reject'
  | 'withdraw'
  | 'reserve'
  | 'returnToFunnel'
  | 'reassign'

/** Sana va sabab so'raladigan amallar — ular voronkadan CHIQARADI. */
const CLOSING: readonly CandidateAction[] = ['reject', 'withdraw', 'reserve']

/**
 * BOSQICHI bo'lmagan amallar — izoh so'raladi, CHIQISH sanasi esa yo'q
 * (nomzod voronkadan chiqmaydi).
 *
 * Ikkalasi ham bosqich yozuvi hosil qilmaydi, shu sababli yozilgan
 * matn `candidate_notes` jadvaliga tushadi (`Reassign` va
 * `ReturnToFunnel` turlari). U nomzodning `note` maydoni EMAS: u
 * shaxsning uzoq muddatli ta'rifi va tahrirlash formasidan qayta
 * yozilib ketardi; `closingNote` ham emas — qaytarish uni o'sha
 * tranzaksiyada tozalaydi (SPEC §2.3.1).
 */
const WITH_NOTE: readonly CandidateAction[] = ['reassign', 'returnToFunnel']

/**
 * Keyingi aloqa sanasi so'raladigan amallar.
 *
 * ⚠️ «Rad etdik» va «voz kechdi» bu ro'yxatda YO'Q va bo'lishi ham
 * mumkin emas: server o'sha ikkitasida saqlangan sanani TOZALAYDI va
 * yuborilganini e'tiborsiz qoldiradi (SPEC §2.1.2). Maydonni
 * ko'rsatish HR yozgan sana jimgina yo'qoladigan holat bo'lardi.
 *
 * «Rezervga» esa chiqish emas, KECHIKTIRISH («o'rin bo'shasa
 * chaqiramiz») — sana aynan o'sha yerda ma'noli.
 */
const WITH_NEXT_CONTACT: readonly CandidateAction[] = [
  'reserve',
  'reassign',
  'returnToFunnel',
]

/**
 * Nomzodni voronkada QOLDIRADIGAN yoki unga QAYTARADIGAN amallar.
 *
 * Tasdiqlash tugmasining rangi shundan hisoblanadi: qizil faqat
 * voronkadan chiqarish va o'chirish uchun. «Rezervga» ham chiqaradi,
 * lekin u QAYTARILADI — qizil «xato bo'ldi» deb o'qilardi va aynan shu
 * sababdan uning yorlig'i ham kulrang (`CANDIDATE_LOOK`).
 */
const REVERSIBLE: readonly CandidateAction[] = ['reassign', 'reserve', 'returnToFunnel']

/**
 * Nomzod ustidagi OLTITA amal — BITTA oyna
 * (`VacancyActionDialog` bilan bir xil naqsh: tuzilishi bir xil, farq
 * matnda va yuboriladigan so'rovda).
 *
 * <b>«Rad etdik» va «o'zi voz kechdi» ATAYLAB ajratilgan.</b> Voronka
 * hisobotida ular butunlay boshqa ma'no beradi: birinchisi tanlov
 * sifati haqida, ikkinchisi taklif shartlari yoki jarayon uzunligi
 * haqida gapiradi (`CandidateStatus` domen izohi). Bitta «yopish»
 * amaliga yig'ilsa, ikkala muammo ham ko'rinmay qolardi — shu sababli
 * bu yerda ham ikkita alohida tugma va ikkita alohida matn.
 *
 * <b>«Rezervga» ham SHU YERDA</b> va u ham qisqa tasdiq oynasi: sana,
 * ixtiyoriy sabab va keyingi aloqa sanasi. Boshqa yakunlardan farqi
 * bittada — u QAYTARILADI, va qaytarish («Rezervdan qaytarish») ham shu
 * ro'yxatda turadi. Nomzod qaysi bosqichda rezervga o'tgan bo'lsa,
 * o'sha yerdan davom etadi va chiqish sababi tozalanadi.
 *
 * <b>⭐ Endi HAR BIR amal matn so'raydi</b> (foydalanuvchi talabi,
 * 2026-09-10): yopish amallarida u «Sababi», bosqichsiz ikkitasida
 * («o'tkazish», «rezervdan qaytarish») «Izoh». Ilgari o'sha ikkitasi
 * matn so'ramasdi va «gaplashdik, o'ylab ko'raman dedi» degan gap
 * hech qayerga yozilmasdi — ertasiga uni hech kim eslay olmasdi.
 *
 * Matn HECH QACHON majburiy emas: bo'sh bo'lsa yozuv umuman hosil
 * bo'lmaydi va amal baribir bajariladi. Majburlangan maydon «ok» kabi
 * yolg'on izohlar tug'diradi (`CandidateStageEntry.Conclusion` domen
 * izohi).
 *
 * <b>«Ishga olindi» amali bu ro'yxatda YO'Q.</b> Uning O'Z oynasi bor
 * (`candidate-hire-dialog.tsx`): u xodim yaratadi va o'nga yaqin
 * hujjatiy maydon so'raydi, ya'ni «sana + sabab» qolipiga sig'maydi.
 * Holatni qo'lda «ishga olingan» qilib qo'yish yo'li esa umuman yo'q —
 * o'shanda sinov muddati va buyruq ma'lumoti chetlab o'tilardi.
 *
 * <b>O'chirish — YUMSHOQ.</b> Yozuv bazada qoladi. Lekin rad etilgan
 * nomzod O'CHIRILMAYDI: u kelasi izlanishning bazasi (backend
 * hujjati shunday deydi) — o'chirish faqat XATO kiritilgan yozuv
 * uchun va matn ham shuni aytadi.
 */
export function CandidateActionDialog({
  candidate,
  action,
  onClose,
}: {
  candidate: Candidate
  /** `null` — oyna yopiq. */
  action: CandidateAction | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const close = useCloseCandidate()
  const reassign = useReassignCandidate()
  const restore = useReturnCandidateToFunnel()
  const pending =
    close.isPending || reassign.isPending || restore.isPending

  /*
   * Faqat OCHIQ vakansiyalar: yopiq yoki to'xtatilganiga nomzod
   * o'tkazilmaydi va server buni «vakansiya.qabul_qilmaydi» bilan rad
   * etadi. Ro'yxat faqat «o'tkazish» amalida so'raladi — hook shartsiz
   * chaqiriladi (React qoidasi), lekin oyna yopiq turganda so'rov
   * baribir keshdan keladi: uni nomzodlar ro'yxati allaqachon olgan.
   */
  const vacancies = useVacancyOptions('Open')

  const [at, setAt] = useState(today)
  const [reason, setReason] = useState('')
  const [vacancyId, setVacancyId] = useState('')
  const [note, setNote] = useState('')
  const [nextContact, setNextContact] = useState('')

  useEffect(() => {
    if (action === null) return
    setAt(today())
    setReason('')
    setVacancyId('')
    setNote('')
    /*
      Keyingi aloqa sanasi MAVJUD qiymatdan boshlanadi: rezervga
      o'tkazishda HR odatda uni bir-ikki haftaga suradi, noldan
      terish emas.

      ⚠️ Bo'sh qoldirilgan quti bu oynada TOZALAMAYDI — bu uchta amalda
      «tozalash» degan buyruq umuman yo'q (`clearNextContact` faqat
      bosqich va suhbat so'rovlarida bor, SPEC §4.5). Ya'ni bo'sh quti
      «sanaga tegilmadi» degani va HR qo'ygan sana o'z joyida qoladi.
    */
    setNextContact(candidate.nextContactDate ?? '')
    close.reset()
    reassign.reset()
    restore.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [action, candidate.id, candidate.nextContactDate])

  if (action === null) return null

  const failure = close.error ?? reassign.error ?? restore.error
  /*
    ⭐ Qaytarish sababi — RAD ETILGAN va VOZ KECHGAN nomzodda majburiy
    (server ham talab qiladi: «nomzod.qaytarish_sababi_yoq»).
    Rezervdan qaytarishda esa ixtiyoriy: rezervning butun ma'nosi
    «o'rin bo'shasa chaqiramiz», ya'ni qaytish o'sha holatning tabiiy
    yakuni. Tugma o'chirilgani xatoni OLDINDAN aytadi — HR matnni
    yozib, so'ng 400 olishi kerak emas.
  */
  const reasonRequired =
    action === 'returnToFunnel' && candidate.status !== 'Reserve'

  const incomplete =
    (action === 'reassign' && vacancyId === '') || (reasonRequired && note.trim() === '')

  const submit = () => {
    if (incomplete) return

    if (action === 'reassign') {
      reassign.mutate(
        {
          id: candidate.id,
          vacancyId,
          note: orNull(note),
          nextContactDate: orNull(nextContact),
        },
        { onSuccess: onClose },
      )
      return
    }

    if (action === 'returnToFunnel') {
      restore.mutate(
        { id: candidate.id, note: orNull(note), nextContactDate: orNull(nextContact) },
        { onSuccess: onClose },
      )
      return
    }

    close.mutate(
      {
        id: candidate.id,
        action,
        at: orNull(at),
        reason: orNull(reason),
        /*
          Sana FAQAT rezervga o'tkazishda yuboriladi. Rad etish va voz
          kechishda maydon chizilmaydi, ya'ni qiymat baribir bo'sh
          bo'lardi — lekin shart ATAYLAB yozilgan: server u ikkitasida
          sanani tozalaydi va kelgan qiymatni e'tiborsiz qoldiradi
          (`closeCandidate` izohi), shu sababli niyat kodda ko'rinib
          turishi kerak.
        */
        nextContactDate: action === 'reserve' ? orNull(nextContact) : null,
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!pending}
      title={t(`recruiting.candidateAction.${action}Title`)}
      description={candidate.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            /*
              Qizil FAQAT qaytarib bo'lmaydigan amallarda: rad etish,
              voz kechish va o'chirish. «O'tkazish», «Rezervga» va
              «Rezervdan qaytarish» esa qaytariladi — ular tugmani
              qizartirmaydi (sabab `REVERSIBLE` izohida).
            */
            variant={REVERSIBLE.includes(action) ? 'primary' : 'danger'}
            onClick={submit}
            disabled={pending || incomplete}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {t(`recruiting.candidateAction.${action}Confirm`)}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && failure !== undefined && (
          <Alert tone="danger">{errorMessage(recruitingErrorCode(failure))}</Alert>
        )}

        <p className="text-subhead text-[var(--fg-secondary)]">
          {t(`recruiting.candidateAction.${action}Text`)}
        </p>

        {action === 'reassign' && (
          <Select
            label={t('recruiting.vacancy')}
            placeholder={t('recruiting.chooseVacancy')}
            hint={t('recruiting.vacancyOpenHint')}
            options={vacancies.options}
            value={vacancyId}
            disabled={pending || vacancies.isPending}
            onChange={(event) => setVacancyId(event.target.value)}
          />
        )}

        {/*
          Sana va sabab faqat voronkadan CHIQARISHDA: bosqichsiz
          amallarda («o'tkazish», «rezervdan qaytarish») chiqish sanasi
          degan narsaning o'zi yo'q — nomzod voronkada qoladi.
        */}
        {CLOSING.includes(action) && (
          <>
            <TextField
              label={t('recruiting.closedAt')}
              type="date"
              value={at}
              disabled={pending}
              onChange={(event) => setAt(event.target.value)}
            />

            {/*
              Sabab ICHKI: u nomzodga ketadigan xabarga tushmaydi
              (backend `BotTexts.RejectionNotice`). Bu yozuvsiz HR
              izohni «nomzod o'qiydi» deb yozardi va izohning ichki
              qiymati yo'qolardi — shu sababli eslatma maydonning
              O'ZIDA turadi.

              `maxLength` — ustun kengligi (2000). Server uzunlikni
              tekshirmaydi, chegarani maydonning o'zi ushlab turadi.
            */}
            <TextArea
              label={t('recruiting.reason')}
              placeholder={t('recruiting.candidateReasonPlaceholder')}
              hint={t('recruiting.candidateReasonHint')}
              maxLength={2000}
              value={reason}
              disabled={pending}
              onChange={(event) => setReason(event.target.value)}
            />
          </>
        )}

        {/*
          Izoh — bosqichsiz amallar uchun. Yorliq «Sababi» EMAS,
          «Izoh»: nomzodni boshqa vakansiyaga o'tkazish yoki rezervdan
          qaytarish yakun emas va uning «sababi» ham yo'q — bu shunchaki
          nima gaplashilganining qaydi. Eslatma esa AYNI o'sha:
          matn ichki qoladi va nomzodga bormaydi.

          O'rin egari (`placeholder`) ATAYLAB yo'q: mavjud matnlarning
          ikkisi ham yolg'on bo'lardi — «Nima uchun voronkadan chiqdi»
          bu amalda chiqish yo'q, «Ichki eslatma: kim tavsiya qildi»
          esa nomzodning uzoq muddatli ta'rifi (boshqa maydon). Uchinchi
          kalit o'ylab topish esa tarjima faylini bekorga
          kengaytirardi.
        */}
        {WITH_NOTE.includes(action) && (
          <TextArea
            /*
              Qaytarishda maydon boshqacha NOMLANADI: u «izoh» emas,
              QARORNING sababi («nega fikr o'zgardi») va lentada
              abadiy qoladi. Ko'chirishda esa oddiy izoh — shu sababli
              yorliq ham, tavsif ham amalga qarab tanlanadi.
            */
            label={
              action === 'returnToFunnel'
                ? t('recruiting.candidateAction.returnReason')
                : t('recruiting.note')
            }
            hint={
              action === 'returnToFunnel'
                ? t('recruiting.candidateAction.returnReasonHint')
                : t('recruiting.candidateReasonHint')
            }
            maxLength={2000}
            value={note}
            disabled={pending}
            onChange={(event) => setNote(event.target.value)}
          />
        )}

        {/*
          Keyingi aloqa sanasi — «hozir o'rin yo'q, ikki haftadan keyin
          qo'ng'iroq qilaman» degan holat uchun. Kalendar sanasi,
          vaqtsiz va nomzodga BORMAYDI.

          O'tgan sana ham to'g'ri qiymat va tekshirilmaydi (SPEC
          §2.1.3): «bugun yoki muddati o'tganlar» ro'yxati aynan shu
          maydondan tuziladi.
        */}
        {WITH_NEXT_CONTACT.includes(action) && (
          <TextField
            label={t('recruiting.nextContact')}
            hint={t('recruiting.nextContactHint')}
            type="date"
            value={nextContact}
            disabled={pending}
            onChange={(event) => setNextContact(event.target.value)}
          />
        )}
      </div>
    </Dialog>
  )
}
