import { useNavigate } from '@tanstack/react-router'
import { ArrowRight, CircleCheck, LoaderCircle, TriangleAlert } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Button,
  Checkbox,
  Dialog,
  Select,
  TextArea,
  TextField,
} from '@/shared/ui'
import type { Candidate, CandidateHireResult, JobOffer } from '../api/recruiting-api'
import {
  useAcceptOffer,
  useEmployeeOptions,
  useFilterOptions,
} from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { orNull, today } from '../lib/recruiting-presentation'

const FORM_ID = 'offer-accept-form'

/**
 * ⚠️⚠️ TAKLIF QABUL QILINDI — nomzod XODIMGA aylanadi.
 *
 * Butun 2-bosqichning eng jiddiy amali va u QAYTARIB BO'LMAYDI.
 * Server bitta tranzaksiyada to'rt ish qiladi: taklifni yopadi,
 * nomzodni «ishga olingan» qiladi, XODIM kartasini yaratadi va unga
 * MAJBURIY sinov muddatini boshlaydi. Ya'ni bir bosishdan keyin
 * `/xodimlar` va `/sinov` ro'yxatlarida yangi odam paydo bo'ladi.
 *
 * Shu sababli oyna UCH qismdan iborat:
 *
 * 1. <b>Ogohlantirish.</b> Nima bo'lishi ro'yxat bo'lib, aniq
 *    yozilgan — «tasdiqlaysizmi?» degan mavhum savol emas.
 * 2. <b>Forma.</b> FAQAT hujjatiy qism so'raladi: JSHSHIR, pasport,
 *    buyruq, stavka, sinov muddati. Shaxsiy ma'lumot NOMZODDAN
 *    ko'chadi va qaytadan kiritilmaydi — voronkaning butun ma'nosi
 *    shu (`AcceptJobOfferCommand` izohi).
 * 3. <b>Natija.</b> Muvaffaqiyatdan keyin oyna YOPILMAYDI: u
 *    ogohlantirishlarni (`hire.warnings`) ko'rsatadi va yangi xodim
 *    kartasiga havola beradi.
 *
 * <b>Nega natija alohida qadam.</b> Ogohlantirishlar XATO EMAS —
 * amal bajarildi, lekin HR bilishi kerak bo'lgan narsa bor (masalan
 * stavka chegarasi). Oyna darhol yopilsa, ular bilan birga
 * yo'qolardi va hech qayerda qayta ko'rinmasdi: server ularni faqat
 * SHU javobda qaytaradi, keyin ular hech qanday so'rovdan olinmaydi.
 *
 * <b>Nega JSHSHIR bu yerda so'raladi.</b> Nomzodda u bo'lmaydi (u
 * hujjat topshirmagan), xodimda esa JSHSHIR — asosiy identifikator
 * va dublikat qidiruvining kaliti. Bo'sh qoldirilsa ham qabul
 * o'tadi: Excel'da ham 540 xodimdan uchtasida JSHSHIR yo'q edi.
 *
 * <b>⭐ FIRMA, BO'LIM va LAVOZIM ham AYNAN shu yerda tanlanadi.</b>
 * Ilgari uchalasi vakansiyadan olinardi va bu oynada umuman
 * ko'rinmasdi. Endi vakansiya faqat E'LON — kompaniya tuzilmasidan
 * unda hech narsa saqlanmaydi (backend'dagi `Vacancy` domen izohi).
 * Ya'ni bu forma — <b>e'lon bilan shtat jadvali uchrashadigan yagona
 * nuqta</b>: «Сотувчи-консультант» e'loniga kelgan odam bugun qaysi
 * firmaga, qaysi bo'limga va qaysi shtat birligiga olinayotgani shu
 * yerda hal bo'ladi va shu yerdan hujjatga tushadi.
 *
 * <b>Nega bu yaxshiroq.</b> Vakansiya ochilayotganda bu savollarning
 * javobi ko'pincha hali yo'q («odam kerak, qaysi do'konga — ko'ramiz»),
 * qabul kunida esa u har doim aniq. Ilgari HR javobni oldindan taxmin
 * qilib vakansiyaga yozardi, keyin esa uni tuzatish uchun VAKANSIYANI
 * tahrirlash kerak bo'lardi — ya'ni boshqa nomzodlarning ham
 * «kelajagi» o'zgarardi.
 */
