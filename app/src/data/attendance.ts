import type {
  AbsenceRequestDto,
  AttendanceDayDto,
  AttendanceDayStatus,
  AttendanceMarkDto,
  PassDirection,
} from '@/api/types'
import { daysBetween } from '@/lib/time'

/**
 * Davomat ma'lumotini ekran tushunadigan shaklga keltirish.
 *
 * ⚠️ Bu yerda HECH QANDAY yangi qoida yo'q: holatlar serverda
 * hisoblanadi, bu fayl ularni faqat o'qiydi. Kechikish «holat» emas,
 * SON — odam kechikib kelsa ham `Ishladi` bo'ladi (UX §D.4).
 */

/** Oylik yakun — serverdan alohida so'ralmaydi, kataklardan yig'iladi. */
export interface MonthTotals {
  presentDays: number
  absentDays: number
  excusedDays: number
  lateDays: number
  plannedMinutes: number
  workedMinutes: number
}

export function monthTotals(days: readonly AttendanceDayDto[]): MonthTotals {
  const totals: MonthTotals = {
    presentDays: 0,
    absentDays: 0,
    excusedDays: 0,
    lateDays: 0,
    plannedMinutes: 0,
    workedMinutes: 0,
  }

  days.forEach((day) => {
    if (day.status === 'Present' || day.status === 'DayOffWorked') totals.presentDays += 1
    if (day.status === 'Absent') totals.absentDays += 1
    if (day.status === 'Excused') totals.excusedDays += 1
    if (day.lateMinutes > 0) totals.lateDays += 1

    totals.workedMinutes += day.workedMinutes

    if (day.planStartAt && day.planEndAt) {
      const planned = Math.round(
        (new Date(day.planEndAt).getTime() - new Date(day.planStartAt).getTime()) / 60_000,
      )
      if (planned > 0) totals.plannedMinutes += planned - day.breakDeductionMinutes
    }
  })

  return totals
}

export function findDay(
  days: readonly AttendanceDayDto[] | undefined,
  dateIso: string,
): AttendanceDayDto | null {
  if (!days) return null
  return days.filter((day) => day.date === dateIso)[0] ?? null
}

/** Kun ichidagi o'tishlar — yangisi tepada (UX §A.5). */
export function marksNewestFirst(day: AttendanceDayDto | null): AttendanceMarkDto[] {
  if (!day) return []
  return day.marks.slice().sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))
}

/**
 * Keyingi belgi qaysi yo'nalishda bo'lishi (UX §C.2).
 *
 * ⚠️ Yo'nalishni XODIM tanlamaydi. Bitta tugma, va ilova qaysi
 * yo'nalish kerakligini o'zi biladi: ikki tugma 08:30 da «Chiqish»
 * bosishga taklif qilardi.
 */
export function nextDirection(day: AttendanceDayDto | null): PassDirection {
  const marks = marksNewestFirst(day)
  const last = marks[0]

  if (!last) return 'In'
  return last.direction === 'In' ? 'Out' : 'In'
}

/** Oxirgi qabul qilingan o'tish — tasdiq qatori uchun. */
export function lastMark(day: AttendanceDayDto | null): AttendanceMarkDto | null {
  return marksNewestFirst(day)[0] ?? null
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

/** Oyda uchragan holatlar — taqvim ostidagi «Belgilar» ro'yxati uchun. */
export function statusesInMonth(days: readonly AttendanceDayDto[]): AttendanceDayStatus[] {
  const seen: AttendanceDayStatus[] = []

  days.forEach((day) => {
    if (day.status === 'Planned' || day.status === 'NotEmployed') return
    if (seen.indexOf(day.status) < 0) seen.push(day.status)
  })

  return seen
}
