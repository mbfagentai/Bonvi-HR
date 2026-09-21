import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { Link } from '@tanstack/react-router'
import { Coffee } from 'lucide-react'
import { useSyncExternalStore, type ReactNode } from 'react'

import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import { Badge, Skeleton } from '@/shared/ui'

import type {
  AttendanceDayStatus,
  TimesheetCell,
  TimesheetRow,
  TimesheetTotals,
} from '../api/attendance-api'
import {
  DAY_STATUS_LEGEND,
  DAY_STATUS_LOOK,
  LATE_BAR_CLASS,
  WEEKDAY_LABEL_KEY,
  formatDuration,
  formatHours,
  formatPassTime,
  isWeekend,
  isoWeekday,
} from '../lib/attendance-presentation'

/*
 * ⭐ Tabel — tizimdagi YAGONA zich narsa (SPEC §8.6). Atrofidagi hamma
 * narsa (sarlavha, filtrlar, sahifa chetlari) odatdagi nafasini saqlaydi;
 * zichlik faqat shu faylda.
 *
 * Qoidalar va nima uchun:
 *   · katak 36×36, `--radius-xs`, 2 px oraliq — chiziq YO'Q, ajratish
 *     oraliq va yumshoq fon bilan (tizimning umumiy qoidasi);
 *   · katakda BITTA raqam — soat, bitta kasr bilan; qolgan hamma narsa
 *     bosilganda ochiladigan oynachada (iOS: bosqichma-bosqich ochish);
 *   · holat — 12 % li TO'LDIRISH, kechikish — pastki qirradagi 3 px sariq
 *     chiziq (`DAY_STATUS_LOOK`, `LATE_BAR_CLASS`);
 *   · xodim va jami ustunlari QOTIRILGAN, kunlar esa suriladi.
 */

/**
 * Ish kuni bo'lmagan katakdagi qisqa HOLAT KODI tarjima kalitlari.
 *
 * NEGA kod ham kerak (SPEC «katakda bitta raqam» degan bo'lsa ham): raqam
 * faqat ishlangan kunda bor. «Kelmadi», «dam olish», «jadval yo'q»
 * kataklarida raqam 0 va u hech narsa aytmaydi — egasi Verifix'dagi kabi
 * qisqa harf kodini so'radi, legenda esa kodni rang bilan birga
 * tushuntiradi. Ishlangan kunda baribir SOAT turadi.
 *
 * `NotEmployed` — kodi YO'Q: odam bu sanada umuman ishlamagan va katak
 * ATAYLAB bo'sh (§8.6). `Planned` ham shunday: kun hali kelmagan va
 * aytadigan gap yo'q — oyning qolgan yarmi harflar bilan to'lmasligi
 * kerak.
 */
/** Legendadagi «ishlangan kun» namunasi — 8 soat, katakda «8,0» bo'lib chiqadi. */
const SAMPLE_WORKED_MINUTES = 480

const STATUS_CODE_KEY: Readonly<Record<Exclude<AttendanceDayStatus, 'NotEmployed' | 'Planned'>, string>> = {
  Present: 'attendance.codePresent',
  Absent: 'attendance.codeAbsent',
  DayOff: 'attendance.codeDayOff',
  DayOffWorked: 'attendance.codeDayOffWorked',
  MissingExit: 'attendance.codeMissingExit',
  MissingEntry: 'attendance.codeMissingEntry',
  NoSchedule: 'attendance.codeNoSchedule',
  Excused: 'attendance.codeExcused',
}

/**
 * `md` (48rem) — shundan torroq ekranda grid grid bo'lishga URINMAYDI
 * (SPEC §8.6): har bir xodim alohida kartada, kunlari gorizontal lentada.
 */
const DESKTOP_QUERY = '(min-width: 48rem)'

function subscribeDesktop(onChange: () => void): () => void {
  const media = window.matchMedia(DESKTOP_QUERY)
  media.addEventListener('change', onChange)
  return () => media.removeEventListener('change', onChange)
}

/**
 * Ekran keng ekanmi — CSS `hidden` bilan emas, SHARTLI RENDER bilan.
 *
 * NEGA: har bir katak o'z oynachasini (Radix menyusi) olib yuradi — 25
 * qator × 31 kun = 775 ta. Ikkala ko'rinishni ham chizib, bittasini CSS
 * bilan yashirish ularni ikki barobar ko'paytirardi.
 */