export function OfferAcceptDialog({
  candidate,
  offer,
  onClose,
}: {
  candidate: Candidate
  /** `null` — oyna yopiq. */
  offer: JobOffer | null
  onClose: () => void
}) {
  const { t, money } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  const accept = useAcceptOffer()
  const employees = useEmployeeOptions()
  /*
    Bo'lim, lavozim va firma ro'yxatlari — xodimlar modulining tayyor
    hook'idan (`useFilterOptions`). NEGA nusxa yozilmadi: u aynan shu
    uchta ma'lumotnomani yig'adi va ishga qabul formasi
    (`hire-dialog.tsx`) allaqachon undan foydalanadi — ya'ni ikkala
    ekranda ro'yxatlar bir xil bo'ladi va kesh ham bitta.
  */
  const options = useFilterOptions()

  const [legalEntityId, setLegalEntityId] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [positionId, setPositionId] = useState('')
  const [respondedAt, setRespondedAt] = useState(today)
  const [responseNote, setResponseNote] = useState('')
  const [hireDate, setHireDate] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [pinfl, setPinfl] = useState('')
  const [passport, setPassport] = useState('')
  const [rate, setRate] = useState('1')
  const [isPrimary, setIsPrimary] = useState(true)
  const [responsibleEmployeeId, setResponsibleEmployeeId] = useState('')
  const [probationDays, setProbationDays] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)
  /** `null` — hali yuborilmagan; qiymat bo'lsa oyna NATIJA holatida. */
  const [result, setResult] = useState<CandidateHireResult | null>(null)

  useEffect(() => {
    if (offer === null) return
    setFailure(null)
    setFieldError(null)
    setResult(null)
    accept.reset()

    setLegalEntityId('')
    setDepartmentId('')
    setPositionId('')
    setRespondedAt(today())
    setResponseNote('')
    // Ishga qabul sanasi — taklifdagi ish boshlash sanasi. Server ham
    // bo'sh qoldirilganda aynan shuni oladi; maydonda ko'rsatib
    // qo'yish HR ga uni tuzatish imkonini beradi.
    setHireDate(offer.startDate)
    setOrderNo('')
    setOrderDate(today())
    setPinfl('')
    setPassport('')
    setRate('1')
    setIsPrimary(true)
    setResponsibleEmployeeId('')
    setProbationDays('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [offer])

  if (offer === null) return null

  const fieldMessage = (field: string): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  /*
    Firma va lavozim MAJBURIY (server ularsiz «taklif.yuridik_shaxs_yoq»
    va «taklif.lavozim_yoq» qaytaradi). Bo'lim ixtiyoriy —
    `HireEmployeeCommand` dagi bilan bir xil.

    Tugma OLDINDAN o'chiriladi: server xatosini kutib turish bu yerda
    ayniqsa yomon bo'lardi — amal qaytarib bo'lmaydigan va HR uni
    «bosdim, nima bo'ldi?» holatida takrorlashga urinardi.
  */
  const incomplete = legalEntityId === '' || positionId === ''

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    accept.mutate(
      {
        id: offer.id,
        body: {
          legalEntityId,
          positionId,
          departmentId: departmentId === '' ? null : departmentId,
          respondedAt: orNull(respondedAt),
          responseNote: orNull(responseNote),
          hireDate: orNull(hireDate),
          orderNo: orNull(orderNo),
          orderDate: orNull(orderDate),
          pinfl: orNull(pinfl),
          // Pasport doim katta harfda saqlanadi: «ab1234567» → «AB1234567»
          // (`employee-schemas.ts` dagi `toPassport` bilan bir xil qoida).
          passport: passport.trim() === '' ? null : passport.trim().toUpperCase(),
          rate: rateOrNull(rate),
          isPrimary,
          responsibleEmployeeId: responsibleEmployeeId === '' ? null : responsibleEmployeeId,
          probationDays: probationDays.trim() === '' ? null : Number(probationDays),
        },
      },
      {
        onSuccess: setResult,
        onError: (error) => {
          const code = recruitingErrorCode(error)
          const field = RECRUITING_ERROR_FIELD[code]
          if (field === undefined) setFailure(code)
          else setFieldError({ field, code })
        },
      },
    )
  }

  /* ───────────────────────── NATIJA ───────────────────────── */

  if (result !== null) {
    const employeeId = result.hire.employeeId

    return (
      <Dialog
        open
        onClose={onClose}
        title={t('recruiting.acceptDoneTitle')}
        description={candidate.fullName}
        footer={
          <>
            <Button type="button" onClick={onClose}>
              {t('common.close')}
            </Button>
            {employeeId !== null && (
              <Button
                type="button"
                variant="primary"
                onClick={() => {
                  onClose()
                  void navigate({ to: '/xodimlar/$employeeId', params: { employeeId } })
                }}
              >
                {t('recruiting.openEmployee')}
                <ArrowRight />
              </Button>
            )}
          </>
        }
      >
        <div className="space-y-4">
          <Alert tone="success" title={t('recruiting.acceptDoneHeading')}>
            {t('recruiting.acceptDoneText')}
          </Alert>

          {/*
            ⚠️ OGOHLANTIRISHLAR — e'tiborsiz qolmasligi kerak.
            Ular server javobida FAQAT SHU YERDA keladi: keyin hech
            qanday so'rovdan olinmaydi. Kodi tarjima qilinadi, tarjima
            topilmasa serverning o'z matni ko'rsatiladi — xato
            yashirilmaydi (`useErrorMessage` ning zaxira qoidasi).
          */}
          {result.hire.warnings.length > 0 && (
            <Alert tone="warning" title={t('recruiting.acceptWarnings')}>
              <ul className="mt-1 space-y-1.5">
                {result.hire.warnings.map((warning) => (
                  <li key={warning.code} className="flex gap-2 text-footnote">
                    <TriangleAlert className="mt-0.5 size-3.5 shrink-0" strokeWidth={2} />
                    <span>{errorMessage(warning.code, warning.message)}</span>
                  </li>
                ))}
              </ul>
            </Alert>
          )}
        </div>
      </Dialog>
    )
  }

  /* ───────────────────────── FORMA ───────────────────────── */

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      dismissible={!accept.isPending}
      title={t('recruiting.acceptTitle')}
      description={candidate.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={accept.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            /*
              «Yaratish» tugmasi EMAS, `primary`: bu yozuv qo'shish
              emas, tasdiqlash. `danger` ham emas — amal buzuvchi
              emas, qaytarib bo'lmaydigan. Ogohlantirish esa
              formaning tepasida, qizil tugmadan ko'ra ko'proq
              o'qiladigan joyda turadi.
            */
            variant="primary"
            disabled={accept.isPending || incomplete}
          >
            {accept.isPending ? (
              <LoaderCircle className="animate-spin" />
            ) : (
              <CircleCheck />
            )}
            {t('recruiting.acceptConfirm')}
          </Button>
        </>
      }
    >
      <form
        id={FORM_ID}
        noValidate
        className="space-y-5"
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

        {/* ⚠️ Nima bo'lishi — mavhum «tasdiqlaysizmi?» emas, aniq ro'yxat */}
        <Alert tone="warning" title={t('recruiting.acceptWarningTitle')}>
          <ul className="mt-1 list-disc space-y-1 pl-4 text-footnote">
            <li>{t('recruiting.acceptEffectEmployee')}</li>
            <li>{t('recruiting.acceptEffectProbation')}</li>
            <li>{t('recruiting.acceptEffectVacancy')}</li>
            <li className="font-medium">{t('recruiting.acceptEffectIrreversible')}</li>
          </ul>
        </Alert>

        {/*
          Xulosa qatorida VAKANSIYA nomi (e'lon nomi) turadi.

          NEGA yangi kalit (`acceptOfferSummary`): eskisi
          `{{position}}` o'rnini kutardi va u yerga lavozim nomi
          qo'yilardi. Endi bu qiymat lavozim EMAS — o'rin nomini
          o'zgartirmasdan qoldirish kod bilan matn o'rtasida jimgina
          yolg'on hosil qilardi.
        */}
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('recruiting.acceptOfferSummary', {
            vacancy: offer.vacancy ?? '—',
            salary: money(offer.salary.amount, offer.salary.currency),
            date: offer.startDate,
          })}
        </p>

        {/*
          ⭐ Shtat birligi — BIRINCHI bo'lim va ataylab shunday: qolgan
          maydonlarning yarmi ixtiyoriy, bu esa qarorning o'zi. Yuqoridagi
          jumla «qaysi e'longa kelgan»ini aytadi, bu bo'lim esa «qayerga
          olinadi»ni so'raydi — ikkalasi yonma-yon turgani muhim.
        */}
        <Section
          title={t('recruiting.acceptSectionPlacement')}
          hint={t('recruiting.acceptPlacementHint')}
        >
          <Select
            label={t('employee.legalEntity')}
            placeholder={t('recruiting.chooseLegalEntity')}
            options={options.legalEntities}
            value={legalEntityId}
            error={fieldMessage('legalEntityId')}
            disabled={accept.isPending || options.isPending}
            onChange={(event) => setLegalEntityId(event.target.value)}
          />

          <Select
            label={t('employee.position')}
            placeholder={t('recruiting.choosePosition')}
            options={options.positions}
            value={positionId}
            error={fieldMessage('positionId')}
            disabled={accept.isPending || options.isPending}
            onChange={(event) => {
              const next = event.target.value
              setPositionId(next)
              /*
                Lavozim tanlanganda bo'lim O'ZI to'ladi — ma'lumotnomada
                lavozim allaqachon bo'limga biriktirilgan
                (`FilterOptions.positionDepartment`). Bu `hire-dialog.tsx`
                dagi AYNAN o'sha qulaylik va u yerdagi bilan bir xil
                qoida: faqat bo'lim BO'SH bo'lganda ishlaydi, ya'ni HR
                ataylab boshqa bo'lim tanlagan bo'lsa — tegilmaydi.
              */
              const suggested = options.positionDepartment.get(next)
              if (suggested !== undefined && departmentId === '') setDepartmentId(suggested)
            }}
          />

          <Select
            label={t('employee.department')}
            placeholder={t('recruiting.noDepartment')}
            options={options.departments}
            value={departmentId}
            error={fieldMessage('departmentId')}
            disabled={accept.isPending || options.isPending}
            onChange={(event) => setDepartmentId(event.target.value)}
          />
        </Section>

        <Section title={t('recruiting.acceptSectionDocuments')}>
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('employee.pinfl')}
              hint={t('recruiting.acceptPinflHint')}
              inputMode="numeric"
              autoComplete="off"
              value={pinfl}
              error={fieldMessage('pinfl')}
              disabled={accept.isPending}
              onChange={(event) => setPinfl(event.target.value)}
            />
            <TextField
              label={t('employee.passport')}
              placeholder="AA1234567"
              autoComplete="off"
              value={passport}
              error={fieldMessage('passport')}
              disabled={accept.isPending}
              onChange={(event) => setPassport(event.target.value)}
            />
          </div>
        </Section>

        <Section title={t('recruiting.acceptSectionOrder')}>
          <div className="grid gap-4 sm:grid-cols-3">
            <TextField
              label={t('employee.hireDate')}
              type="date"
              value={hireDate}
              disabled={accept.isPending}
              onChange={(event) => setHireDate(event.target.value)}
            />
            <TextField
              label={t('employee.orderNo')}
              autoComplete="off"
              value={orderNo}
              disabled={accept.isPending}
              onChange={(event) => setOrderNo(event.target.value)}
            />
            <TextField
              label={t('employee.orderDate')}
              type="date"
              value={orderDate}
              disabled={accept.isPending}
              onChange={(event) => setOrderDate(event.target.value)}
            />
          </div>
        </Section>

        <Section title={t('recruiting.acceptSectionTerms')}>
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label={t('recruiting.rate')}
              hint={t('recruiting.rateHint')}
              inputMode="decimal"
              value={rate}
              error={fieldMessage('rate')}
              disabled={accept.isPending}
              onChange={(event) => setRate(event.target.value)}
            />

            {/*
              Sinov muddati BO'SH qoldirilishi mumkin — server
              lavozimdagi qiymatni, u ham bo'lmasa 90 kunni oladi.
              NOL yoki manfiy qiymat esa XATO: sinovsiz qabul yo'q
              (`AcceptJobOfferCommand.ProbationDays` izohi). Shu
              sababli `min={1}`.
            */}
            <TextField
              label={t('recruiting.probationDays')}
              hint={t('recruiting.probationDaysHint')}
              type="number"
              min={1}
              inputMode="numeric"
              value={probationDays}
              error={fieldMessage('probationDays')}
              disabled={accept.isPending}
              onChange={(event) => setProbationDays(event.target.value)}
            />
          </div>

          <Select
            label={t('recruiting.mentor')}
            placeholder={t('safety.noResponsible')}
            options={employees.options}
            value={responsibleEmployeeId}
            disabled={accept.isPending || employees.isPending}
            onChange={(event) => setResponsibleEmployeeId(event.target.value)}
          />

          <Checkbox
            checked={isPrimary}
            onChange={setIsPrimary}
            disabled={accept.isPending}
            label={t('recruiting.isPrimary')}
          />
        </Section>

        <Section title={t('recruiting.acceptSectionResponse')}>
          <TextField
            label={t('recruiting.respondedAt')}
            type="date"
            value={respondedAt}
            disabled={accept.isPending}
            onChange={(event) => setRespondedAt(event.target.value)}
          />
          <TextArea
            label={t('recruiting.responseNote')}
            placeholder={t('recruiting.responseNotePlaceholder')}
            value={responseNote}
            disabled={accept.isPending}
            onChange={(event) => setResponseNote(event.target.value)}
          />
        </Section>

        {/*
          Shaxsiy ma'lumot (F.I.Sh., tug'ilgan sana, telefon, manzil)
          bu yerda QAYTA SO'RALMAYDI — u nomzoddan to'g'ridan-to'g'ri
          ko'chadi. Buni aytib qo'yish kerak: aks holda HR maydonlarni
          «unutilgan» deb o'ylab, nomzodni tahrirlashga qaytardi.
        */}
        <p className="text-caption2 text-[var(--fg-tertiary)]">
          {t('recruiting.acceptPersonalHint')}
        </p>
      </form>
    </Dialog>
  )
}

