import { CalendarDays, ChevronLeft, ChevronRight, DoorOpen } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { CATALOG_MANAGE_PERMISSION } from '@/features/catalog/lib/catalog-kinds'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  Card,
  DataTable,
  EmptyState,
  Pagination,
  SegmentedControl,
  Skeleton,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { AttendancePass, TimesheetCell, TimesheetRow } from '../api/attendance-api'
import { useAttendancePasses, useTimesheet } from '../api/attendance-queries'
import { deviceProviderLabel } from '../lib/attendance-manage-presentation'
import {
  DAY_STATUS_LOOK,
  METHOD_LABEL_KEY,
  WEEKDAY_LABEL_KEY,
  WEEKDAY_SHORT_KEY,
  ATTENDANCE_MANAGE,
  ATTENDANCE_REQUESTS_MANAGE,
  ATTENDANCE_VIEW,
  formatDuration,
  formatPassTime,
  isWeekend,
  isoWeekday,
  monthLabel,
} from '../lib/attendance-presentation'
import {
  currentMonth,
  monthRange,
  shiftMonth,
  type MonthValue,
} from '../lib/attendance-search'
import { AbsenceRequestsList } from './absence-requests-list'
import { AttendanceDayDialog } from './attendance-day-dialog'
import { MobileEvidenceLine } from './attendance-mobile-evidence'
import { EmployeeAppAccessTab } from './employee-app-access-tab'
import { EmployeeTeamLeadSection } from './employee-team-lead-section'
import { ScheduleRequestsList } from './schedule-requests-list'
import {
  AttendancePassPhotoDialog,
  PassDirectionBadge,
  PassSnapshot,
} from './attendance-pass-photo-dialog'

/** O'tishlar ro'yxatining sahifa hajmi — bir oyda odatda 40–60 qator. */
const PASSES_PAGE_SIZE = 50

/**
 * Xodim kartasidagi «Davomat» tabi (SPEC-panel §F).
 *
 * <b>Nega xodim kartasining ICHIDA, alohida ekran emas.</b> Bir odam —
 * bitta karta. Verifix'da bu ekran yagona profil, chunki u yerda HR
 * kartasi yo'q; bizda esa bor va ikkinchi profil «qaysi biri to'g'ri?»
 * degan savolni tug'dirardi. Rasm, F.I.Sh., lavozim, bo'lim va holat
 * nishoni kartaning SARLAVHASIDA allaqachon turibdi — bu yerda
 * takrorlanmaydi (`§5.1` qaroridan ikkinchi nusxa chiqmasin).
 *
 * <b>Ichkarida ikkita ko'rinish</b> — Verifix'dagi «Календарь / Отметки»
 * ning o'zi: oylik kalendar va shu oyning turniket jurnali. Ular bitta
 * yuqori tab ichida, chunki kartada allaqachon oltita tab bor va
 * yettinchisidan keyin qatorning o'zi ro'yxatga aylanardi. Uchinchisi —
 * so'rovlar (yo'qlik va jadval so'rovlari), to'rtinchisi — «Ilova» (xodim ilovasining login va
 * paroli; faqat `attendance.manage` ga ko'rinadi).
 *
 * Ma'lumot MAVJUD endpointlardan: `/timesheet?employeeId=` (kalendar va
 * oylik yakun) va `/passes?employeeId=` (jurnal). Yangi endpoint yo'q.
 */
