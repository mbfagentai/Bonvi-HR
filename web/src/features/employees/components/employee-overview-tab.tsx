import {
  ArrowRightLeft,
  BadgeCheck,
  Building2,
  CalendarClock,
  CalendarDays,
  CircleSlash,
  ExternalLink,
  Gavel,
  Globe,
  IdCard,
  ListPlus,
  LogOut,
  MapPin,
  Network,
  Phone,
  Sparkles,
  TriangleAlert,
  UserRound,
  Wallet,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { formatPhone } from '@/shared/lib/phone'
import { Badge, Button, Card, CardContent, CardHeader, EmptyState } from '@/shared/ui'
import type { EmployeeDetails, EmploymentSummary } from '../api/employees-api'
import { useEmployeeDirections } from '../api/employees-queries'
import {
  EMPLOYMENT_DUTY,
  EMPLOYMENT_PROBATION_COMPLETE,
  EMPLOYMENT_PROBATION_EXTEND,
  EMPLOYMENT_SALARY,
  EMPLOYMENT_TERMINATE,
  EMPLOYMENT_TRANSFER,
  RECRUITING_VIEW,
  SALARY_VIEW,
  formatPinfl,
  formatRate,
} from '../lib/employee-presentation'
import { DirectionBadge, StatusBadge } from './employee-badges'
import { EmployeeCheckFiles } from './employee-check-files'
import { EmployeeCouncilSection } from './employee-council-section'
import { MehnatUzBlock } from './mehnat-uz-block'

/**
 * «Umumiy» tab — shaxs haqidagi hamma narsa bitta ekranda.
 *
 * Uch blokga bo'lingan: kim (shaxsiy), qayerda ishlaydi (mehnat
 * munosabatlari) va tekshiruvlar. HR anketani shu tartibda to'ldiradi,
 * shu sababli o'qish ham shu tartibda.
 */
/**
 * Mehnat munosabati ustidagi amallar.
 *
 * Bitta `onTerminate` o'rniga ro'yxat: HR kartadan turib ko'chirish,
 * qo'shimcha vazifa, oklad va bo'shatishni bajaradi. Har biri uchun
 * alohida prop bo'lsa, yangi amal qo'shilganda zanjirning har bir
 * bo'g'iniga tegishga to'g'ri kelardi.
 *
 * Oxirgi ikkitasi — sinov muddati amallari. Ular ro'yxatga QO'SHILDI,
 * chunki ikkalasi ham aynan shu mehnat munosabatiga tegishli va bir xil
 * yo'l bilan yuqoriga uzatiladi; alohida prop qilish esa yuqoridagi izoh
 * ogohlantirgan holatning o'zi bo'lardi.
 */
export type EmploymentAction =
  | 'transfer'
  | 'duty'
  | 'salary'
  | 'terminate'
  | 'probationResolve'
  | 'probationExtend'

export function EmployeeOverviewTab({
  employee,
  onAction,
}: {
  employee: EmployeeDetails
  /**
   * Amal oynasini ochish. `null` — kartada hech qanday amal tugmasi yo'q.
   *
   * Qaysi tugma chizilishini bu yer emas, har bir tugmaning O'Z ruxsati
   * hal qiladi (`EmploymentCard`): amallar endi «hammasi yoki hech biri»
   * emas.
   */
  onAction: ((action: EmploymentAction, employment: EmploymentSummary) => void) | null
}) {
  const { t, date } = useLocale()
  const { hasPermission } = useAuth()

  const canViewRecruiting = hasPermission(RECRUITING_VIEW)
  const directions = useEmployeeDirections(employee)

  return (
    <div className="grid gap-5 lg:grid-cols-5">
      {/* ───────── Shaxsiy ma'lumotlar ───────── */}
      <Card className="lg:col-span-3">
        <CardHeader title={t('employees.personalInfo')} />
        <CardContent>
          <dl className="grid gap-x-8 gap-y-0 sm:grid-cols-2">
            <InfoRow
              icon={CalendarDays}
              label={t('employee.birthDate')}
              value={employee.birthDate === null ? null : date(employee.birthDate)}
            />
            <InfoRow
              icon={IdCard}
              label={t('employee.pinfl')}
              value={formatPinfl(employee.pinfl)}
              mono
            />
            <InfoRow
              icon={IdCard}
              label={t('employee.passport')}
              value={employee.passport}
              mono
            />
            <InfoRow
              icon={Phone}
              label={t('employee.phone')}
              value={formatPhone(employee.phone)}
              mono
            />
            <InfoRow
              icon={Phone}
              label={t('employees.secondPhone')}
              value={formatPhone(employee.secondPhone)}
              mono
            />
            <InfoRow
              icon={Sparkles}
              label={t('employee.speciality')}
              value={employee.speciality}
            />
            <InfoRow
              icon={MapPin}
              label={t('employee.address')}
              value={employee.address}
              className="sm:col-span-2"
            />
            <InfoRow
              icon={Globe}
              label={t('employee.telegram')}
              className="sm:col-span-2"
              value={
                employee.telegramUrl === null ? null : (
                  <a
                    href={employee.telegramUrl}
                    target="_blank"
                    rel="noreferrer noopener"
                    /* Havola matni — `--accent-text`: `--accent` oq matn
                       ostidagi fon uchun, yorug' fonda esa kichik matn
                       AA dan o'tishi uchun to'qroq pog'ona kerak. */
                    className="inline-flex items-center gap-1.5 text-[var(--accent-text)] hover:underline"
                  >
                    {employee.telegramUrl.replace(/^https?:\/\/(t\.me\/)?/u, '@')}
                    <ExternalLink className="size-3.5" strokeWidth={2.2} />
                  </a>
                )
              }
            />
          </dl>
        </CardContent>
      </Card>

      {/* ───────── Yo'nalishlar va tekshiruvlar ───────── */}
      <div className="space-y-5 lg:col-span-2">
        <Card>
          <CardHeader title={t('employee.direction')} />
          <CardContent>
            {directions.length === 0 ? (
              <p className="text-footnote text-[var(--fg-tertiary)]">
                {t('employees.noDirections')}
              </p>
            ) : (
              <div className="flex flex-wrap gap-1.5">
                {directions.map((direction) => (
                  <DirectionBadge key={direction.id} name={direction.name} size="md" />
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader
            title={t('employees.checks')}
            subtitle={t('employees.checksHint')}
          />
          <CardContent className="space-y-2.5">
            {/*
              «mehnat.uz: ha/yo'q» va «Xavfsizlik tekshiruvi» qatorlari
              OLIB TASHLANDI (foydalanuvchi qarori). Birinchisi bir xil
              savolni ikki marta so'rardi — havola va «Materiallar»
              ro'yxati shu kartada turibdi va javobni ham, dalilni ham
              beradi. Ikkinchisi esa bazada bironta ham to'ldirilmagan
              edi: HR uni hech qachon ishlatmagan.
            */}
            <MehnatUzBlock url={employee.mehnatUzUrl} />

            {/*
              Sudlanganlik ikki qismdan: JAVOB (ha / yo'q / noma'lum) va
              uning TAFSILOTI. Ilgari faqat matn bor edi va undan javobni
              o'qib bo'lmasdi — bo'sh matn «sudlanmagan» degani emas,
              «hech kim yozmagan» degani. Endi javob tahrirlash oynasida
              tanlanadi, matn esa uning izohi bo'lib qoladi.
            */}
            <div className="flex items-start gap-2.5 rounded-md bg-[var(--bg-sunken)] px-3 py-2.5">
              <Gavel
                className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]"
                strokeWidth={2}
              />
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-footnote font-medium text-[var(--fg-secondary)]">
                    {t('employees.criminalRecord')}
                  </p>
                  <CriminalRecordBadge value={employee.hasCriminalRecord} />
                </div>

                {/*
                  Tafsilot faqat YOZILGAN bo'lsa chiziladi: bo'sh joyda
                  «—» turishi javobni takrorlardi va qatorni uzaytirardi.
                */}
                {employee.criminalRecordNote !== null && (
                  <p className="mt-1 text-subhead text-[var(--fg)]">
                    {employee.criminalRecordNote}
                  </p>
                )}
              </div>
            </div>

            {/*
              Dalillar — kartaning OXIRIDA va barcha tekshiruvlarga umumiy:
              mehnat.uz skrinshoti ham, qarzdorlik yoki sudlanganlik
              ma'lumotnomasi ham shu ro'yxatda (2026-09-27).
            */}
            {/*
              `key` — xodim almashganda blok QAYTA o'rnatiladi: yuklash
              jarayoni va xatolar ro'yxati oldingi odamning kartasida qoladi,
              yangisiga o'tib ketmaydi (sahifa o'zi qayta o'rnatilmaydi).
            */}
            <EmployeeCheckFiles key={employee.id} employeeId={employee.id} />
          </CardContent>
        </Card>
      </div>

      {/*
        ───────── Kengash xulosasi ─────────

        Ruxsati yo'q foydalanuvchida bo'lim UMUMAN chizilmaydi (bo'sh
        yoki xatoli holat ko'rsatilmaydi) — kartadagi tablar bilan bir
        xil qoida. Ruxsat `recruiting.view` va u `employees.view` dan
        MUSTAQIL: suhbat xulosalari va rad etish sabablari alohida
        toifadagi ma'lumot, xodimlar ro'yxatini ochgan har bir kishi
        ularni ko'rishi shart emas (sababi backend'dagi
        `Permissions.RecruitingView` izohida).
      */}
      {canViewRecruiting && <EmployeeCouncilSection employeeId={employee.id} />}

      {/* ───────── Mehnat munosabatlari ───────── */}
      <Card className="lg:col-span-5">
        <CardHeader
          title={t('employees.employments')}
          subtitle={t('employees.employmentsHint')}
        />
        <CardContent>
          {employee.employments.length === 0 ? (
            <EmptyState
              icon={Building2}
              title={t('employees.noEmployments')}
              description={t('employees.noEmploymentsText')}
              className="py-10"
            />
          ) : (
            <ul className="grid gap-3 xl:grid-cols-2">
              {employee.employments.map((employment) => (
                <EmploymentCard
                  key={employment.id}
                  employment={employment}
                  onAction={onAction}
                />
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

/** Bitta ish joyi: firma, bo'lim, lavozim, stavka, oklad va buyruq. */
function EmploymentCard({
  employment,
  onAction,
}: {
  employment: EmploymentSummary
  onAction: ((action: EmploymentAction, employment: EmploymentSummary) => void) | null
}) {
  const { t, date, money } = useLocale()
  const { hasPermission } = useAuth()

  const canSeeSalary = hasPermission(SALARY_VIEW)
  const closed = employment.status === 'terminated'

  /*
    Har bir amal — O'Z ruxsati bilan (ilgari hammasi bitta
    `employment.manage` edi). Tugma faqat bosilganda server qabul
    qiladigan odamga chiziladi.
  */
  const canTransfer = hasPermission(EMPLOYMENT_TRANSFER)
  const canDuty = hasPermission(EMPLOYMENT_DUTY)
  const canTerminate = hasPermission(EMPLOYMENT_TERMINATE)
  const canExtendProbation = hasPermission(EMPLOYMENT_PROBATION_EXTEND)
  const canCompleteProbation = hasPermission(EMPLOYMENT_PROBATION_COMPLETE)

  /*
    Oklad tugmasi IKKI ruxsat talab qiladi: o'zgartirish (`employment.salary`)
    va ko'rish (`salary.view`) — ko'ra olmaydigan odam oynada joriy okladni
    ko'rmay turib uni o'zgartirmasligi kerak (oyna uni oldindan to'ldiradi).
  */
  const canChangeSalary = canSeeSalary && hasPermission(EMPLOYMENT_SALARY)

  const hasAnyAction = canTransfer || canDuty || canChangeSalary || canTerminate

  return (
    <li
      className={cn(
        'rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-4',
        // Yopilgan munosabat pastroq darajada — lekin SHAFFOFLIK bilan emas:
        // qorong'i fonda shaffof matn o'qilmay qoladi
        closed && 'border-dashed',
      )}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="flex items-center gap-2 text-callout font-semibold">
            <Building2 className="size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
            {employment.legalEntity?.name ?? t('employees.noLegalEntity')}
          </p>

          <p className="mt-1.5 flex items-center gap-2 text-subhead">
            <UserRound className="size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
            {employment.position?.name ?? t('employees.noPosition')}
          </p>

          <p className="mt-1 flex items-center gap-2 text-footnote text-[var(--fg-secondary)]">
            <Network className="size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
            {employment.department?.name ?? '—'}
          </p>
        </div>

        <div className="flex shrink-0 flex-col items-end gap-1.5">
          <StatusBadge status={employment.status} size="sm" />
          {employment.isPrimary && (
            <Badge tone="brand" size="sm">
              {t('employees.primaryJob')}
            </Badge>
          )}
          {employment.rate !== 1 && (
            <Badge tone="info" size="sm">
              {t('employees.rateValue', { rate: formatRate(employment.rate) })}
            </Badge>
          )}
        </div>
      </div>

      <dl className="mt-3.5 grid gap-x-6 gap-y-1.5 border-t border-[var(--border)] pt-3 sm:grid-cols-2">
        <MiniRow label={t('employee.hireDate')} value={date(employment.hireDate)} />

        {employment.hireOrderNo !== null && (
          <MiniRow label={t('employee.orderNo')} value={employment.hireOrderNo} />
        )}

        {employment.probationEndDate !== null && (
          <MiniRow
            label={t('employee.probationUntil')}
            value={date(employment.probationEndDate)}
            tone="warning"
          />
        )}

        {employment.terminationDate !== null && (
          <MiniRow
            label={t('employees.terminationDate')}
            value={date(employment.terminationDate)}
          />
        )}

        {employment.terminationReasonName !== null && (
          <MiniRow
            label={t('employees.terminationReason')}
            value={employment.terminationReasonName}
          />
        )}

        {/* ⚠️ Oklad faqat `salary.view` ruxsati bilan. Ruxsat yo'q bo'lsa
            qator umuman chizilmaydi — bo'sh joy ham ma'lumot bermasin */}
        {/*
          Faol vazifa bo'lsa oklad pastdagi «hisob» blokida — ikki marta
          chizilmaydi. Oklad KIRITILMAGAN bo'lsa qator yashirilmaydi, «kiritilmagan»
          deb aytiladi: aks holda HR okladni yo'qolgan deb o'ylardi (2026-09-27).
        */}
        {canSeeSalary && employment.duties.length === 0 && (
          <MiniRow
            label={t('employee.salary')}
            value={
              employment.salary === null
                ? t('employees.salaryNotSet')
                : money(employment.salary.amount, employment.salary.currency)
            }
            muted={employment.salary === null}
          />
        )}
      </dl>

      {employment.duties.length > 0 && (
        <PayBreakdown employment={employment} canSeeSalary={canSeeSalary} />
      )}

      {/*
        Sinov muddati bloki — oddiy amallardan YUQORIDA va ajratilgan.

        NEGA alohida: sinovdagi xodim uchun eng muhim savol «ko'chiraymi»
        emas, «sinovni nima qilamiz». Ilgari kartada shu haqda faqat MATN
        turardi (yuqoridagi «Sinov muddati tugashi» qatori) va HR javob
        berish uchun `/sinov` sahifasiga o'tishi kerak edi — u yerdan esa
        odamni qaytadan qidirib topish kerak bo'lardi.

        Blok faqat OCHIQ sinovda chiziladi: `probationId` yakunlanganida
        `null` bo'ladi va domen ham yakunlangan muddatni na yopadi, na
        uzaytiradi. Ruxsatlar `/sinov` sahifasidagi bilan aynan bir xil:
        ikki tugma — ikki ALOHIDA ruxsat (`employment.probation.extend` va
        `.complete`); blok ulardan kamida bittasi bo'lsa chiziladi.

        Uchala natija (doimiy xodim · o'tolmadi · o'zi ketdi) va xulosa
        matni bitta oynada tanlanadi — `ResolveDialog`. Uni bu yerda
        takrorlash o'rniga, karta o'sha oynani chaqiradi.
      */}
      {(canExtendProbation || canCompleteProbation) &&
        !closed &&
        onAction !== null &&
        employment.probationId !== null && (
        <div className="mt-3.5 flex flex-wrap items-center justify-between gap-2.5 rounded-md border border-ios-orange/35 bg-ios-orange/10 px-3 py-2.5">
          {/*
            Sana bu yerda TAKRORLANMAYDI — u yuqoridagi «Sinov muddati
            tugashi» qatorida turibdi va u ruxsatdan qat'i nazar hamma
            ko'radigan MA'LUMOT. Bu blok esa amal haqida; bir xil sanani
            ikki marta yozish kartani shovqinli qilardi.
          */}
          <p className="min-w-0 text-footnote font-medium text-ios-orange">
            {t('probation.title')}
          </p>

          <span className="flex shrink-0 flex-wrap gap-1.5">
            {canExtendProbation && (
              <Button
                type="button"
                size="sm"
                variant="secondary"
                onClick={() => onAction('probationExtend', employment)}
              >
                <CalendarClock />
                {t('probation.extend')}
              </Button>
            )}

            {canCompleteProbation && (
              <Button
                type="button"
                size="sm"
                variant="primary"
                onClick={() => onAction('probationResolve', employment)}
              >
                <BadgeCheck />
                {t('probation.resolve')}
              </Button>
            )}
          </span>
        </div>
      )}

      {/*
        ⚠️ Yarim qolgan ketish.

        Sinovni yakunlash mehnat munosabatini TUGATMAYDI va bu ataylab:
        bo'shatish alohida buyruq bilan rasmiylashtiriladi (raqam, sana,
        sabab) va uni sinov oynasidan taxmin qilib yozib bo'lmaydi. Lekin
        natijasi «o'tolmadi» yoki «o'zi ketdi» bo'lgan sinovdan keyin
        munosabat OCHIQ qolsa, odam `/sinov` ro'yxatidan chiqib ketadi-yu,
        xodimlar ro'yxatida «Sinovda» bo'lib turaverardi — ya'ni ketgan
        odam ishlayotganlar orasida sanaladi va hisobotga ham shunday
        tushadi.

        Ilgari bu holat HECH QAYERDA ko'rinmasdi. Endi karta uni ochiq
        aytadi va keyingi qadamni yonida beradi.
      */}
      {!closed && employment.probationEndedByLeaving && (
        <div className="mt-3.5 flex flex-wrap items-center justify-between gap-2.5 rounded-md border border-ios-red/35 bg-ios-red/10 px-3 py-2.5">
          <p className="min-w-0 text-footnote font-medium text-ios-red">
            {t('employees.probationLeftOpen')}
          </p>

          {canTerminate && onAction !== null && (
            <Button
              type="button"
              size="sm"
              variant="danger"
              className="shrink-0"
              onClick={() => onAction('terminate', employment)}
            >
              <LogOut />
              {t('employees.terminate')}
            </Button>
          )}
        </div>
      )}

      {/*
        Amallar faqat KUCHDAGI munosabatda ko'rinadi: yopilganini ko'chirib
        ham, unga vazifa berib ham bo'lmaydi — domen ularni rad etadi.
        Tugmani ko'rsatib, keyin xato chiqarish foydalanuvchini aldash
        bo'lardi.
      */}
      {hasAnyAction && !closed && onAction !== null && (
        <div className="mt-3.5 flex flex-wrap justify-end gap-1.5 border-t border-[var(--border)] pt-3">
          {canTransfer && (
            <Button type="button" size="sm" variant="ghost" onClick={() => onAction('transfer', employment)}>
              <ArrowRightLeft />
              {t('employees.transfer')}
            </Button>
          )}

          {canDuty && (
            <Button type="button" size="sm" variant="ghost" onClick={() => onAction('duty', employment)}>
              <ListPlus />
              {t('employees.duty')}
            </Button>
          )}

          {canChangeSalary && (
            <Button type="button" size="sm" variant="ghost" onClick={() => onAction('salary', employment)}>
              <Wallet />
              {t('employee.salary')}
            </Button>
          )}

          {canTerminate && (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              onClick={() => onAction('terminate', employment)}
              className="text-ios-red hover:bg-ios-red/10 hover:text-ios-red"
            >
              <LogOut />
              {t('employees.terminate')}
            </Button>
          )}
        </div>
      )}
    </li>
  )
}

/** Yorliq + qiymat qatori. Bo'sh qiymat chiziqcha bilan ko'rsatiladi. */
function InfoRow({
  icon: Icon,
  label,
  value,
  mono = false,
  className,
}: {
  icon: LucideIcon
  label: string
  value: ReactNode
  /** Raqamli qiymatlar (JSHSHIR, pasport, telefon) ustma-ust tushishi uchun. */
  mono?: boolean
  className?: string
}) {
  const empty = value === null || value === undefined || value === ''

  return (
    <div
      className={cn(
        'flex items-start gap-3 border-b border-[var(--border)] py-3 last:border-0',
        className,
      )}
    >
      <Icon className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />

      <div className="min-w-0 flex-1">
        <dt className="text-caption1 text-[var(--fg-secondary)]">{label}</dt>
        <dd
          className={cn(
            'mt-0.5 text-subhead break-words',
            mono && 'tabular',
            empty ? 'text-[var(--fg-tertiary)]' : 'text-[var(--fg)]',
          )}
        >
          {empty ? '—' : value}
        </dd>
      </div>
    </div>
  )
}

/**
 * Oklad + faol qo'shimcha vazifalar + jami — kartaning butun kengligida.
 *
 * Egasining qarori (2026-09-27): qo'shimcha haq okladning USTIGA yozilmaydi,
 * unga QO'SHILIB ko'rsatiladi. Ikki ustunli jadvalda vazifa nomi va summa
 * sig'masdi va qatorlar sinib ketardi — shu sababli bu alohida, kvitansiya
 * kabi blok: chapda nom, o'ngda summa, pastda chiziq va jami.
 *
 * Vazifaning o'zi hammaga ko'rinadi; haqi va jami — faqat
 * `salary.extra.view` bilan (server ularni `null` qaytaradi).
 */
function PayBreakdown({
  employment,
  canSeeSalary,
}: {
  employment: EmploymentSummary
  canSeeSalary: boolean
}) {
  const { t, money } = useLocale()

  return (
    <div className="mt-3 space-y-1.5 rounded-md border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2.5">
      {/* Oklad kiritilmagan bo'lsa ham qator bor — jami nimadan iboratligi aniq ko'rinsin */}
      {canSeeSalary && (
        <PayLine
          label={t('employee.salary')}
          value={
            employment.salary === null
              ? t('employees.salaryNotSet')
              : money(employment.salary.amount, employment.salary.currency)
          }
          muted={employment.salary === null}
        />
      )}

      {employment.duties.map((duty) => (
        <PayLine
          key={duty.eventId}
          label={duty.duty}
          hint={t('employees.activeDuty')}
          value={
            duty.extraPay === null
              ? null
              : `+${money(duty.extraPay.amount, duty.extraPay.currency)}`
          }
        />
      ))}

      {employment.totalPay !== null && (
        <div className="border-t border-[var(--border)] pt-1.5">
          <PayLine
            label={t('employees.total')}
            value={money(employment.totalPay.amount, employment.totalPay.currency)}
            strong
          />
        </div>
      )}
    </div>
  )
}

function PayLine({
  label,
  hint,
  value,
  strong = false,
  muted = false,
}: {
  label: string
  hint?: string
  value: string | null
  strong?: boolean
  /** Qiymat o'rnida izoh («kiritilmagan») — xira rangda. */
  muted?: boolean
}) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <div className="min-w-0">
        <p className={cn('truncate text-footnote', strong ? 'font-semibold' : 'text-[var(--fg)]')} title={label}>
          {label}
        </p>
        {hint !== undefined && <p className="text-caption2 text-[var(--fg-tertiary)]">{hint}</p>}
      </div>

      {value !== null && (
        <span
          className={cn(
            'tabular shrink-0 text-footnote whitespace-nowrap',
            strong ? 'font-semibold' : 'font-medium',
            muted && 'font-normal text-[var(--fg-tertiary)]',
          )}
        >
          {value}
        </span>
      )}
    </div>
  )
}

function MiniRow({
  label,
  value,
  tone,
  muted = false,
}: {
  label: string
  value: string
  tone?: 'warning'
  /** Qiymat o'rnida izoh («kiritilmagan») — xira rangda. */
  muted?: boolean
}) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-caption1 text-[var(--fg-secondary)]">{label}</dt>
      <dd
        className={cn(
          'tabular text-footnote font-medium',
          tone === 'warning' ? 'text-ios-orange' : 'text-[var(--fg)]',
          muted && 'font-normal text-[var(--fg-tertiary)]',
        )}
      >
        {value}
      </dd>
    </div>
  )
}

/**
 * Uch holatli tekshiruv: o'tgan / o'tmagan / ma'lum emas.
 *
 * «Ma'lum emas» ni «yo'q» ga aylantirib bo'lmaydi: Excel'da bu katak
 * ko'p yozuvda bo'sh qolgan va uni «tekshirilmagan» deb ko'rsatish
 * xodimga tuhmat bo'lardi.
 */
/**
 * Sudlanganlik javobi — uch holatli nishon.
 *
 * <b>«Noma'lum» ATAYLAB neytral, ogohlantirish emas.</b> U «bu odam
 * shubhali» degani emas, «hech kim tekshirmagan» degani — ya'ni bu HR
 * ning ish ro'yxatidagi band, xodimga qo'yilgan baho emas.
 *
 * Ilgari bu yerda uchta qator uchun umumiy <c>CheckRow</c> turardi
 * («o'tgan / o'tmagan / noma'lum»). Ikkita qator olib tashlangach u
 * yagona joyda qoldi va shu sababli o'z ma'nosiga ko'chirildi: bu yerda
 * savol «tekshiruvdan o'tdimi» emas, «sudlanganmi».
 */
function CriminalRecordBadge({ value }: { value: boolean | null }) {
  const { t } = useLocale()

  if (value === null) {
    return (
      <Badge tone="neutral" size="sm">
        <CircleSlash className="size-3" strokeWidth={2.5} />
        {t('employees.unknown')}
      </Badge>
    )
  }

  return value ? (
    <Badge tone="warning" size="sm">
      <TriangleAlert className="size-3.5" strokeWidth={2.5} />
      {t('employees.criminalRecordYes')}
    </Badge>
  ) : (
    <Badge tone="success" size="sm">
      <BadgeCheck className="size-3.5" strokeWidth={2.5} />
      {t('employees.criminalRecordNo')}
    </Badge>
  )
}