/**
 * Forma bo'limi.
 *
 * Oyna uzun (o'nta maydon) — bo'limlarsiz u bitta uzluksiz ro'yxatga
 * aylanardi va HR qaysi maydon nimaga kerakligini ajrata olmasdi.
 * `hire-dialog.tsx` bu muammoni QADAMLAR bilan yechgan (uch bosqichli
 * sehrgar), lekin bu yerda qadam kerak emas: maydonlarning yarmi
 * ixtiyoriy va HR ko'pincha faqat buyruq raqamini yozadi. Qadamga
 * bo'lish uni uch marta «Davom etish» bosishga majbur qilardi.
 */
function Section({
  title,
  hint,
  children,
}: {
  title: string
  /** Bo'lim nima uchun kerakligini bir jumlada aytadi. Faqat kerak joyda. */
  hint?: string
  children: ReactNode
}) {
  return (
    <section className="space-y-4 rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
      <div className="space-y-1">
        <h3 className="text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
          {title}
        </h3>
        {hint !== undefined && (
          <p className="text-caption1 text-[var(--fg-tertiary)]">{hint}</p>
        )}
      </div>
      {children}
    </section>
  )
}

/** Stavka: «0,5» ham, «0.5» ham qabul qilinadi (`toRate` bilan bir xil qoida). */
function rateOrNull(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (normalized === '') return null
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : null
}
