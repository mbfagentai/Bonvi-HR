import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, DateField, Dialog, Select, TextArea } from '@/shared/ui'
import {
  type Candidate,
  type CandidateStageEntry,
  type PipelineStage,
} from '../api/recruiting-api'
import { useEditStageEntry, useRecordStage, useStages } from '../api/recruiting-queries'
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
 * <b>⭐ IKKI REJIM: yangi yozuv va TUZATISH.</b> `entry` berilsa oyna
 * aynan o'sha yozuvni tahrirlaydi (`PUT .../stages/{entryId}`),
 * berilmasa tarixga YANGI qator qo'shadi (`POST .../stages`). Qaysi
 * yozuv tuzatilayotganini MIJOZ aytadi, server taxmin qilmaydi.
 *
 * ⚠️ Ilgari bu qaror serverda edi va nosozlik manbai bo'lgan: ayni
 * shu bosqichdagi oxirgi ochiq yozuv JIMGINA to'ldirilardi. Natijada (1) bo'sh xulosa bilan saqlash oldingi
 * izohni o'chirardi, (2) lentadagi ESKIROQ qatorga natija yozilsa,
 * o'sha qator bo'sh qolib, pastga duplikat tushardi (2026-09-16).
 *
 * <b>Bosqichni oldindan ochish mumkin.</b> HR suhbatni belgilaydi
 * (nomzod o'sha bosqichga ko'chadi, xulosa esa bo'sh qoladi), suhbat
 * tugagach lentadagi O'SHA qatorni ochib xulosani yozadi — tarixda
 * ikkita qator paydo bo'lmaydi.
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
 * <b>⚠️ Tuzatishda maydonlar MAVJUD qiymat bilan to'ldiriladi.</b>
 * Xulosa maydoni bo'sh ochilsa, HR faqat natijani o'zgartirib
 * saqlaganda oldin yozilgan matn o'chib ketardi — server yuborilgan
 * qiymatni ustidan yozadi.
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
  /**
   * TUZATILADIGAN yozuv — lentadagi qatordan ochilganda.
   *
   * Berilsa oyna tahrir rejimida ishlaydi: bosqich o'zgarmaydi,
   * maydonlar mavjud qiymat bilan to'ldiriladi va saqlash YANGI qator
   * qo'shmaydi.
   */
  entry = null,
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
  entry?: CandidateStageEntry | null
  lockedStage?: PipelineStage | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const record = useRecordStage()
  const edit = useEditStageEntry()

  /*
    Ikkala hook ham SHARTSIZ chaqiriladi (React qoidasi), lekin oyna
    bir vaqtda faqat bittasi bilan ishlaydi. Holat (kutish, xato) shu
    yerdan olinadi — pastda ikki tarmoqli shart yozilmasin.
  */
  const saving = entry === null ? record : edit
  /*
   * Faqat FAOL bosqichlar: faolsizlantirilganiga nomzodni o'tkazib
   * bo'lmaydi va server buni 409 bilan rad etadi
   * («bosqich.faol_emas»). Ro'yxatda ko'rsatib, keyin xato berish
   * foydalanuvchini bekorga sarson qilardi.
   */
  const stages = useStages(true)

  const [stageId, setStageId] = useState('')
  const [date, setDate] = useState(today)
  const [conclusion, setConclusion] = useState('')
  const [nextContact, setNextContact] = useState('')

  useEffect(() => {
    if (!open) return
    record.reset()
    edit.reset()

    /*
      Standart bosqich: (1) TUZATILAYOTGAN yozuvniki, (2) QOTIB
      qo'yilgani, (3) nomzodning JORIY bosqichi. Oxirgisi eng ko'p
      uchraydigan holat — «yana gaplashdim». Keyingi bosqichni taxmin
      qilib qo'yish RAD ETILDI: voronkada tartib qat'iy emas va
      noto'g'ri taxmin HR ni e'tiborsizlikka undardi.
    */
    setStageId(entry?.stageId ?? lockedStage?.id ?? candidate.currentStageId ?? '')

    /*
      ⚠️ TUZATISHDA maydonlar yozuvning O'Z qiymatidan boshlanadi.
      Bo'sh ochilsa, HR faqat sanani tuzatib saqlaganda ilgari yozilgan
      xulosa o'chib ketardi: server yuborilgan qiymatni ustidan yozadi
      (`editCandidateStageEntry` izohi).
    */
    setDate(entry?.date ?? today())
    setConclusion(entry?.conclusion ?? '')
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
  }, [open, entry, lockedStage, candidate.currentStageId, candidate.nextContactDate])

  if (!open) return null

  const stageOptions = (stages.data ?? []).map((stage) => ({
    value: stage.id,
    label: stage.name,
  }))

  /*
    Bosqich TANLANMAYDIGAN ikki holat: tezkor qatordan ochilgan oyna
    (bosgan tugmaning bosqichi) va TUZATISH (yozuvning o'z bosqichi —
    u umuman o'zgarmaydi). Ikkalasida ham `disabled` qilingan `Select`
    o'rniga oddiy yozuv chiziladi: xiralashgan tanlagich «ishlamayapti»
    degan ma'no berardi.
  */
  const qotganBosqich = entry?.stage ?? lockedStage?.name ?? null

  const submit = () => {
    if (stageId === '') return

    /*
      Uch maydon ikkala so'rovda ham bir xil. `stageId` esa FAQAT yangi
      yozuvda: tuzatish yozuvning bosqichini o'zgartirmaydi (server
      shartnomasi) va uni yuborish yolg'on va'da bo'lardi.
    */
    const umumiy = {
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
    }

    if (entry === null) {
      record.mutate({ id: candidate.id, stageId, ...umumiy }, { onSuccess: onClose })
      return
    }

    edit.mutate({ id: candidate.id, entryId: entry.id, ...umumiy }, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!saving.isPending}
      title={
        entry !== null
          ? t('recruiting.stageEditTitle')
          : lockedStage === null
            ? t('recruiting.stageRecordTitle')
            : t('recruiting.stageMoveTo', { stage: lockedStage.name })
      }
      description={candidate.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={saving.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={saving.isPending || stageId === ''}
          >
            {saving.isPending && <LoaderCircle className="animate-spin" />}
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
        {saving.isError && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(recruitingErrorCode(saving.error))}
          </Alert>
        )}

        {/*
          Qotib qo'yilgan bosqich — TANLAGICH emas, yozuv. `disabled`
          qilingan `Select` xiralashardi va «ishlamayapti» degan ma'no
          berardi (tezkor qatordagi joriy bosqich yorlig'i bilan aynan
          bir xil mulohaza).
        */}
        {qotganBosqich === null ? (
          <Select
            label={t('recruiting.stage')}
            placeholder={t('recruiting.chooseStage')}
            options={stageOptions}
            value={stageId}
            disabled={saving.isPending || stages.isPending}
            onChange={(event) => setStageId(event.target.value)}
          />
        ) : (
          <div className="space-y-2">
            <p className="text-footnote font-medium text-[var(--fg-secondary)]">
              {t('recruiting.stage')}
            </p>
            <p className="text-body font-medium">{qotganBosqich}</p>
          </div>
        )}

        {/*
          ⭐ «Natija» tanlagichi YO'Q (2026-09-16). Bosqichning o'zi
          holatni aytadi, natija esa xulosa matnida yoziladi — ikkita
          joy bir xil savolga javob berardi.
        */}
        <DateField
          label={t('recruiting.stageDate')}
          value={date}
          disabled={saving.isPending}
          onChange={setDate}
        />

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
          disabled={saving.isPending}
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
        <DateField
          label={t('recruiting.nextContact')}
          hint={t('recruiting.nextContactHint')}
          value={nextContact}
          disabled={saving.isPending}
          onChange={setNextContact}
        />
      </form>
    </Dialog>
  )
}
