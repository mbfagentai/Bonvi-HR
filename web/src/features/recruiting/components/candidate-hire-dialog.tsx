import { useNavigate } from '@tanstack/react-router'
import {
  ArrowRight,
  ChevronDown,
  CircleCheck,
  LoaderCircle,
  TriangleAlert,
} from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { SALARY_VIEW } from '@/features/employees/lib/employee-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Checkbox, DateField, Dialog, Select, TextField } from '@/shared/ui'
import type { Candidate, HireOutcome } from '../api/recruiting-api'
import {
  useEmployeeOptions,
  useFilterOptions,
  useHireCandidate,
  useReturnCandidateToFunnel,
} from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { orNull } from '../lib/recruiting-presentation'

const FORM_ID = 'candidate-hire-form'

/**
 * DOIM ko'rinib turadigan maydonlar — qolgani «Qo'shimcha» ostida.
 *
 * ⚠️ Ro'yxat bezak emas, XATO uchun: server maydonga bog'liq kod
 * qaytarganda (masalan «sinov.majburiy» → `probationDays`) o'sha maydon
 * yig'ilgan bo'limda tursa, xabar EKRANDA UMUMAN CHIQMASDI — forma
 * jimgina yuborilmay qolardi. Shu sababli bu ro'yxatda YO'Q maydonga
 * xato tushsa, «Qo'shimcha» o'zi ochiladi.
 */
const ALWAYS_VISIBLE_FIELDS: ReadonlySet<string> = new Set([
  'legalEntityId',
  'positionId',
  'departmentId',
  'pinfl',
])

/**
 * ⚠️⚠️ ISHGA OLISH — nomzod XODIMGA aylanadi.
 *
 * Voronkaning yakuniy qadami va nomzoddan xodimga o'tishning YAGONA
 * yo'li (`docs/SPEC-nomzod-yakuni.md`, 2-bo'lim). Server bitta tranzaksiyada
 * nomzodni yopadi, XODIM kartasini yaratadi, mehnat munosabatini
 * ochadi va MAJBURIY sinov muddatini boshlaydi — ya'ni bir bosishdan
 * keyin `/xodimlar` va `/sinov` ro'yxatlarida yangi odam paydo bo'ladi.
 *
 * <b>Naqsh `hire-dialog.tsx` (xodimlar moduli) bilan bir xil.</b>
 * Ikkala oyna ham ayni bir ishga qabul oqimini chaqiradi
 * (`HireEmployeeCommand`), ya'ni bir xil maydonlarni so'raydi va bir
 * xil ogohlantirishlarni qaytaradi.
 *
 * <b>⭐ Farqi — MAJBURIY maydon ATIGI IKKITA:</b> yuridik shaxs va
 * lavozim. Qolganining zaxirasi SERVERDA bor (ishga qabul sanasi —
 * bugun, sinov muddati — lavozimdagi qiymat yoki 90 kun, valyuta —
 * UZS). Shu sababli forma ikki qatlamga bo'lingan: yuqorida qaror
 * (qayerga olinadi + JSHSHIR), pastda esa yig'ilgan «Qo'shimcha».
 *
 * <b>⚠️ JSHSHIR «Qo'shimcha» ga YASHIRILMAGAN</b>, garchi u ixtiyoriy
 * bo'lsa ham. Mavjud xodim aynan JSHSHIR bo'yicha topiladi
 * (`HireEmployeeCommandHandler` dagi dublikat qidiruvi) — u yig'ilgan
 * bo'limda tursa, HR uni ko'rmasdan o'tib ketardi va o'sha odamga
 * IKKINCHI karta ochilardi. Yig'ish qulaylik uchun, dublikat esa
 * qo'lda tozalanadigan zarar.
 *
 * <b>Natija alohida qadam.</b> Muvaffaqiyatdan keyin oyna yopilmaydi:
 * u ogohlantirishlarni (`hire.warnings`) ko'rsatadi va yangi xodim
 * kartasiga havola beradi. Ular server javobida FAQAT SHU YERDA keladi
 * va keyin hech qanday so'rovdan olinmaydi.
 */