export function EmployeeAttendanceTab({
  employeeId,
  month,
  onMonth,
}: {
  employeeId: string
  month: MonthValue
  onMonth: (value: MonthValue) => void
}) {
  const { t, locale } = useLocale()
  const { hasPermission } = useAuth()
  // O'tish oynasidagi «Xodim kartasi» tugmasi — `employees.view` bo'lmasa chizilmaydi
  const canOpenEmployee = hasPermission(EMPLOYEES_VIEW)
  // «Ilova» segmenti — faqat `attendance.manage` (server ham shu ruxsatni so'raydi)
  const canManageApp = hasPermission(ATTENDANCE_MANAGE)
  // «Bo'lim boshlig'i» bloki — server ham shu ikkalasini so'raydi (2026-10-31)
  const canAssignHead = hasPermission(CATALOG_MANAGE_PERMISSION) && hasPermission(ATTENDANCE_VIEW)
  // Kun oynasida «Belgi qo'shish» / «O'chirish» — server ham shu ruxsatni so'raydi (SPEC-qolda-belgi §B)
  const canManageMarks = hasPermission(ATTENDANCE_REQUESTS_MANAGE)

  /*
    Ichki ko'rinish — komponent holatida, manzilda EMAS (tab va oydan
    farqi). Sabab: manzilda saqlanadigan narsa «havola bilan bo'lishsa
    ma'noli bo'ladigan» narsa — qaysi xodim, qaysi oy. «Kalendarmi yoki
    jurnalmi» esa bir xil oyning ikki ko'rinishi: uni manzilga chiqarish
    xodim kartasining manzilini uchinchi maydon bilan cho'zardi.
  */
  const [view, setView] = useState<'calendar' | 'passes' | 'requests' | 'app'>('calendar')
  const [page, setPage] = useState(1)
  const [openPass, setOpenPass] = useState<AttendancePass | null>(null)
  // Ochiq kun oynasi — katakning SANASI; katakning o'zi har safar yangi tabeldan olinadi
  const [openDate, setOpenDate] = useState<string | null>(null)

  const range = monthRange(month)
  const now = currentMonth()
  const isCurrentMonth = month.year === now.year && month.month === now.month

  /*
    Tabel so'rovi IKKALA ko'rinishda ham kerak: oylik yakun (tepadagi
    qator) kalendar bilan bir manbadan o'qiladi, ya'ni «kalendarda uch
    kun qizil, yakunda ikkita» degan holat bo'lishi mumkin emas.
  */
  const timesheet = useTimesheet({
    year: month.year,
    month: month.month,
    employeeId,
    page: 1,
    pageSize: 1,
  })

  const passes = useAttendancePasses(
    {
      employeeId,
      from: range.from,
      to: range.to,
      page,
      pageSize: PASSES_PAGE_SIZE,
    },
    view === 'passes',
  )

  const row: TimesheetRow | null = timesheet.data?.items[0] ?? null
  // Belgi qo'shilgach tabel qayta so'raladi — oynadagi statistika ham yangi katakdan
  const openCell = openDate === null ? null : (row?.cells.find((cell) => cell.date === openDate) ?? null)

  const changeMonth = (value: MonthValue) => {
    onMonth(value)
    // Oy almashsa jurnalning sahifasi ham boshidan: 3-sahifada turgan
    // odam yangi oyning uchinchi sahifasini ko'rishi mantiqsiz.
    setPage(1)
  }

  return (
    <div className="space-y-4">
      <Card className="overflow-hidden">
        {/* ── Oy tanlagich va oylik yakun ── */}
        <div className="flex flex-wrap items-center gap-x-5 gap-y-3 border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
          <div className="flex items-center gap-1" role="group" aria-label={t('attendance.month')}>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              onClick={() => changeMonth(shiftMonth(month, -1))}
              aria-label={t('attendance.prevMonth')}
              title={t('attendance.prevMonth')}
            >
              <ChevronLeft />
            </Button>
            <span className="tabular min-w-[84px] text-center text-callout font-semibold">
              {monthLabel(month, locale)}
            </span>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              onClick={() => changeMonth(shiftMonth(month, 1))}
              aria-label={t('attendance.nextMonth')}
              title={t('attendance.nextMonth')}
            >
              <ChevronRight />
            </Button>
            {!isCurrentMonth && (
              <Button type="button" size="sm" variant="soft" pill onClick={() => changeMonth(now)}>
                {t('attendance.currentMonth')}
              </Button>
            )}
          </div>

          <MonthTotals row={row} loading={timesheet.isPending} />
        </div>

        <div className="px-4 py-3.5 sm:px-5">
          <SegmentedControl
            value={view}
            onChange={setView}
            aria-label={t('employees.tabs')}
            options={[
              { value: 'calendar', label: t('attendance.employeeCalendar') },
              { value: 'passes', label: t('attendance.passesTitle') },
              { value: 'requests', label: t('attendance.requests') },
              ...(canManageApp ? [{ value: 'app' as const, label: t('attendance.appTab') }] : []),
            ]}
          />
        </div>

        {timesheet.isError && view === 'calendar' && (
          <div className="px-4 pb-4 sm:px-5">
            <Alert tone="danger" title={t('employees.loadFailed')}>
              {t('dashboard.loadFailedHint')}
            </Alert>
          </div>
        )}

        {view === 'app' && canManageApp ? (
          /*
            Xodim ilovasi (PWA) — login, parol va «Telefondan belgilash».
            Oyga bog'liq emas (so'rovlar kabi); ilgari kalendar OSTIDA alohida
            karta edi, egasining qarori bilan (2026-09-27) o'z segmentida.
          */
          <div className="px-4 pb-4 sm:px-5">
            <EmployeeAppAccessTab employeeId={employeeId} canOpenEmployee={canOpenEmployee} />
            {canAssignHead && (
              <div className="mt-4">
                <EmployeeTeamLeadSection employeeId={employeeId} />
              </div>
            )}
          </div>
        ) : view === 'requests' ? (
          /*
            So'rovlar oyga BOG'LIQ EMAS: tepadagi oy tanlagichi kalendar
            va jurnal uchun, so'rovlar esa xodimning butun tarixi. Shu
            sababli ular kartaning ichida emas, ostida — o'z kartalari
            bilan (`AbsenceRequestsList` ikkita jadval chizadi).
          */
          <div className="space-y-6 px-4 pb-4 sm:px-5">
            <section className="space-y-3">
              <h3 className="text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
                {t('attendance.requestsTitle')}
              </h3>
              <AbsenceRequestsList employeeId={employeeId} />
            </section>

            {/*
              Jadval so'rovlari yo'qlik so'rovlari OSTIDA (SPEC-jadval-sorovi
              §C4): ikkalasida ham «Qaror kutmoqda» degan jadval bor, shu
              sababli har guruhning o'z sarlavhasi — qaysi biri qaysi ekani
              adashmasin.
            */}
            <section className="space-y-3">
              <h3 className="text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)] uppercase">
                {t('attendance.scheduleRequestsTitle')}
              </h3>
              <ScheduleRequestsList employeeId={employeeId} />
            </section>
          </div>
        ) : view === 'calendar' ? (
          timesheet.isPending ? (
            <CalendarSkeleton />
          ) : row === null ? (
            /*
              Bo'sh oy — xato emas: xodim bu oyda umuman ishlamagan
              (ishga keyin olingan yoki oldin ketgan). Tabel unga qator
              qaytarmaydi, ya'ni ko'rsatiladigan katak ham yo'q.
            */
            <div className="px-4 pb-5 sm:px-5">
              <EmptyState
                icon={CalendarDays}
                title={t('attendance.employeeMonthEmpty')}
                description={t('attendance.employeeMonthEmptyText')}
              />
            </div>
          ) : (
            <MonthCalendar cells={row.cells} onOpenDay={setOpenDate} />
          )
        ) : (
          <PassMonth
            passes={passes.data?.items ?? []}
            total={passes.data?.total ?? 0}
            page={page}
            loading={passes.isPending}
            failed={passes.isError}
            onPage={setPage}
            onOpen={setOpenPass}
          />
        )}
      </Card>

      <AttendanceDayDialog
        employeeId={employeeId}
        cell={openPass === null ? openCell : null}
        canManage={canManageMarks}
        onOpenPass={setOpenPass}
        onClose={() => setOpenDate(null)}
      />

      <AttendancePassPhotoDialog
        passId={openPass?.id ?? null}
        pass={openPass}
        canOpenEmployee={canOpenEmployee}
        onClose={() => setOpenPass(null)}
      />
    </div>
  )
}

