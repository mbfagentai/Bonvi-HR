import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'
import { STAGE_OUTCOMES, type Candidate, type StageOutcome } from '../api/recruiting-api'
import { useRecordStage, useStages } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

const FORM_ID = 'candidate-stage-form'

/**
 * ⭐ Bosqich natijasini yozish — VORONKANING YAGONA HARAKATI.
 *
 * Butun voronka shu bitta oyna orqali harakatlanadi: «suhbat
 * belgilandi», «suhbatdan o'tdi», «kengash rad etdi» — hammasi shu.
 * Alohida «keyingi bosqichga o'tkazish» tugmasi YO'Q va bu ataylab
 * (backend qarori): oldinga yurish MAJBURLANMAYDI — bosqichni
 * o'tkazib yuborish ham, orqaga qaytish ham mumkin, chunki haqiqiy
 * ish jarayoni shunday ketadi.
 *
 * <b>«Kutilmoqda» — to'liq huquqli natija.</b> HR suhbatni oldindan
 * belgilaydi (nomzod o'sha bosqichga ko'chadi, natija esa bo'sh
 * qoladi), suhbat tugagach xuddi shu oynani qaytadan ochib natijani
 * yozadi. Server buni O'ZI biladi: oxirgi yozuv aynan shu bosqich
 * uchun va «kutilmoqda» bo'lsa, YANGI yozuv qo'shilmaydi — mavjudi
 * to'ldiriladi. Ya'ni tarixda ikkita yozuv paydo bo'lmaydi va
 * klientda «bu yangi yozuvmi yoki tahrirmi?» degan qaror ham yo'q.
 *
 * <b>«Ishga olindi» deb belgilash amali YO'Q.</b> Nomzod xodimga
 * faqat «Ishga olish» oynasi orqali aylanadi
 * (`CandidateHireDialog`) — boshqa yo'l bo'lganida sinov muddati,
 * buyruq raqami va JSHSHIR tekshiruvi chetlab o'tilardi.
 */
export function CandidateStageDialog({
  candidate,
  open,
  onClose,
  /** Oldindan tanlangan bosqich — lentadagi yozuvni to'ldirishda. */
  presetStageId = null,
}: {
  candidate: Candidate
  open: boolean
  onClose: () => void
  presetStageId?: string | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const record = useRecordStage()
  /*
   * Faqat FAOL bosqichlar: faolsizlantirilganiga nomzodni o'tkazib
   * bo'lmaydi va server buni 409 bilan rad etadi
   * («bosqich.faol_emas»). Ro'yxatda ko'rsatib, keyin xato berish
   * foydalanuvchini bekorga sarson qilardi.
   */
  const stages = useStages(true)

  const [stageId, setStageId] = useState('')
  const [outcome, setOutcome] = useState<StageOutcome>('Pending')
  const [date, setDate] = useState(today)
  const [conclusion, setConclusion] = useState('')

  useEffect(() => {
    if (!open) return
    record.reset()

    /*
      Standart bosqich: (1) chaqiruvchi ko'rsatgani, (2) nomzodning
      JORIY bosqichi. Ikkinchisi eng ko'p uchraydigan holat —
      «kutilmoqda» deb ochilgan yozuvni to'ldirish. Keyingi bosqichni
      taxmin qilib qo'yish RAD ETILDI: voronkada tartib qat'iy emas va
      noto'g'ri taxmin HR ni e'tiborsizlikka undardi.
    */
    setStageId(presetStageId ?? candidate.currentStageId ?? '')
    setOutcome('Pending')
    setDate(today())
    setConclusion('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, presetStageId, candidate.currentStageId])

  if (!open) return null

  const stageOptions = (stages.data ?? []).map((stage) => ({
    value: stage.id,
    label: stage.name,
  }))

  const outcomeOptions = STAGE_OUTCOMES.map((value) => ({
    value,
    label: t(`recruiting.outcome.${value.toLowerCase()}`),
  }))

  const submit = () => {
    if (stageId === '') return

    record.mutate(
      {
        id: candidate.id,
        stageId,
        outcome,
        date: orNull(date),
        conclusion: orNull(conclusion),
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!record.isPending}
      title={t('recruiting.stageRecordTitle')}
      description={candidate.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={record.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={record.isPending || stageId === ''}
          >
            {record.isPending && <LoaderCircle className="animate-spin" />}
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
        {record.isError && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(recruitingErrorCode(record.error))}
          </Alert>
        )}

        <Select
          label={t('recruiting.stage')}
          placeholder={t('recruiting.chooseStage')}
          options={stageOptions}
          value={stageId}
          disabled={record.isPending || stages.isPending}
          onChange={(event) => setStageId(event.target.value)}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <Select
            label={t('recruiting.outcome.label')}
            hint={t('recruiting.outcomeHint')}
            options={outcomeOptions}
            value={outcome}
            disabled={record.isPending}
            onChange={(event) => setOutcome(event.target.value as StageOutcome)}
          />

          <TextField
            label={t('recruiting.stageDate')}
            type="date"
            value={date}
            disabled={record.isPending}
            onChange={(event) => setDate(event.target.value)}
          />
        </div>

        {/*
          Xulosa — «Kengash» bosqichining butun mazmuni. Ovoz berish
          mexanizmi YO'Q (foydalanuvchi qarori): kengash yig'ilib,
          HR bitta xulosa yozadi. Shu sababli maydon erkin matn va
          hech qanday tuzilma talab qilmaydi.
        */}
        <TextArea
          label={t('recruiting.conclusion')}
          placeholder={t('recruiting.conclusionPlaceholder')}
          value={conclusion}
          disabled={record.isPending}
          onChange={(event) => setConclusion(event.target.value)}
        />
      </form>
    </Dialog>
  )
}
