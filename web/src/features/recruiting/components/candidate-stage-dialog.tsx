import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'
import {
  STAGE_OUTCOMES,
  type Candidate,
  type PipelineStage,
  type StageOutcome,
} from '../api/recruiting-api'
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
 *
 * <b>⭐ Kartadagi TEZKOR QATOR ham shu oynani ochadi</b> —
 * `lockedStage` bilan: bosqich qotib qoladi, ro'yxat umuman
 * chizilmaydi. Ilgari o'sha qator bir bosishda ko'chirar edi va
 * natijani «keyin yozasiz» deb qoldirardi; amalda hech qachon
 * yozilmadi (foydalanuvchi talabi, 2026-09-10). Alohida «tezkor oyna»
 * yozish RAD ETILDI: ikkalasi ham AYNAN shu besh maydonni so'rardi va
 * vaqt o'tib bir-biridan uzoqlashardi — `CandidateActionDialog`
 * oltita amalni bitta oynada boshqarishi bilan bir xil qaror
 * (SPEC §5.1.1).
 *
 * <b>Xulosa MAJBURIY EMAS.</b> Bo'sh xulosa bilan ham SAQLANADI:
 * tekshiruv ham, yulduzcha ham, o'chirilgan tugma ham yo'q. Sabab
 * domen izohida yozilgan (`CandidateStageEntry.Conclusion`):
 * majburlangan maydon «ok», «yaxshi» kabi yolg'on xulosalar
 * tug'diradi va maydonning butun qiymatini yo'q qiladi.
 *
 * <b>Yozilgan matn ICHKI</b> — na xulosa, na keyingi aloqa sanasi
 * nomzodga ketmaydi (SPEC §7). Shu sababli oynada «nomzodga
 * yuborilsinmi?» degan tanlov ham yo'q.
 */
export function CandidateStageDialog({
  candidate,
  open,
  onClose,
  /** Oldindan tanlangan bosqich — lentadagi yozuvni to'ldirishda. */
  presetStageId = null,
  /**
   * Bosqich QOTIB qo'yilgan — tezkor qatordagi tugma shu ko'rinishda
   * ochadi. Ro'yxat chizilmaydi va `stageId` o'zgarmaydi: HR aynan
   * bosgan bosqichni yozadi, tasodifan boshqasini tanlab qo'ymaydi.
   */
  lockedStage = null,
}: {
  candidate: Candidate
  open: boolean
  onClose: () => void
  presetStageId?: string | null
  lockedStage?: PipelineStage | null
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
  const [nextContact, setNextContact] = useState('')

  useEffect(() => {
    if (!open) return
    record.reset()

    /*
      Standart bosqich: (1) QOTIB qo'yilgani, (2) chaqiruvchi
      ko'rsatgani, (3) nomzodning JORIY bosqichi. Uchinchisi eng ko'p
      uchraydigan holat — «kutilmoqda» deb ochilgan yozuvni
      to'ldirish. Keyingi bosqichni taxmin qilib qo'yish RAD ETILDI:
      voronkada tartib qat'iy emas va noto'g'ri taxmin HR ni
      e'tiborsizlikka undardi.
    */
    setStageId(lockedStage?.id ?? presetStageId ?? candidate.currentStageId ?? '')
    setOutcome('Pending')
    setDate(today())
    setConclusion('')
    /*
      Keyingi aloqa sanasi MAVJUD qiymatdan boshlanadi: HR odatda uni
      suradi («yana uch kundan keyin»), noldan terish emas.

      ⚠️ `candidate.nextContactDate` bog'liqliklar ro'yxatida
      TURISHI SHART. Aks holda qaytadan ochilgan oynada eski qiymat
      qolardi va bo'sh qutili holat eng xatarli: u serverga
      «tozalash» buyrug'ini yuborardi (pastdagi `clearNextContact`).
    */
    setNextContact(candidate.nextContactDate ?? '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, presetStageId, lockedStage, candidate.currentStageId, candidate.nextContactDate])

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
        nextContactDate: orNull(nextContact),
        /*
          ⭐ Bo'sh quti O'ZICHA TOZALAMAYDI — shartnomada uch holat bor
          (`recruiting-api.ts`, «keyingi aloqa sanasi: yozish qoidasi»).
          «Tozalash» buyrug'i FAQAT ilgari sana bo'lgan bo'lsa
          yuboriladi: sanasi yo'q nomzodda ochilgan oyna serverga
          keraksiz «tozala» demasligi kerak, aks holda har bir bosqich
          yozuvi jimgina yozuv qilardi.

          Teskarisi ham muhim: HR sanani ATAYLAB o'chirsa
          («qo'ng'iroq kerak emas»), quti bo'sh qolgani YETMAYDI —
          `nextContactDate: null` server uchun «tegma» degani.
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
      dismissible={!record.isPending}
      title={
        lockedStage === null
          ? t('recruiting.stageRecordTitle')
          : t('recruiting.stageMoveTo', { stage: lockedStage.name })
      }
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

        {/*
          Qotib qo'yilgan bosqich — TANLAGICH emas, yozuv. `disabled`
          qilingan `Select` xiralashardi va «ishlamayapti» degan ma'no
          berardi (tezkor qatordagi joriy bosqich yorlig'i bilan aynan
          bir xil mulohaza).
        */}
        {lockedStage === null ? (
          <Select
            label={t('recruiting.stage')}
            placeholder={t('recruiting.chooseStage')}
            options={stageOptions}
            value={stageId}
            disabled={record.isPending || stages.isPending}
            onChange={(event) => setStageId(event.target.value)}
          />
        ) : (
          <div className="space-y-2">
            <p className="text-footnote font-medium text-[var(--fg-secondary)]">
              {t('recruiting.stage')}
            </p>
            <p className="text-body font-medium">{lockedStage.name}</p>
          </div>
        )}

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

          `maxLength` — ustun kengligi (4000). Server matn uzunligini
          TEKSHIRMAYDI (`CandidateStageEntry` izohi: chegara faqat
          ustunda), ya'ni cheklovni shu maydon ushlab turadi — aks
          holda uzun matn bazada xato bo'lib qaytardi.
        */}
        <TextArea
          label={t('recruiting.conclusion')}
          placeholder={t('recruiting.conclusionPlaceholder')}
          maxLength={4000}
          value={conclusion}
          disabled={record.isPending}
          onChange={(event) => setConclusion(event.target.value)}
        />

        {/*
          ⭐ Keyingi aloqa sanasi — «gaplashdik, besh-olti kundan keyin
          keladi deydi» degan holat uchun. Kalendar sanasi, VAQTSIZ: u
          nomzodga xabar bo'lib ketmaydi, faqat HR ning «bugun kim
          bilan gaplashish kerak» ro'yxatida qator hosil qiladi.

          O'TGAN sana ham to'g'ri qiymat va tekshirilmaydi (SPEC
          §2.1.3): maydonning butun maqsadi «muddati o'tganlar»
          ro'yxatini berish — suhbat vaqtidagi «kechagi sana — xato»
          qoidasini bu yerga ko'chirib bo'lmaydi.
        */}
        <TextField
          label={t('recruiting.nextContact')}
          hint={t('recruiting.nextContactHint')}
          type="date"
          value={nextContact}
          disabled={record.isPending}
          onChange={(event) => setNextContact(event.target.value)}
        />
      </form>
    </Dialog>
  )
}