/**
 * Oylik yakun — tepadagi bitta qator.
 *
 * Sonlar tabelning O'Z yakunidan (`TimesheetTotalsDto`), klientda
 * kataklardan qayta yig'ilmaydi: yig'indi ikki joyda hisoblansa, ular
 * ajralib ketishi uchun eshik ochilardi.
 */
function MonthTotals({ row, loading }: { row: TimesheetRow | null; loading: boolean }) {
  const { t, num } = useLocale()

  if (loading) {
    return <Skeleton className="h-5 w-64" />
  }

  if (row === null) {
    return null
  }

  const totals = row.totals
  const shortfall = totals.shortfallMinutes ?? 0
  /*
    «Kam ishlangan» faqat erkin kuni bor oyda: oddiy jadvaldagi 99 % xodimda u
    doim 0 va har kartada «0:00» bo'lib turishi shovqin bo'lardi.
  */
  const hasFlexible = shortfall > 0 || row.cells.some((cell) => cell.isFlexible === true)
  // Shaxsiy vaqt — faqat bo'lsa (SPEC-sorov-tahriri §5): aks holda har kartada «0:00» shovqini
  const personal = totals.personalMinutes ?? 0

  return (
    <dl className="flex flex-wrap items-center gap-x-5 gap-y-1.5 text-footnote">
      <Total
        label={t('attendance.worked')}
        value={`${formatDuration(totals.workedMinutes)} / ${formatDuration(totals.plannedMinutes)}`}
      />
      <Total label={t('attendance.presentDays')} value={num(totals.presentDays)} />
      <Total
        label={t('attendance.absentDays')}
        value={num(totals.absentDays)}
        tone={totals.absentDays > 0 ? 'danger' : undefined}
      />
      <Total
        label={t('attendance.lateMinutes')}
        value={formatDuration(totals.lateMinutes)}
        tone={totals.lateMinutes > 0 ? 'warning' : undefined}
      />
      {hasFlexible && (
        <Total
          label={t('attendance.shortfallMinutes')}
          value={formatDuration(shortfall)}
          tone={shortfall > 0 ? 'warning' : undefined}
        />
      )}
      {personal > 0 && (
        <Total label={t('attendance.personalMinutes')} value={formatDuration(personal)} />
      )}
    </dl>
  )
}

