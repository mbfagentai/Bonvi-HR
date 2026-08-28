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
  useVacancyOptions,
} from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

/** Nomzod kartasidagi amallar — qaysi oyna ochilishini shu belgilaydi. */
export type CandidateAction = 'reject' | 'withdraw' | 'reassign' | 'delete'

/**
 * Nomzod ustidagi to'rtta amal — BITTA oyna
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
 * <b>«Ishga olindi» amali bu ro'yxatda YO'Q.</b> Nomzod xodimga faqat
 * taklif qabul qilinganda aylanadi — qo'lda belgilash yo'li bo'lganida
 * sinov muddati va buyruq ma'lumoti chetlab o'tilardi.
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
  const remove = useDeleteCandidate()
  const pending = close.isPending || reassign.isPending || remove.isPending

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
    remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [action, candidate.id])

  if (action === null) return null

  const failure = close.error ?? reassign.error ?? remove.error
  const incomplete = action === 'reassign' && vacancyId === ''

  const submit = () => {
    if (incomplete) return

    if (action === 'reassign') {
      reassign.mutate({ id: candidate.id, vacancyId }, { onSuccess: onClose })
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
              «O'tkazish» — tiklovchi emas, lekin buzuvchi ham emas:
              nomzod voronkada qoladi, faqat boshqa o'ringa. Qolgan
              uchtasi uni voronkadan chiqaradi — qizil.
            */
            variant={action === 'reassign' ? 'primary' : 'danger'}
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
            hint={t('recruiting.vacancyOpenOnlyHint')}
            options={vacancies.options}
            value={vacancyId}
            disabled={pending || vacancies.isPending}
            onChange={(event) => setVacancyId(event.target.value)}
          />
        )}

        {/*
          Sana va sabab faqat voronkadan chiqarishda: `reassign` va
          `delete` so'rovlarida bunday maydon UMUMAN yo'q — ko'rsatib
          qo'yilsa, HR yozgan matn jimgina yo'qolardi.
        */}
        {(action === 'reject' || action === 'withdraw') && (
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
