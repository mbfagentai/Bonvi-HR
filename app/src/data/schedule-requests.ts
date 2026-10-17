import type { TFunction } from 'i18next'

import type {
  AbsenceRequestDto,
  AbsenceRequestStatus,
  ScheduleChangeDayDto,
  ScheduleChangeRequestDto,
} from '@/api/types'
import type { Formatter } from '@/lib/format'
import { shortTime } from './attendance'

/**
 * Jadval so'rovlari — ekranga qulay shakl (SPEC-jadval-sorovi §C).
 *
 * ⚠️ Qoidalar SERVERNIKI (`WorkScheduleDay.Validate`): bu yerdagi
 * tekshiruv faqat xodimni serverga borib-kelishdan oldin ogohlantiradi.
 * Ikkalasi farq qilsa server haq — uning javobi baribir tarjima bo'lib
 * chiqadi (`errors.davomat.jadval_*`).
 */

const MINUTES_PER_DAY = 24 * 60

/** «08:30» → 510. */
function minutesOf(hhmm: string): number {
  const hours = Number(hhmm.slice(0, 2))
  const minutes = Number(hhmm.slice(3, 5))
  return hours * 60 + minutes
}

/** `from` dan `to` gacha necha daqiqa — yarim tundan o'tib (`TimeOnly` ayirmasi kabi). */
function forward(from: string, to: string): number {
  return (minutesOf(to) - minutesOf(from) + MINUTES_PER_DAY) % MINUTES_PER_DAY
}

/** Tugash boshlanishdan oldin — tungi smena, ertasi kuni tugaydi. */
export function crossesMidnight(start: string, end: string): boolean {
  return start !== '' && end !== '' && end < start
}

/**
 * Tanaffus smena ICHIDAmi — soat ciferblatida emas, smenaning o'z o'qida
 * (server bilan aynan bir xil): 20:00–08:00 smenada 01:00 tanaffus
 * boshlanishdan besh soat KEYIN turadi.
 */
export function breakInsideShift(start: string, end: string, breakStart: string, breakEnd: string): boolean {
  const shiftLength = forward(start, end)
  const fromStart = forward(start, breakStart)
  const toStart = forward(start, breakEnd)

  return fromStart < toStart && toStart <= shiftLength
}

/** Forma qiymati («08:30» yoki bo'sh) → simdagi `TimeOnly` («08:30:00»). */
export function wireTime(hhmm: string): string | null {
  return hhmm === '' ? null : `${hhmm}:00`
}

/** Kunning rejasi bir satrda: «08:30–18:00, tanaffus 12:20–14:20» yoki «Dam olish kuni». */
export function dayPlanText(
  t: TFunction,
  day: Pick<ScheduleChangeDayDto, 'isWorking' | 'startTime' | 'endTime' | 'breakStart' | 'breakEnd'>,
): string {
  if (!day.isWorking) return t('schedule.dayOff')

  const range = `${shortTime(day.startTime) ?? '—'}–${shortTime(day.endTime) ?? '—'}`

  return day.breakStart && day.breakEnd
    ? t('schedule.rangeWithBreak', {
        range,
        break: `${shortTime(day.breakStart) ?? ''}–${shortTime(day.breakEnd) ?? ''}`,
      })
    : range
}

/**
 * Almashtirish juftlari: server har bir sanani ALOHIDA qator qilib beradi
 * (A ⇄ B va B ⇄ A), ekranga esa juft bir marta chiqadi.
 */
export function swapPairs(days: readonly ScheduleChangeDayDto[]): Array<{ date: string; withDate: string }> {
  return days
    .filter((day) => day.swapWith !== null && day.date < day.swapWith)
    .map((day) => ({ date: day.date, withDate: day.swapWith ?? day.date }))
}

/** Ro'yxat qatorining sarlavhasi — tur bo'yicha. */
export function scheduleTitleKey(request: ScheduleChangeRequestDto): string {
  return request.kind === 'Swap' ? 'schedule.titleSwap' : 'schedule.titleChange'
}

/**
 * Ro'yxat qatorining ikkinchi satrlari: «16.10 · 08:30–18:00, tanaffus …»,
 * «16.10 · Dam olish kuni», «15.10 ⇄ 18.10». Uzun so'rovda birinchi
 * uchtasi va «yana N ta».
 */
export function scheduleSummaryLines(
  t: TFunction,
  format: Formatter,
  request: ScheduleChangeRequestDto,
  limit = 3,
): string[] {
  const lines =
    request.kind === 'Swap'
      ? swapPairs(request.days).map((pair) => `${format.dayMonth(pair.date)} ⇄ ${format.dayMonth(pair.withDate)}`)
      : request.days.map((day) => `${format.dayMonth(day.date)} · ${dayPlanText(t, day)}`)

  if (lines.length <= limit) return lines

  return lines.slice(0, limit).concat([t('schedule.moreItems', { n: lines.length - limit })])
}

/** Ikki xil so'rov bitta ro'yxatda — `createdAt` bo'yicha, yangisi tepada. */
export type AnyRequest =
  | { type: 'absence'; id: string; status: AbsenceRequestStatus; createdAt: string; absence: AbsenceRequestDto }
  | {
      type: 'schedule'
      id: string
      status: AbsenceRequestStatus
      createdAt: string
      schedule: ScheduleChangeRequestDto
    }

/** Lahza — satr emas: `…+00:00` va `…Z` satrlari leksik tartibda adashadi. */
function instant(value: string): number {
  const parsed = Date.parse(value)
  return Number.isFinite(parsed) ? parsed : 0
}

export function mergeRequests(
  absence: readonly AbsenceRequestDto[],
  schedule: readonly ScheduleChangeRequestDto[],
): AnyRequest[] {
  const merged: AnyRequest[] = absence
    .map<AnyRequest>((item) => ({
      type: 'absence',
      id: item.id,
      status: item.status,
      createdAt: item.createdAt,
      absence: item,
    }))
    .concat(
      schedule.map<AnyRequest>((item) => ({
        type: 'schedule',
        id: item.id,
        status: item.status,
        createdAt: item.createdAt,
        schedule: item,
      })),
    )

  return merged.sort((a, b) => instant(b.createdAt) - instant(a.createdAt))
}