function Total({
  label,
  value,
  tone,
}: {
  label: string
  value: string
  tone?: 'danger' | 'warning'
}) {
  return (
    <div className="flex items-baseline gap-1.5">
      <dt className="text-[var(--fg-secondary)]">{label}</dt>
      <dd
        className={cn(
          'tabular font-semibold',
          tone === 'danger' && 'text-ios-red',
          tone === 'warning' && 'text-ios-orange',
        )}
      >
        {value}
      </dd>
    </div>
  )
}

/**
 * Oylik kalendar — dushanbadan boshlanadigan yetti ustun.
 *
 * <b>Rang — holatning O'ZI</b> va u tabel gridi bilan BITTA xaritadan
 * (`DAY_STATUS_LOOK`): bir xil kun ikki ekranda ikki xil rangda
 * ko'rinmasligi kerak. Kechikish, erta ketish va ortiqcha ish esa rang
 * emas, alohida punktir lavhalar (Verifix uslubi, 2026-11-03): «kechikdi,
 * lekin keldi» ikki xabar va ular bir-birini bosmasligi kerak.
 *
 * Katak ichida: REJA (ko'k lavha — jadval nima degan), HAQIQIY (yashil
 * lavha — turniket nima degan) va chetlanishlar. Reja kun yozuvidagi
 * SURATDAN keladi, bugungi jadvaldan qayta hisoblanmaydi — o'tgan oyning
 * kalendari jadval o'zgargani uchun o'zgarib qolmaydi.
 */
function MonthCalendar({
  cells,
  onOpenDay,
}: {
  cells: readonly TimesheetCell[]
  /** Katak bosilganda — kun oynasi (SPEC-qolda-belgi §C). */
  onOpenDay: (date: string) => void
}) {
  const { t } = useLocale()

  const first = cells[0]
  if (first === undefined) return null

  // Oyning birinchi kuni haftaning nechanchi kuni — shuncha bo'sh katak.
  const lead = (isoWeekday(first.date) ?? 1) - 1
  const today = todayIso()

  return (
    <div className="overflow-x-auto px-4 pb-5 sm:px-5">
      <div className="grid min-w-[660px] grid-cols-7 gap-1.5">
        {([1, 2, 3, 4, 5, 6, 7] as const).map((day) => (
          <div
            key={day}
            title={t(WEEKDAY_LABEL_KEY[day])}
            className={cn(
              'pb-1 text-center text-caption1 font-medium',
              isWeekend(day) ? 'text-ios-red/70' : 'text-[var(--fg-secondary)]',
            )}
          >
            {t(WEEKDAY_SHORT_KEY[day])}
          </div>
        ))}

        {Array.from({ length: lead }, (_, index) => (
          <div key={`bosh-${index}`} aria-hidden />
        ))}

        {cells.map((cell) => (
          <DayCell key={cell.date} cell={cell} today={today} onOpen={() => onOpenDay(cell.date)} />
        ))}
      </div>
    </div>
  )
}