function useIsDesktop(): boolean {
  return useSyncExternalStore(
    subscribeDesktop,
    () => window.matchMedia(DESKTOP_QUERY).matches,
    () => true,
  )
}

/* ═════════════════════════════════════════════════════════════ grid */

/**
 * Tabel panjarasi — xodim × kun.
 *
 * Kataklar serverdan OYNING HAR BIR KUNI uchun keladi (yozuvi yo'q kunlar
 * serverda hosil qilinadi, §5.7.1) — bu yerda bo'shliq to'ldirilmaydi va
 * holat taxmin qilinmaydi.
 */
export function AttendanceTimesheetGrid({
  days,
  rows,
  loading,
}: {
  days: readonly string[]
  rows: readonly TimesheetRow[]
  loading: boolean
}) {
  const desktop = useIsDesktop()

  if (loading) return <GridSkeleton />

  return desktop ? <DesktopGrid days={days} rows={rows} /> : <MobileList rows={rows} />
}

/**
 * Keng ekran — bitta suriladigan quti ichida jadval.
 *
 * ⚠️ QOTIRILGAN USTUNLAR — `position: sticky` KATAKLARNING O'ZIDA va ular
 * shu quti ICHIDA. Ilova qobig'i `h-dvh overflow-hidden` va sticky
 * element eng yaqin SURILADIGAN ajdodiga yopishadi (`app-shell.tsx`
 * izohi): sticky ajdodga qo'yilsa yoki kataklar bilan quti orasida
 * `overflow`/`transform` li oraliq element paydo bo'lsa, ustun jimgina
 * kunlar bilan birga surilib ketadi — loyiha buni bir marta boshidan
 * kechirgan (SPEC §8.6).
 *
 * Quti balandligi cheklangan (`max-h-[70dvh]`): shunda sarlavha qatori
 * ham shu qutida qotadi va 25-qatorda ham kun raqami ko'rinib turadi.
 * Ikki o'qning ikkalasi ham bitta qutida suriladi, ya'ni burchak katagi
 * uchala qotirishni birga ushlaydi.
 *
 * Oraliq `border-spacing` bilan emas, har bir katakning 1 px ichki
 * chekkasi bilan (`p-px` → qo'shnilar orasida 2 px): oraliq katakning
 * O'Z yuzasi bo'lsa, qotirilgan ustunning oq foni uni to'liq yopadi va
 * ostidan surilayotgan kataklar 2 px tirqishdan ko'rinib qolmaydi.
 */
