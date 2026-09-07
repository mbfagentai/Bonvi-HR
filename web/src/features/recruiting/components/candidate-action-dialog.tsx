import { useNavigate } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'
import type { Candidate } from '../api/recruiting-api'
import {
  useCloseCandidate,
  useDeleteCandidate,
  useReassignCandidate,
  useReturnCandidateFromReserve,
  useVacancyOptions,
} from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

/** Nomzod kartasidagi amallar — qaysi oyna ochilishini shu belgilaydi. */
export type CandidateAction =
  | 'reject'
  | 'withdraw'
  | 'reserve'
  | 'returnFromReserve'
  | 'reassign'
  | 'delete'

/** Sana va sabab so'raladigan amallar — ular voronkadan CHIQARADI. */
const CLOSING: readonly CandidateAction[] = ['reject', 'withdraw', 'reserve']

/**
 * Nomzodni voronkada QOLDIRADIGAN yoki unga QAYTARADIGAN amallar.
 *
 * Tasdiqlash tugmasining rangi shundan hisoblanadi: qizil faqat
 * voronkadan chiqarish va o'chirish uchun. «Rezervga» ham chiqaradi,
 * lekin u QAYTARILADI — qizil «xato bo'ldi» deb o'qilardi va aynan shu
 * sababdan uning yorlig'i ham kulrang (`CANDIDATE_LOOK`).
 */
const REVERSIBLE: readonly CandidateAction[] = ['reassign', 'reserve', 'returnFromReserve']

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
 * <b>«Rezervga» ham SHU YERDA</b> va u ham qisqa tasdiq oynasi: sana va
 * ixtiyoriy sabab. Boshqa yakunlardan farqi bittada — u QAYTARILADI, va
 * qaytarish («Rezervdan qaytarish») ham shu ro'yxatda turadi. U yagona
 * amal bo'lib, maydonsiz: nomzod qaysi bosqichda rezervga o'tgan bo'lsa,
 * o'sha yerdan davom etadi va chiqish sababi tozalanadi — kiritadigan
 * hech narsa yo'q.
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
  const navigate = useNavigate()

  const close = useCloseCandidate()
  const reassign = useReassignCandidate()
  const restore = useReturnCandidateFromReserve()
  const remove = useDeleteCandidate()
  const pending =
    close.isPending || reassign.isPending || restore.isPending || remove.isPending

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

  useEffect(() => {
    if (action === null) return
    setAt(today())
    setReason('')
    setVacancyId('')
    close.reset()
    reassign.reset()
    restore.reset()
    remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [action, candidate.id])

  if (action === null) return null

  const failure = close.error ?? reassign.error ?? restore.error ?? remove.error
  const incomplete = action === 'reassign' && vacancyId === ''

  const submit = () => {
    if (incomplete) return

    if (action === 'reassign') {
      reassign.mutate({ id: candidate.id, vacancyId }, { onSuccess: onClose })
      return
    }

    if (action === 'returnFromReserve') {
      restore.mutate(candidate.id, { onSuccess: onClose })
      return
    }

    if (action === 'delete') {
      remove.mutate(candidate.id, {
        // Yozuv ro'yxatdan g'oyib bo'ladi — kartada qolish ma'nosiz.
        onSuccess: () => {
          onClose()
          void navigate({ to: '/nomzodlar' })
        },
      })
      return
    }

    close.mutate(
      { id: candidate.id, action, at: orNull(at), reason: orNull(reason) },
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
          Sana va sabab faqat voronkadan chiqarishda: `reassign`,
          `returnFromReserve` va `delete` so'rovlarida bunday maydon
          UMUMAN yo'q — ko'rsatib qo'yilsa, HR yozgan matn jimgina
          yo'qolardi.
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

            <TextArea
              label={t('recruiting.reason')}
              placeholder={t('recruiting.candidateReasonPlaceholder')}
              value={reason}
              disabled={pending}
              onChange={(event) => setReason(event.target.value)}
            />
          </>
        )}
      </div>
    </Dialog>
  )
}