function DayCell({ cell, today, onOpen }: { cell: TimesheetCell; today: string; onOpen: () => void }) {
  const { t, num } = useLocale()

  /*
    Chetlanish SO'Z bilan yoziladi: «22 daq», «1 soat 5 daq».
    Ilgari bu yerda `22′` turardi — shtrix belgisi ekranda tutuq
    belgisiga o'xshab ketdi va o'qiyotgan odam uni sonning bir qismi
    deb o'yladi. Soatga o'tish 60 daqiqadan boshlanadi: «75 daq» ni
    odam boshida bo'lardi.
  */
  const minutes = (value: number): string => {
    const hours = Math.floor(value / 60)
    const rest = value % 60

    if (hours === 0) {
      return `${num(value)} ${t('attendance.minutesShort')}`
    }

    const head = `${num(hours)} ${t('attendance.hoursShort')}`
    return rest === 0 ? head : `${head} ${num(rest)} ${t('attendance.minutesShort')}`
  }

  const look = DAY_STATUS_LOOK[cell.status]
  const isToday = cell.date === today
  const dayNumber = Number(cell.date.slice(8))

  // Erkin kun (SPEC-erkin-jadval §7.1): reja — norma va oraliq, chetlanish — kam ishlangan
  const flexible = cell.isFlexible ?? false
  const shortfall = cell.shortfallMinutes ?? 0

  const plan =
    cell.planStartAt === null
      ? null
      : flexible
        ? t('attendance.planFlexibleLine', {
            hours: formatDuration(cell.plannedMinutes ?? 0),
            range: `${formatPassTime(cell.planStartAt)}–${formatPassTime(cell.planEndAt)}`,
          })
        : `${formatPassTime(cell.planStartAt)} – ${formatPassTime(cell.planEndAt)}`

  /*
    Kirish bor-u chiqish yo'q kun «08:50 – —» bo'lib chiqadi: chiziqcha
    ATAYLAB qoldiriladi, chunki holatning o'zi (`MissingExit`) allaqachon
    katak rangida turibdi va ikkinchi marta so'z bilan takrorlash
    kalendarni shovqinga aylantirardi.
  */
  const actual =
    cell.firstInAt === null && cell.lastOutAt === null
      ? null
      : `${formatPassTime(cell.firstInAt)} – ${formatPassTime(cell.lastOutAt)}${
          cell.autoExit ? ` ${t('attendance.autoExit')}` : ''
        }`

  const overtime = cell.overtimeMinutes ?? 0

  /*
    Verifix uslubi (egasi, 2026-11-03, «Календарь» skrinshotlari): REJA — to'q ko'k lavha,
    HAQIQIY — yashil lavha, chetlanishlar — punktir chegarali kichik lavhalar (kechikish va
    erta ketish qizil, ortiqcha ish yashil, erkin kunda kam ishlangan to'q sariq). Katak —
    TUGMA: bosilganda shu kunning barcha belgilari ochiladi (SPEC-qolda-belgi §C).
  */
  return (
    <button
      type="button"
      onClick={onOpen}
      aria-label={`${dayNumber} — ${t('attendance.dayDialog')}`}
      className={cn(
        /*
          Chegara HAR BIR katakda, holatidan qat'i nazar: bo'sh kun ham o'z joyiga ega
          bo'lishi kerak (ilgari «ishlagan» kunlar oq fonda katak bo'lib ko'rinmasdi).
        */
        'min-h-[92px] w-full cursor-pointer rounded-md border border-[var(--border)] p-1.5 text-left transition-shadow hover:shadow-[0_0_0_2px_var(--accent)] focus-visible:shadow-[0_0_0_2px_var(--accent)] focus-visible:outline-none',
        look.cellClass,
        isToday && 'ring-2 ring-[var(--accent)] ring-inset',
      )}
    >
      <div className="flex items-center justify-between gap-1 px-0.5">
        <span className="tabular text-caption1 font-semibold opacity-70">{dayNumber}</span>

        {/* Holat NOMI faqat vaqt bo'lmagan kunda — rang baribir holatni aytib turadi */}
        {actual === null && (
          <span className="truncate text-caption2 opacity-70">{t(look.labelKey)}</span>
        )}
      </div>

      {plan !== null && (
        <span className="tabular mt-1 block truncate rounded-sm bg-ios-blue px-1.5 py-0.5 text-center text-caption2 font-medium text-white">
          {plan}
        </span>
      )}

      {actual !== null && (
        <span className="tabular mt-1 block truncate rounded-sm bg-ios-green px-1.5 py-0.5 text-center text-caption2 font-semibold text-white">
          {actual}
        </span>
      )}

      {/* Tasdiqlangan «ish vaqti» so'rovidan — o'tishsiz kun ham «Ishladi» bo'lishi mumkin */}
      {(cell.creditedMinutes ?? 0) > 0 && (
        <span className="mt-1 block px-1 text-caption2 leading-tight text-ios-blue">
          {t('attendance.creditedShort')}{' '}
          <span className="tabular font-semibold">{minutes(cell.creditedMinutes ?? 0)}</span>
        </span>
      )}

      {/* Shaxsiy so'rov soatlari — sababli, lekin ishlangan vaqtga kirmaydi: neytral */}
      {(cell.personalMinutes ?? 0) > 0 && (
        <span className="mt-1 block px-1 text-caption2 leading-tight text-[var(--fg-secondary)]">
          {t('attendance.personalShort')}{' '}
          <span className="tabular font-semibold">{formatDuration(cell.personalMinutes ?? 0)}</span>
        </span>
      )}

      {/* Chetlanishlar ALOHIDA: kelish va ketish — ikki boshqa suhbat */}
      {flexible ? (
        shortfall > 0 && <DeviationChip tone="warning">− {minutes(shortfall)}</DeviationChip>
      ) : (
        <>
          {cell.lateMinutes > 0 && (
            <DeviationChip tone="danger" title={t('attendance.lateMinutes')}>
              − {minutes(cell.lateMinutes)}
            </DeviationChip>
          )}
          {cell.earlyLeaveMinutes > 0 && (
            <DeviationChip tone="danger" title={t('attendance.earlyLeaveMinutes')}>
              − {minutes(cell.earlyLeaveMinutes)}
            </DeviationChip>
          )}
        </>
      )}
      {overtime > 0 && (
        <DeviationChip tone="success" title={t('attendance.overtimeMinutes')}>
          + {minutes(overtime)}
        </DeviationChip>
      )}
    </button>
  )
}