function DesktopGrid({ days, rows }: { days: readonly string[]; rows: readonly TimesheetRow[] }) {
  const { t } = useLocale()
  const today = todayIso()

  return (
    /*
      ⭐ Konteyner so'rovi (`@container/tabel`), media so'rovi EMAS: yon
      menyu yig'ilganda ±188 px o'zgaradi va oyna kengligi jadvalga
      qancha joy qolganini bilmaydi (statistika sahifasida ham shu
      sabab bilan shunday qilingan).
    */
    <div className="@container/tabel max-h-[70dvh] overflow-auto overscroll-contain">
      {/*
        `w-full` — o'ng tomonda bo'sh maydon qolmasligi uchun. Kataklar
        katta ekranda O'ZI ham kattalashadi (`DayCell`), ya'ni ortiqcha
        kenglik avval jadvalning o'ziga ketadi; undan keyin qolgani
        XODIM ustuniga tushadi (pastdagi `w-full`) va ismlar kesilmay
        to'liq ko'rinadi.
      */}
      <table className="w-full border-separate border-spacing-0">
        <thead>
          <tr>
            <th
              scope="col"
              className={cn(
                // `w-full` + `min-w` — jadval kengaysa ortiqcha joy SHU
                // ustunga tushadi, torayganda esa 220 px dan pastga
                // tushmaydi (ism va lavozim o'qilishi kerak).
                'sticky top-0 left-0 z-30 w-full min-w-[220px] bg-[var(--bg-elevated)] pt-3 pr-3 pb-1.5 pl-4 text-left sm:pl-5',
                'text-caption2 font-semibold tracking-wider whitespace-nowrap text-[var(--fg-secondary)] uppercase',
              )}
            >
              {t('attendance.employee')}
            </th>

            {days.map((day) => (
              <th
                key={day}
                scope="col"
                className="sticky top-0 z-20 bg-[var(--bg-elevated)] px-px pt-3 pb-1.5"
              >
                <DayNumber date={day} today={today} />
              </th>
            ))}

            <th
              scope="col"
              className={cn(
                'sticky top-0 right-0 z-30 bg-[var(--bg-elevated)] pt-3 pr-4 pb-1.5 pl-3 text-right sm:pr-5',
                'text-caption2 font-semibold tracking-wider whitespace-nowrap text-[var(--fg-secondary)] uppercase',
              )}
            >
              {t('attendance.totalWorked')}
            </th>
          </tr>
        </thead>

        <tbody>
          {rows.map((row) => (
            <tr key={row.employeeId}>
              <th
                scope="row"
                className="sticky left-0 z-10 w-full min-w-[220px] bg-[var(--bg-elevated)] py-px pr-3 pl-4 text-left font-normal sm:pl-5"
              >
                <EmployeeCell row={row} wide />
              </th>

              {row.cells.map((cell) => (
                <td key={cell.date} className="p-px">
                  <DayCell cell={cell} employeeName={row.fullName} />
                </td>
              ))}

              <td className="sticky right-0 z-10 bg-[var(--bg-elevated)] py-px pr-4 pl-3 sm:pr-5">
                <TotalsCell row={row} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/**
 * Tor ekran — bitta xodim, bitta karta, kunlar gorizontal lentada.
 *
 * Buni telefonda hech kim ishlatmaydi, lekin rahbarning planshetida buzilib
 * ko'ringan jadval yomon taassurot qoldiradi va bu bor-yo'g'i bitta
 * chegara (SPEC §8.6).
 */
function MobileList({ rows }: { rows: readonly TimesheetRow[] }) {
  const today = todayIso()

  return (
    <ul className="divide-y divide-[var(--border)]">
      {rows.map((row) => (
        <li key={row.employeeId} className="space-y-3 px-4 py-4">
          <div className="flex items-center gap-3">
            <div className="min-w-0 flex-1">
              <EmployeeCell row={row} wide />
            </div>
            <TotalsCell row={row} />
          </div>

          <div className="overflow-x-auto overscroll-x-contain pb-1">
            <div className="flex w-max gap-0.5">
              {row.cells.map((cell) => (
                <div key={cell.date} className="flex flex-col items-center gap-1">
                  <DayNumber date={cell.date} today={today} />
                  <DayCell cell={cell} employeeName={row.fullName} />
                </div>
              ))}
            </div>
          </div>
        </li>
      ))}
    </ul>
  )
}

function GridSkeleton() {
  return (
    <div className="space-y-2 px-4 py-4 sm:px-5">
      {Array.from({ length: 8 }, (_, index) => (
        <div key={`skeleton-${index}`} className="flex items-center gap-3">
          <Skeleton className="size-8 shrink-0 rounded-full" />
          <Skeleton className="h-4 w-40 shrink-0" />
          <Skeleton className="h-9 flex-1" />
        </div>
      ))}
    </div>
  )
}

/* ═════════════════════════════════════════════════════════ bo'laklar */

/**
 * Kun raqami — ustun sarlavhasi.
 *
 * Dam olish kunlari KANVAS rangida (SPEC §8.6): hafta maromi chiziqlarsiz
 * ko'rinadi. Bugungi kun tint matnida — «qayerdamiz» degan savolga javob,
 * bezak emas.
 */
function DayNumber({ date, today }: { date: string; today: string }) {
  const { t } = useLocale()
  const weekday = isoWeekday(date)
  const weekend = weekday !== null && isWeekend(weekday)
  const isToday = date === today

  return (
    <span
      title={weekday === null ? undefined : t(WEEKDAY_LABEL_KEY[weekday])}
      className={cn(
        'tabular grid h-6 place-items-center rounded-xs',
        // Kenglik katak bilan BIR XIL bo'lishi shart: aks holda raqam
        // ustuniga to'g'ri kelmay, grid qiyshiq ko'rinardi.
        'w-9 text-caption2',
        '@min-[1500px]/tabel:w-10 @min-[1750px]/tabel:w-11 @min-[1750px]/tabel:text-caption1',
        weekend ? 'bg-[var(--bg)] text-[var(--fg-secondary)]' : 'text-[var(--fg-tertiary)]',
        isToday && 'font-bold text-[var(--accent-text)]',
      )}
    >
      {Number(date.slice(8, 10))}
    </span>
  )
}

function EmployeeCell({ row, wide = false }: { row: TimesheetRow; wide?: boolean }) {
  const detail = row.position ?? row.department

  /*
    Karta EKRANDAGI oy bilan ochiladi: avgust tabelida ismni bosgan HR
    avgust kalendarini ko'rishi kerak, joriy oyni emas. Oy qatorning O'Z
    kataklaridan olinadi — grid'ga qo'shimcha parametr uzatilmaydi va
    telefon ko'rinishi ham (u `days` ni olmaydi) bir xil ishlaydi.
  */
  const first = row.cells[0]?.date

  return (
    <Link
      to="/xodimlar/$employeeId"
      params={{ employeeId: row.employeeId }}
      search={
        first === undefined
          ? { tab: 'attendance' as const }
          : {
              tab: 'attendance' as const,
              year: Number(first.slice(0, 4)),
              month: Number(first.slice(5, 7)),
            }
      }
      className={cn('flex items-center gap-2.5 rounded-sm', wide ? 'min-w-0' : 'w-[220px]')}
    >
      <EmployeePhoto
        employeeId={row.employeeId}
        photoKey={row.photoKey}
        name={row.fullName}
        size={wide ? 'md' : 'sm'}
      />
      <span className="min-w-0 leading-tight">
        <span className="truncate-1 @min-[1750px]/tabel:text-subhead block text-footnote font-medium">
          {row.fullName}
        </span>
        {detail !== null && (
          <span className="truncate-1 @min-[1750px]/tabel:text-footnote block text-caption2 text-[var(--fg-tertiary)]">
            {detail}
          </span>
        )}
      </span>
    </Link>
  )
}

/**
 * Bitta kun katagi va uning oynachasi.
 *
 * Oynacha Radix menyusi ustiga qurilgan (loyihada allaqachon bor,
 * `user-menu.tsx`): portal orqali suriladigan qutidan TASHQARIGA chiqadi,
 * ya'ni chetdagi qatorlarda qirqilmaydi, ekran chetiga yaqinlashsa o'zi
 * joyini o'zgartiradi, Escape va tashqariga bosish bilan yopiladi va
 * fokusni katakka qaytaradi. Qo'lda yozilgan joylashtirish esa inline
 * `style` bilan koordinata yozishni talab qilardi.
 *
 * Ochilish — BOSISH/TEGISH bilan, sichqoncha ustiga kelishi bilan emas:
 * zich gridda kursor kataklar ustidan o'tganda har birida oyna
 * miltillardi, planshetda esa «ustiga kelish» umuman yo'q. Bitta xatti-
 * harakat sichqoncha, barmoq va klaviatura uchun bir xil ishlaydi.
 *
 * `role="dialog"` — ichida menyu bandlari yo'q, faqat ma'lumot; Radix
 * qo'ygan `menu` roli ekran o'quvchisiga yolg'on gapirardi.
 */
function DayCell({ cell, employeeName }: { cell: TimesheetCell; employeeName: string }) {
  const { t, locale, date } = useLocale()
  const look = DAY_STATUS_LOOK[cell.status]
  const label = `${employeeName} · ${date(cell.date)} · ${t(look.labelKey)}`

  return (
    <DropdownMenu.Root modal={false}>
      <DropdownMenu.Trigger asChild>
        <button
          type="button"
          aria-haspopup="dialog"
          aria-label={label}
          className={cn(
            'tabular grid place-items-center overflow-hidden rounded-xs font-medium',
            /*
              Katak katta ekranda O'SADI (egasi, 2026-09-20): faqat
              kenglik bo'yicha cho'zilish bo'sh joyni xodim ustuniga
              berardi, jadvalning o'zi esa kichkina bo'lib qolaverardi.
              36 → 40 → 44 px: 31 kunda bu ~1180 dan ~1430 px gacha,
              ya'ni 27 dyuymli monitorda jadval maydonni to'ldiradi.
            */
            'size-9 text-caption2',
            '@min-[1500px]/tabel:size-10 @min-[1750px]/tabel:size-11 @min-[1750px]/tabel:text-caption1',
            look.cellClass,
            cell.lateMinutes > 0 && LATE_BAR_CLASS,
            'transition-shadow duration-150 data-[state=open]:ring-2 data-[state=open]:ring-[var(--ring)]',
          )}
        >
          {cellText(cell, t, locale)}
        </button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          role="dialog"
          aria-label={label}
          side="bottom"
          align="center"
          sideOffset={6}
          collisionPadding={12}
          className="pop-panel z-50 w-[288px] rounded-xl border border-[var(--border)] bg-[var(--bg-elevated)] p-4 shadow-floating outline-none"
        >
          <CellDetails cell={cell} />
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/** Katak matni: ishlangan bo'lsa — soat («7,9»), bo'lmasa — holat kodi. */
function cellText(
  cell: TimesheetCell,
  t: (key: string) => string,
  locale: Parameters<typeof formatHours>[1],
): string {
  if (cell.workedMinutes > 0) return formatHours(cell.workedMinutes, locale)
  // Ikkalasi ham ATAYLAB bo'sh: odam ishlamagan sana va hali kelmagan kun.
  if (cell.status === 'NotEmployed' || cell.status === 'Planned') return ''
  return t(STATUS_CODE_KEY[cell.status])
}

/**
 * Katak oynachasi: kirish / chiqish, ishlangan, kechikish, erta ketish va
 * TANAFFUS satri.
 *
 * ⭐ Tanaffus satri — oynachaning eng muhim qatori (SPEC §8.6, AMENDED
 * 2026-09-17). Tanaffus ishlangan vaqtdan AYIRILADI (§5.5.1) va binoda 8
 * soat turgan odamda «7:00» ko'rgan HR birinchi navbatda «nega?» deb
 * so'raydi. Javob kichik jamidan TAXMIN qilinmaydi — to'g'ridan-to'g'ri
 * yoziladi, FAQAT saqlangan ikki maydondan (`breakLine`).
 *
 * Satr odam binoda bo'lgan kunlardagina chiqadi: «Kelmadi» yoki «Dam
 * olish» katagida «tanaffus ayirilmadi» degan gap to'g'ri, lekin hech
 * qanday raqamni tushuntirmaydi — shovqin.
 */
function CellDetails({ cell }: { cell: TimesheetCell }) {
  const { t, date, num } = useLocale()
  const weekday = isoWeekday(cell.date)

  const present =
    cell.firstInAt !== null ||
    cell.lastOutAt !== null ||
    cell.workedMinutes > 0 ||
    cell.breakTakenMinutes > 0 ||
    cell.breakDeductionMinutes > 0

  const minutes = (value: number) => (value > 0 ? t('attendance.minutes', { n: num(value) }) : '—')

  return (
    <div className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="tabular text-subhead font-semibold">{date(cell.date)}</p>
          {weekday !== null && (
            <p className="text-caption1 text-[var(--fg-tertiary)]">
              {t(WEEKDAY_LABEL_KEY[weekday])}
            </p>
          )}
        </div>
        <DayStatusBadge status={cell.status} />
      </div>

      <dl className="grid grid-cols-2 gap-x-4 gap-y-2.5 rounded-md bg-[var(--bg-sunken)] p-3">
        <Fact label={t('attendance.firstIn')}>{formatPassTime(cell.firstInAt)}</Fact>
        <Fact label={t('attendance.lastOut')}>{formatPassTime(cell.lastOutAt)}</Fact>
        <Fact label={t('attendance.worked')}>
          <span className="font-semibold">{formatDuration(cell.workedMinutes)}</span>
        </Fact>
        <Fact label={t('attendance.lateMinutes')}>
          <span className={cn(cell.lateMinutes > 0 && 'font-semibold text-ios-orange')}>
            {minutes(cell.lateMinutes)}
          </span>
        </Fact>
        <Fact label={t('attendance.earlyLeaveMinutes')}>
          <span className={cn(cell.earlyLeaveMinutes > 0 && 'font-semibold text-ios-orange')}>
            {minutes(cell.earlyLeaveMinutes)}
          </span>
        </Fact>
      </dl>

      {present && (
        <p className="flex items-start gap-2 text-footnote text-[var(--fg-secondary)]">
          <Coffee className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
          <span>{breakLine(cell, t, num)}</span>
        </p>
      )}
    </div>
  )
}

/**
 * Tanaffus satri — BIRINCHI MOS KELGANI yutadi (SPEC §8.6, AMENDED
 * 2026-09-17):
 *   1. ayirilgan > 0 → «Tanaffus: N daq. ayirildi» (+ «(M daq. olingan)»);
 *   2. olingan > 0 → «Tanaffus olingan — ayirilmadi»;
 *   3. aks holda → «Tanaffus ayirilmadi».
 *
 * ⚠️ «Tanaffus oynasi belgilanmagan» degan to'rtinchi shakl ATAYLAB YO'Q.
 * Ushlanma binoda bo'lgan vaqt bilan cheklangach (§13 #14), «0 olingan,
 * 0 ayirilgan» ham «oyna yo'q», ham «oyna paytida binoda bo'lmagan» degani
 * va saqlangan qator ularni ajratmaydi. Bugungi jadvalni o'qib
 * «aniqlashtirish» esa raqam hisoblangan jadvalni emas, bugungisini
 * tasvirlardi. Satr faqat qator ISBOTLAGAN narsani aytadi.
 */
function breakLine(
  cell: TimesheetCell,
  t: (key: string, options?: Record<string, unknown>) => string,
  num: (value: number) => string,
): string {
  if (cell.breakDeductionMinutes > 0) {
    const deducted = t('attendance.breakDeducted', { deducted: num(cell.breakDeductionMinutes) })

    return cell.breakTakenMinutes > 0
      ? `${deducted} ${t('attendance.breakTakenPart', { taken: num(cell.breakTakenMinutes) })}`
      : deducted
  }

  if (cell.breakTakenMinutes > 0) return t('attendance.breakTaken')

  return t('attendance.breakNotDeducted')
}

/**
 * Qotirilgan «jami» katagi: ishlangan / rejadagi soat.
 *
 * Ustun ATAYLAB tor — ikki raqam. Kunlar soni, kechikish va erta ketish
 * yig'indilari bosilganda ochiladigan oynachada: qotirilgan ustun har
 * santimetrda kunlar maydonidan joy oladi.
 */
function TotalsCell({ row }: { row: TimesheetRow }) {
  const { t } = useLocale()
  const totals = row.totals

  return (
    <DropdownMenu.Root modal={false}>
      <DropdownMenu.Trigger asChild>
        <button
          type="button"
          aria-haspopup="dialog"
          aria-label={`${row.fullName} · ${t('attendance.totalWorked')}`}
          className={cn(
            'flex w-[96px] flex-col items-end rounded-sm px-2 py-0.5 text-right leading-tight',
            'transition-colors hover:bg-[var(--bg-hover)] data-[state=open]:bg-[var(--bg-active)]',
          )}
        >
          <span className="tabular @min-[1750px]/tabel:text-subhead text-footnote font-semibold">
            {formatDuration(totals.workedMinutes)}
          </span>
          <span className="tabular text-caption2 text-[var(--fg-tertiary)]">
            / {formatDuration(totals.plannedMinutes)}
          </span>
        </button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          role="dialog"
          aria-label={row.fullName}
          side="bottom"
          align="end"
          sideOffset={6}
          collisionPadding={12}
          className="pop-panel z-50 w-[272px] rounded-xl border border-[var(--border)] bg-[var(--bg-elevated)] p-4 shadow-floating outline-none"
        >
          <p className="truncate-1 mb-3 text-subhead font-semibold">{row.fullName}</p>
          <TotalsDetails totals={totals} />
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

function TotalsDetails({ totals }: { totals: TimesheetTotals }) {
  const { t, num } = useLocale()
  const minutes = (value: number) => (value > 0 ? t('attendance.minutes', { n: num(value) }) : '—')

  return (
    <dl className="grid grid-cols-2 gap-x-4 gap-y-2.5 rounded-md bg-[var(--bg-sunken)] p-3">
      <Fact label={t('attendance.totalWorked')}>
        <span className="font-semibold">{formatDuration(totals.workedMinutes)}</span>
      </Fact>
      <Fact label={t('attendance.totalPlanned')}>{formatDuration(totals.plannedMinutes)}</Fact>
      <Fact label={t('attendance.presentDays')}>{num(totals.presentDays)}</Fact>
      <Fact label={t('attendance.absentDays')}>
        <span className={cn(totals.absentDays > 0 && 'font-semibold text-ios-red')}>
          {num(totals.absentDays)}
        </span>
      </Fact>
      <Fact label={t('attendance.lateMinutes')}>{minutes(totals.lateMinutes)}</Fact>
      <Fact label={t('attendance.earlyLeaveMinutes')}>{minutes(totals.earlyLeaveMinutes)}</Fact>
    </dl>
  )
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-caption2 text-[var(--fg-tertiary)]">{label}</dt>
      <dd className="tabular mt-0.5 text-footnote">{children}</dd>
    </div>
  )
}

/* ═══════════════════════════════════════════════════ umumiy: holat va legenda */

/** Kun holati yorlig'i — oynachada va hisobotlar jadvalida bir xil. */
export function DayStatusBadge({ status }: { status: AttendanceDayStatus }) {
  const { t } = useLocale()
  const look = DAY_STATUS_LOOK[status]
  const Icon = look.icon

  return (
    <Badge tone={look.tone} size="sm">
      <Icon className="size-3" strokeWidth={2.5} />
      {t(look.labelKey)}
    </Badge>
  )
}

/**
 * Legenda — sahifaning TEPASIDA, DOIM ko'rinib turadi (SPEC §8.6), yuklanish
 * va bo'sh holatda ham: rang va kodning ma'nosi tooltip'da yashirinsa,
 * uni hech kim topmaydi.
 *
 * ⚠️ Ilgari gridning OSTIDA edi — 50 qatorlik sahifada u ekrandan ancha
 * pastda qolib, HR uni umuman ko'rmasdi (egasi 2026-09-18: «qaysi belgi
 * nimani anglatadi — tepaga yozib qo'y»). Endi sarlavha ostida.
 *
 * ⭐ NAMUNA KATAKDAGIDEK. Ishlangan kunda katakda harf emas, SOAT turadi
 * (`cellText`: «7,7»), ya'ni «Ishladi» va «Dam olish kuni ishladi» namunasi
 * ham soat bilan chiziladi. Birinchi versiyada ular harf kodi («I», «DI»)
 * bilan chizilardi — gridda esa bunday harf HECH QACHON chiqmaydi.
 *
 * Oq (`Present`) va bo'sh (`NotEmployed`) namunalarga ingichka halqa
 * qo'yilgan — FAQAT legendada: oq kartada ular aks holda ko'rinmasdi.
 * Gridning o'zida chiziq yo'q, u yerda ularni qo'shni kataklar ajratadi.
 */
export function TimesheetLegend() {
  const { t, locale } = useLocale()

  /** Namuna matni — katakning o'zi qanday yozsa, shunday (`cellText`). */
  const sample = (status: AttendanceDayStatus): string =>
    status === 'NotEmployed' || status === 'Planned'
      ? ''
      : status === 'Present' || status === 'DayOffWorked'
        ? formatHours(SAMPLE_WORKED_MINUTES, locale)
        : t(STATUS_CODE_KEY[status])

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-2.5">
      <span className="text-caption1 font-semibold text-[var(--fg-secondary)]">
        {t('attendance.legend')}
      </span>

      {DAY_STATUS_LEGEND.map((status) => (
        <span key={status} className="flex items-center gap-1.5 text-caption1 text-[var(--fg-secondary)]">
          <span
            aria-hidden
            className={cn(
              'tabular grid h-5 min-w-6 place-items-center rounded-xs px-1 text-caption2 font-medium',
              DAY_STATUS_LOOK[status].cellClass,
              (status === 'Present' || status === 'NotEmployed') &&
                'ring-1 ring-[var(--border)] ring-inset',
            )}
          >
            {sample(status)}
          </span>
          {/* Oq katakdagi raqamning ma'nosi aynan shu yerda aytiladi */}
          {status === 'Present' ? t('attendance.legendPresentHours') : t(DAY_STATUS_LOOK[status].labelKey)}
        </span>
      ))}

      <span className="flex items-center gap-1.5 text-caption1 text-[var(--fg-secondary)]">
        <span
          aria-hidden
          className={cn(
            'grid h-5 min-w-6 overflow-hidden rounded-xs ring-1 ring-[var(--border)] ring-inset',
            LATE_BAR_CLASS,
          )}
        />
        {t('attendance.late')}
      </span>
    </div>
  )
}