export function CandidateHireDialog({
  candidate,
  open,
  onClose,
}: {
  candidate: Candidate
  open: boolean
  onClose: () => void
}) {
  const { t, money } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  const { hasPermission } = useAuth()
  /*
    Oklad `salary.view` ruxsatiga bog'liq: ruxsat bo'lmasa maydon BO'SH
    emas, umuman YO'Q — `hire-dialog.tsx` dagi bilan aynan bir xil
    qoida. Bo'sh maydon «oklad kiritilmagan» degan yolg'on taassurot
    qoldirardi va HR uni to'ldirmoqchi bo'lardi.
  */
  const canSeeSalary = hasPermission(SALARY_VIEW)

  const hire = useHireCandidate()
  const restore = useReturnCandidateToFunnel()
  const employees = useEmployeeOptions()
  /*
    Bo'lim, lavozim va firma ro'yxatlari — xodimlar modulining tayyor
    hook'idan. `hire-dialog.tsx` ham aynan shundan oladi: ikkala
    ekranda ro'yxatlar bir xil bo'ladi va kesh ham bitta.
  */
  const options = useFilterOptions()

  const [legalEntityId, setLegalEntityId] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [positionId, setPositionId] = useState('')
  const [pinfl, setPinfl] = useState('')
  const [passport, setPassport] = useState('')
  const [hireDate, setHireDate] = useState('')
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')
  const [salaryAmount, setSalaryAmount] = useState('')
  const [rate, setRate] = useState('1')
  const [isPrimary, setIsPrimary] = useState(true)
  const [responsibleEmployeeId, setResponsibleEmployeeId] = useState('')
  const [probationDays, setProbationDays] = useState('')

  /** «Qo'shimcha» — YIG'ILGAN holatda ochiladi. */
  const [more, setMore] = useState(false)

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)
  /** `null` — hali yuborilmagan; qiymat bo'lsa oyna NATIJA holatida. */
  const [result, setResult] = useState<HireOutcome | null>(null)
  /**
   * Rezervdagi nomzod voronkaga QAYTARILDIMI.
   *
   * ⚠️ Bu bayroq shart, `candidate.status` ga qarab bo'lmaydi: qaytarish
   * o'tib, ishga olish yiqilsa (masalan odam qora ro'yxatda), HR
   * maydonni tuzatib qayta yuboradi — va o'shanda ikkinchi marta
   * qaytarish «nomzod.rezervda_emas» (409) bilan yiqilardi. Kartadagi
   * `status` esa keshdan keladi va o'sha lahzada hali eski bo'lishi
   * mumkin.
   */
  const [restored, setRestored] = useState(false)

  const fromReserve = candidate.status === 'Reserve'
  const pending = hire.isPending || restore.isPending

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)
    setResult(null)
    setRestored(false)
    setMore(false)
    hire.reset()
    restore.reset()

    setLegalEntityId('')
    setDepartmentId('')
    setPositionId('')
    setPinfl('')
    setPassport('')
    setHireDate('')
    setOrderNo('')
    setOrderDate('')
    setSalaryAmount('')
    setRate('1')
    setIsPrimary(true)
    setResponsibleEmployeeId('')
    setProbationDays('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, candidate.id])

  if (!open) return null

  const fieldMessage = (field: string): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  /*
    Firma va lavozim MAJBURIY (server ularsiz «nomzod.yuridik_shaxs_yoq»
    va «nomzod.lavozim_yoq» qaytaradi). Tugma OLDINDAN o'chiriladi: amal
    qaytarib bo'lmaydigan va server xatosini kutish HR ni «bosdim, nima
    bo'ldi?» holatida takrorlashga undardi.
  */
  const incomplete = legalEntityId === '' || positionId === ''

  const submit = () => {
    if (incomplete || pending) return
    setFailure(null)
    setFieldError(null)

    const onError = (error: unknown) => {
      const code = recruitingErrorCode(error)
      const field = RECRUITING_ERROR_FIELD[code]

      if (field === undefined) {
        setFailure(code)
        return
      }

      setFieldError({ field, code })
      // Xato yig'ilgan bo'limdagi maydonga tushgan bo'lsa — bo'limni
      // ochamiz (sabab `ALWAYS_VISIBLE_FIELDS` izohida).
      if (!ALWAYS_VISIBLE_FIELDS.has(field)) setMore(true)
    }

    const send = () => {
      hire.mutate(
        {
          id: candidate.id,
          body: {
            legalEntityId,
            positionId,
            departmentId: departmentId === '' ? null : departmentId,
            hireDate: orNull(hireDate),
            orderNo: orNull(orderNo),
            orderDate: orNull(orderDate),
            pinfl: orNull(pinfl),
            // Pasport doim katta harfda saqlanadi: «ab1234567» →
            // «AB1234567» (`employee-schemas.ts` dagi `toPassport`
            // bilan bir xil qoida).
            passport: passport.trim() === '' ? null : passport.trim().toUpperCase(),
            salaryAmount: canSeeSalary ? numberOrNull(salaryAmount) : null,
            rate: numberOrNull(rate),
            isPrimary,
            responsibleEmployeeId: responsibleEmployeeId === '' ? null : responsibleEmployeeId,
            probationDays: probationDays.trim() === '' ? null : Number(probationDays),
          },
        },
        { onSuccess: (done) => setResult(done.hire), onError },
      )
    }

    /*
      ⭐ Rezervdagi nomzod — IKKI so'rov, shu tartibda.

      Server rezervdagi odamni to'g'ridan-to'g'ri ishga olishdan
      ATAYLAB tiyadi («nomzod.jarayonda_emas», 409) va uni avval
      voronkaga qaytarishni talab qiladi. HR uchun esa bu bitta qaror:
      «bu odamni olamiz». Ikkita tugma bosishga majburlash — server
      cheklovini foydalanuvchiga o'tkazish bo'lardi.

      Birinchisi o'tib, ikkinchisi yiqilsa nomzod voronkada FAOL bo'lib
      qoladi (chiqish sanasi va sababi tozalangan). Bu yo'qotish emas,
      ko'rinadigan holat: karta darhol «Jarayonda» bo'ladi va HR uni
      qaytadan rezervga o'tkaza oladi.
    */
    if (fromReserve && !restored) {
      restore.mutate({ id: candidate.id }, {
        onSuccess: () => {
          setRestored(true)
          send()
        },
        onError,
      })
      return
    }

    send()
  }

  /* ───────────────────────── NATIJA ───────────────────────── */

  if (result !== null) {
    const employeeId = result.employeeId

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
            ⚠️ OGOHLANTIRISHLAR — e'tiborsiz qolmasligi kerak. Ular
            server javobida FAQAT SHU YERDA keladi: keyin hech qanday
            so'rovdan olinmaydi. Kodi tarjima qilinadi, tarjima
            topilmasa serverning o'z matni ko'rsatiladi — xato
            yashirilmaydi (`useErrorMessage` ning zaxira qoidasi).
          */}
          {result.warnings.length > 0 && (
            <Alert tone="warning" title={t('recruiting.acceptWarnings')}>
              <ul className="mt-1 space-y-1.5">
                {result.warnings.map((warning) => (
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

  const salaryValue = numberOrNull(salaryAmount)

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      dismissible={!pending}
      title={t('recruiting.hireTitle')}
      description={candidate.fullName}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            /*
              `primary`, `danger` emas: amal buzuvchi emas, qaytarib
              bo'lmaydigan. Ogohlantirish esa formaning tepasida —
              qizil tugmadan ko'ra ko'proq o'qiladigan joyda.
            */
            variant="primary"
            disabled={pending || incomplete}
          >
            {pending ? <LoaderCircle className="animate-spin" /> : <CircleCheck />}
            {t('recruiting.hireConfirm')}
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
          Rezervdagi nomzod uchun qadam BITTA emas, IKKITA — va buni
          aytib qo'yish kerak: aks holda HR kartada «Rezerv» yorlig'ini
          ko'rib turib, ishga olish qanday o'tganini tushunmasdi.
        */}
        {fromReserve && <Alert tone="info">{t('recruiting.hireFromReserve')}</Alert>}

        {/*
          ⭐ Shtat birligi — BIRINCHI bo'lim: qolgan maydonlarning
          hammasi ixtiyoriy, bu esa qarorning o'zi. Vakansiya faqat
          E'LON — «qaysi firmaga, bo'limga va lavozimga» degan savol
          aynan shu yerda hal bo'ladi (`Vacancy` domen izohi).
        */}
        <Section
          title={t('recruiting.acceptSectionPlacement')}
          hint={t('recruiting.acceptPlacementHint')}
        >
          {/*
            ⚠️ Ma'lumotnoma BO'SH kelgan hol: ro'yxat yuklanmagan yoki
            unda hali yozuv yo'q. Ikkala majburiy maydon ham shu
            ro'yxatlardan to'ladi, ya'ni tugma abadiy o'chiq turardi va
            HR sababini bilmasdan urinaverardi. Yuklanish esa alohida
            holat: u paytda maydonlar `disabled` va bu xabar chiqmaydi.
          */}
          {!options.isPending &&
            (options.legalEntities.length === 0 || options.positions.length === 0) && (
              <Alert tone="warning">{t('recruiting.hireCatalogEmpty')}</Alert>
            )}

          <Select
            label={t('employee.legalEntity')}
            placeholder={t('recruiting.chooseLegalEntity')}
            options={options.legalEntities}
            value={legalEntityId}
            error={fieldMessage('legalEntityId')}
            disabled={pending || options.isPending}
            onChange={(event) => setLegalEntityId(event.target.value)}
          />

          <Select
            label={t('employee.position')}
            placeholder={t('recruiting.choosePosition')}
            options={options.positions}
            value={positionId}
            error={fieldMessage('positionId')}
            disabled={pending || options.isPending}
            onChange={(event) => {
              const next = event.target.value
              setPositionId(next)
              /*
                Lavozim tanlanganda bo'lim O'ZI to'ladi — ma'lumotnomada
                lavozim allaqachon bo'limga biriktirilgan. Faqat bo'lim
                BO'SH bo'lganda ishlaydi: HR ataylab boshqasini tanlagan
                bo'lsa, tegilmaydi (`hire-dialog.tsx` dagi qoida).
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
            disabled={pending || options.isPending}
            onChange={(event) => setDepartmentId(event.target.value)}
          />
        </Section>

        {/*
          ⚠️ JSHSHIR — OCHIQ, «Qo'shimcha» ning ichida emas. Sabab
          komponent boshidagi izohda: dublikat xodim qidiruvi aynan
          shunga tayanadi.
        */}
        <Section title={t('recruiting.hireSectionIdentity')} hint={t('recruiting.hirePinflWhy')}>
          <TextField
            label={t('employee.pinfl')}
            hint={t('recruiting.acceptPinflHint')}
            inputMode="numeric"
            autoComplete="off"
            value={pinfl}
            error={fieldMessage('pinfl')}
            disabled={pending}
            onChange={(event) => setPinfl(event.target.value)}
          />
        </Section>

        {/*
          «Qo'shimcha» — YIG'ILGAN. Ichidagi HAR BIR maydonning zaxira
          qiymati serverda bor, ya'ni oynani umuman ochmasdan ham ishga
          olish o'tadi. Har bir maydon yonida esa o'sha zaxira YOZILGAN:
          «bo'sh qoldirsam nima bo'ladi?» degan savol maydonning
          o'zidan javob olishi kerak.
        */}
        <section className="overflow-hidden rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)]">
          <button
            type="button"
            aria-expanded={more}
            onClick={() => setMore((previous) => !previous)}
            className="press flex w-full items-center gap-2 p-4 text-left"
          >
            <ChevronDown
              className={`size-4 shrink-0 text-[var(--fg-tertiary)] transition-transform duration-200 ${
                more ? 'rotate-180' : ''
              }`}
              strokeWidth={2.5}
            />
            <span className="min-w-0 flex-1">
              <span className="block text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
                {t('recruiting.hireMoreTitle')}
              </span>
              <span className="mt-1 block text-caption1 text-[var(--fg-tertiary)]">
                {t('recruiting.hireMoreHint')}
              </span>
            </span>
          </button>

          {more && (
            <div className="space-y-4 px-4 pb-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <TextField
                  label={t('employee.passport')}
                  placeholder="AA1234567"
                  autoComplete="off"
                  value={passport}
                  error={fieldMessage('passport')}
                  disabled={pending}
                  onChange={(event) => setPassport(event.target.value)}
                />
                <DateField
                  label={t('employee.hireDate')}
                  hint={t('recruiting.hireDateHint')}
                  value={hireDate}
                  disabled={pending}
                  onChange={setHireDate}
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <TextField
                  label={t('employee.orderNo')}
                  placeholder="№ 91"
                  autoComplete="off"
                  value={orderNo}
                  disabled={pending}
                  onChange={(event) => setOrderNo(event.target.value)}
                />
                <DateField
                  label={t('employee.orderDate')}
                  value={orderDate}
                  disabled={pending}
                  onChange={setOrderDate}
                />
              </div>

              {/*
                Kelishilgan oklad faqat SHU maydondan keladi
                (`HireCandidateCommand.SalaryAmount` izohi). Bo'sh
                qoldirilsa munosabat oklad yozuvisiz ochiladi.
              */}
              {canSeeSalary ? (
                <TextField
                  label={t('employee.salary')}
                  placeholder="4500000"
                  inputMode="numeric"
                  hint={
                    salaryValue === null ? t('recruiting.hireSalaryHint') : money(salaryValue)
                  }
                  value={salaryAmount}
                  error={fieldMessage('salaryAmount')}
                  disabled={pending}
                  inputClassName="tabular"
                  onChange={(event) => setSalaryAmount(event.target.value)}
                />
              ) : (
                <Alert tone="info">{t('employees.salaryHidden')}</Alert>
              )}

              <div className="grid gap-4 sm:grid-cols-2">
                <TextField
                  label={t('recruiting.rate')}
                  hint={t('recruiting.rateHint')}
                  inputMode="decimal"
                  value={rate}
                  error={fieldMessage('rate')}
                  disabled={pending}
                  onChange={(event) => setRate(event.target.value)}
                />

                {/*
                  Sinov muddati BO'SH qoldirilishi mumkin — server
                  lavozimdagi qiymatni, u ham bo'lmasa 90 kunni oladi.
                  NOL yoki manfiy qiymat esa XATO: sinovsiz qabul yo'q
                  («sinov.majburiy»). Shu sababli `min={1}`.
                */}
                <TextField
                  label={t('recruiting.probationDays')}
                  hint={t('recruiting.probationDaysHint')}
                  type="number"
                  min={1}
                  inputMode="numeric"
                  value={probationDays}
                  error={fieldMessage('probationDays')}
                  disabled={pending}
                  onChange={(event) => setProbationDays(event.target.value)}
                />
              </div>

              <Select
                label={t('recruiting.mentor')}
                placeholder={t('safety.noResponsible')}
                options={employees.options}
                value={responsibleEmployeeId}
                disabled={pending || employees.isPending}
                onChange={(event) => setResponsibleEmployeeId(event.target.value)}
              />

              <Checkbox
                checked={isPrimary}
                onChange={setIsPrimary}
                disabled={pending}
                label={t('recruiting.isPrimary')}
              />
            </div>
          )}
        </section>

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

/** Forma bo'limi — sarlavha, izoh va maydonlar guruhi. */
function Section({
  title,
  hint,
  children,
}: {
  title: string
  hint?: string
  children: ReactNode
}) {
  return (
    <section className="space-y-4 rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
      <div className="space-y-1">
        <h3 className="text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
          {title}
        </h3>
        {hint !== undefined && <p className="text-caption1 text-[var(--fg-tertiary)]">{hint}</p>}
      </div>
      {children}
    </section>
  )
}

/**
 * Stavka va oklad uchun son.
 *
 * Qoidasi xodimlar modulidagi `toAmount` bilan AYNAN bir xil: probel
 * tashlanadi («4 500 000»), vergul nuqtaga aylanadi («0,5»). Ikkala
 * ekranda bir xil kiritilgan matn bir xil natija berishi kerak — HR
 * summani odatda nusxa ko'chirib qo'yadi.
 *
 * Bo'sh yoki son bo'lmagan qiymat — `null`, ya'ni server o'z zaxirasini
 * oladi (oklad uchun: yozuv umuman ochilmaydi).
 */
function numberOrNull(value: string): number | null {
  const normalized = value.replaceAll(/\s/gu, '').replace(',', '.')
  if (normalized === '') return null
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : null
}