/** Chetlanish lavhasi — punktir chegara (Verifix'dagi «- 2 ч 32 мин» kabi). */
function DeviationChip({
  tone,
  title,
  children,
}: {
  tone: 'danger' | 'success' | 'warning'
  title?: string
  children: ReactNode
}) {
  return (
    <span
      title={title}
      className={cn(
        'tabular mt-1 block truncate rounded-sm border border-dashed px-1.5 py-0.5 text-center text-caption2 font-medium',
        tone === 'danger' && 'border-ios-red text-ios-red',
        tone === 'success' && 'border-ios-green text-ios-green',
        tone === 'warning' && 'border-ios-orange text-ios-orange',
      )}
    >
      {children}
    </span>
  )
}

function CalendarSkeleton() {
  return (
    <div aria-hidden className="px-4 pb-5 sm:px-5">
      <div className="grid min-w-[660px] grid-cols-7 gap-1.5">
        {Array.from({ length: 35 }, (_, index) => (
          <Skeleton key={index} className="h-[92px] rounded-md" />
        ))}
      </div>
    </div>
  )
}

/** Jadval modeli modul darajasida — har render'da yangi obyekt uni qayta qurardi. */
const passColumn = dataColumnHelper<AttendancePass>()

/** Bo'sh massiv ham modul darajasida (`attendance-passes-page.tsx` dagi qoida). */
const NO_PASSES: AttendancePass[] = []

/**
 * Shu oyning turniket jurnali — xodim bo'yicha, JADVAL ko'rinishida.
 *
 * <b>Nega jadval, ro'yxat emas</b> (egasi, 2026-09-19, Verifix'ning
 * «Отметки» ekraniga qarab): bu yerda odam qatorlarni bir-biriga
 * SOLISHTIRADI — «17-sentabrda qaysi qurilmadan o'tgan», «yuz bilanmi
 * yoki karta bilan». Ustunlar tikka turganda ko'z pastga yuguradi;
 * kartochka shaklidagi ro'yxatda esa har qator qayta o'qiladi.
 *
 * Ustunlar Verifix'dagi bilan bir xil savollarga javob beradi: vaqt,
 * joy, qurilma turi, belgi turi va identifikatsiya usuli. Farqi —
 * eng chapdagi TURNIKET SURATI: u bizda bor va «bu haqiqatan shu
 * odammi» degan savolga javob beradigan yagona ustun.
 *
 * Qator bosilganda surat oynasi ochiladi; oyna jurnal sahifasidagi bilan
 * AYNAN bitta komponent (`AttendancePassPhotoDialog`).
 */
