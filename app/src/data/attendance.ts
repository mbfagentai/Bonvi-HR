import type {
  AbsenceRequestDto,
  AttendanceDayStatus,
  AttendanceMarkDto,
  AttendanceMonthDto,
  MyCurrentStateDto,
  TimesheetCellDto,
  TimesheetTotalsDto,
} from '@/api/types'
import { daysBetween } from '@/lib/time'

/**
 * Davomat ma'lumotini ekran tushunadigan shaklga keltirish.
 *
 * ⚠️ Bu yerda HECH QANDAY yangi qoida yo'q: holatlar, belgilarning qaysi
 * katakka tushishi va keyingi yo'nalish SERVERDA hisoblanadi, bu fayl
 * ularni faqat o'qiydi. Kechikish «holat» emas, SON — odam kechikib kelsa
 * ham `Ishladi` bo'ladi (UX §D.4).
 */

/**
 * Ekrandagi bitta kun: tabel katagi + shu katakka tushgan belgilar.
 *
 * Simda katak ichma-ich keladi (`{ day, marks }`); ekranlar esa tekis
 * obyekt bilan ishlaydi — bu aylantirish SHU YERDA bir marta bajariladi.
 */
export interface AttendanceDay extends TimesheetCellDto {
  marks: AttendanceMarkDto[]
}

/** Ekrandagi oy. */
export interface AttendanceMonth {
  year: number
  month: number
  days: AttendanceDay[]
  totals: TimesheetTotalsDto
  current: MyCurrentStateDto
}

export function toMonth(dto: AttendanceMonthDto): AttendanceMonth {
  return {
    year: dto.year,
    month: dto.month,
    days: dto.days.map((item) => ({ ...item.day, marks: item.marks })),
    totals: dto.totals,
    current: dto.current,
  }
}

/** Oylik yakun: soatlar SERVERNIKI, kun sanoqlari kataklardan. */
export interface MonthTotals {
  presentDays: number
  absentDays: number
  excusedDays: number
  lateDays: number
  plannedMinutes: number
  workedMinutes: number
  /** «Kam ishlangan» — erkin kunlardagi normadan kami, serverning jami qatoridan. */
  shortfallMinutes: number
}

/**
 * Oylik yakun.
 *
 * ⚠️ Rejadagi va ishlangan daqiqalar SERVERNING jami qatoridan
 * (`totals`) — HR tabelidagi AYNAN o'sha son. Ilgari ilova rejani
 * `planEnd − planStart − tanaffus` bilan o'zi hisoblardi va HR
 * ko'rgan «Rejadagi soat» dan farq qilardi (reja surati tanaffusni boshqa
 * shartda ayiradi). Kunlar esa kataklardan: «Sababli» va «dam olish kuni
 * ishladi» server jami qatorida alohida yo'q.
 */
export function monthTotals(days: readonly AttendanceDay[], totals: TimesheetTotalsDto | null): MonthTotals {
  const result: MonthTotals = {
    presentDays: 0,
    absentDays: 0,
    excusedDays: 0,
    lateDays: 0,
    plannedMinutes: totals ? totals.plannedMinutes : 0,
    workedMinutes: totals ? totals.workedMinutes : 0,
    // Eski server bu maydonni bermaydi — erkin kun ham yo'q
    shortfallMinutes: totals ? totals.shortfallMinutes ?? 0 : 0,
  }

  days.forEach((day) => {
    if (day.status === 'Present' || day.status === 'DayOffWorked') result.presentDays += 1
    if (day.status === 'Absent') result.absentDays += 1
    if (day.status === 'Excused') result.excusedDays += 1
    if (day.lateMinutes > 0) result.lateDays += 1
  })

  return result
}

/**
 * Erkin kunning «Ish vaqti» (rejadagi daqiqalar), aks holda `null`.
 * Hosil qilingan katakda reja yo'q (`plannedMinutes = 0`) — u erkin
 * deb ko'rsatilmaydi: ko'rsatadigan oyna ham, norma ham yo'q.
 */
export function flexiblePlannedMinutes(day: TimesheetCellDto | null): number | null {
  if (!day || day.isFlexible !== true) return null
  const planned = day.plannedMinutes ?? 0
  return planned > 0 ? planned : null
}

export function findDay(
  days: readonly AttendanceDay[] | undefined,
  dateIso: string,
): AttendanceDay | null {
  if (!days) return null
  return days.filter((day) => day.date === dateIso)[0] ?? null
}

/** Lahzani solishtirish uchun — ISO satrning SILJISHI har xil bo'lishi mumkin. */
function instant(value: string): number {
  const parsed = Date.parse(value)
  return Number.isFinite(parsed) ? parsed : 0
}

/**
 * Kun ichidagi o'tishlar — yangisi tepada (UX §A.5).
 *
 * ⚠️ Lahza bo'yicha, satr bo'yicha EMAS: `…+00:00` va `…+05:00` satrlarini
 * `localeCompare` bilan solishtirish boshqa tartib berardi.
 */
export function marksNewestFirst(day: AttendanceDay | null): AttendanceMarkDto[] {
  if (!day) return []
  return day.marks.slice().sort((a, b) => instant(b.at) - instant(a.at))
}

/** Tanlangan kunni qamrab olgan so'rovlar. */
export function requestsOnDay(
  requests: readonly AbsenceRequestDto[] | undefined,
  dateIso: string,
): AbsenceRequestDto[] {
  if (!requests) return []
  return requests.filter((request) => request.fromDate <= dateIso && dateIso <= request.toDate)
}

/** So'rov necha kunni egallaydi — ikkala chet ham kiradi. */
export function requestDayCount(request: AbsenceRequestDto): number {
  return daysBetween(request.fromDate, request.toDate)
}

/** «09:00:00» → «09:00» — ekranda soniya ko'rsatilmaydi. */
export function shortTime(value: string | null): string | null {
  return value ? value.slice(0, 5) : null
}

/** Oyda uchragan holatlar — taqvim ostidagi «Belgilar» ro'yxati uchun. */
export function statusesInMonth(days: readonly AttendanceDay[]): AttendanceDayStatus[] {
  const seen: AttendanceDayStatus[] = []

  days.forEach((day) => {
    if (day.status === 'Planned' || day.status === 'NotEmployed') return
    if (seen.indexOf(day.status) < 0) seen.push(day.status)
  })

  return seen
}