function PassMonth({
  passes,
  total,
  page,
  loading,
  failed,
  onPage,
  onOpen,
}: {
  passes: AttendancePass[]
  total: number
  page: number
  loading: boolean
  failed: boolean
  onPage: (page: number) => void
  onOpen: (pass: AttendancePass) => void
}) {
  const { t, date } = useLocale()

  const columns = useMemo<Array<DataColumn<AttendancePass>>>(
    () => [
      /*
        ⭐ BIR QATOR (egasi 2026-10-22): surat 40 px, qurilma, joy va telefon
        belgisi bitta chiziqda (ilgari qurilma ostida yana ikki qator edi).
      */
      passColumn.display({
        id: 'snapshot',
        header: '',
        // 40 px surat + katak to'ldirishi (28 px) ≈ 68 px.
        meta: { width: '68px' },
        cell: ({ row }) => <PassSnapshot pass={row.original} className="size-10" />,
      }),

      passColumn.accessor('occurredAt', {
        header: t('attendance.time'),
        enableSorting: false,
        cell: ({ getValue }) => (
          <span className="tabular block font-semibold">
            {date(getValue())} {formatPassTime(getValue())}
          </span>
        ),
      }),

      passColumn.accessor('deviceName', {
        header: t('attendance.device'),
        enableSorting: false,
        cell: ({ row }) => (
          <span className="flex items-center gap-2">
            <span>{row.original.deviceName ?? '—'}</span>
            {/*
              Joy — qurilmaning erkin matnli izohi, ya'ni u BO'LMASLIGI
              odatiy hol. Bo'lmaganda chiziqcha chizilmaydi: nom yonida
              hech narsa qolmaydi va jadval tinch turadi.
            */}
            {row.original.deviceLocation !== null && (
              <span
                className="block max-w-[240px] truncate text-caption1 text-[var(--fg-tertiary)]"
                title={row.original.deviceLocation}
              >
                {row.original.deviceLocation}
              </span>
            )}
            <MobileEvidenceLine pass={row.original} className="mt-0" />
          </span>
        ),
      }),

      passColumn.accessor('deviceProvider', {
        header: t('attendance.provider'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ getValue }) => {
          const provider = getValue()

          return (
            <span className="text-[var(--fg-secondary)]">
              {provider === null ? '—' : deviceProviderLabel(provider, t)}
            </span>
          )
        },
      }),

      passColumn.accessor('direction', {
        header: t('attendance.direction'),
        enableSorting: false,
        cell: ({ row }) => <PassDirectionBadge
            direction={row.original.direction}
            inferred={row.original.directionSource === 'Alternation'}
          />,
      }),

      passColumn.accessor('method', {
        header: t('attendance.method'),
        enableSorting: false,
        meta: { hideOnMobile: true },
        cell: ({ row }) => (
          <span className="text-[var(--fg-secondary)]">
            {t(METHOD_LABEL_KEY[row.original.method])}
          </span>
        ),
      }),
    ],
    [t, date],
  )

  if (failed) {
    return (
      <div className="px-4 pb-5 sm:px-5">
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {t('dashboard.loadFailedHint')}
        </Alert>
      </div>
    )
  }

  return (
    <div className="pb-4">
      <DataTable
        columns={columns}
        data={loading ? NO_PASSES : passes}
        getRowId={(pass) => pass.id}
        loading={loading}
        skeletonRows={6}
        onRowClick={onOpen}
        rowLabel={(pass) => `${date(pass.occurredAt)} ${formatPassTime(pass.occurredAt)}`}
        empty={
          <EmptyState
            icon={DoorOpen}
            title={t('attendance.employeePassesEmpty')}
            description={t('attendance.employeePassesEmptyText')}
          />
        }
      />

      <Pagination
        page={page}
        pageSize={PASSES_PAGE_SIZE}
        total={total}
        onPageChange={onPage}
        className="mt-3 px-4 sm:px-5"
      />
    </div>
  )
}
