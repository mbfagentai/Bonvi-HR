import { addDays, daysInMonth, pad, parseIsoDate, todayIso } from '@/lib/time'
import type {
  AbsenceRequestDto,
  AbsenceTypeDto,
  AttendanceDayDto,
  AttendanceMarkDto,
  MeDto,
} from '@/api/types'

import mockAvatarUrl from './avatar.jpg'

/**
 * Soxta ma'lumot — haqiqiy server ulanmaguncha.
 *
 * ⚠️ Bu fayl BIR MARTALIK: keyingi to'lqinda `src/mock/` butunlay
 * o'chadi va `api/client.ts` dagi bitta qator haqiqiy mijozga
 * almashadi. Shu sababli bu yerda ekranga tegadigan hech narsa yo'q —
 * faqat `api/types.ts` dagi shakllar to'ldiriladi.
 *
 * Kunlar SANA bo'yicha hosil qilinadi, qo'lda yozilmaydi: aks holda
 * ertaga taqvim bo'm-bo'sh ochilardi va egasi ilovani o'lik deb
 * o'ylardi.
 */

/** Jadval: 08:30–18:00, tanaffus 13:00–14:00 (Toshkent vaqti). */
const PLAN_START_HOUR = 8
const PLAN_START_MINUTE = 30
const PLAN_END_HOUR = 18
const BREAK_MINUTES = 60

export const MOCK_SITE_NAME = 'Bosh ofis'

export const MOCK_ABSENCE_TYPES: AbsenceTypeDto[] = [
  { id: 'at-1', name: 'Ishxona ishi bilan' },
  { id: 'at-2', name: 'Kasallik' },
  { id: 'at-3', name: 'Mehnat ta’tili' },
  { id: 'at-4', name: 'O‘z hisobidan' },
  { id: 'at-5', name: 'Oilaviy sabab' },
]

export const MOCK_ME: MeDto = {
  employeeId: 'emp-1',
  fullName: 'Aliyev Sardor Baxtiyorovich',
  // Sample portrait so the avatar can be seen on screen. An employee
  // without a photo is still a normal case — the initials fallback stays.
  // The real `/api/me` returns a full URL.
  photoUrl: mockAvatarUrl,
  departmentName: 'Ishlab chiqarish bo‘limi',
  positionName: 'Smena boshlig‘i',
  mobileCheckInAllowed: true,
  absenceTypes: MOCK_ABSENCE_TYPES,
}

/** Toshkent vaqtidagi soatni UTC lahzaga aylantiradi (mintaqa +5). */
export function tashkentToUtcIso(dateIso: string, hour: number, minute: number): string {
  const parts = parseIsoDate(dateIso)
  if (!parts) return new Date().toISOString()

  return new Date(Date.UTC(parts.year, parts.month - 1, parts.day, hour - 5, minute)).toISOString()
}

function mark(
  dateIso: string,
  hour: number,
  minute: number,
  direction: 'In' | 'Out',
  source: 'Terminal' | 'Mobile' = 'Terminal',
): AttendanceMarkDto {
  return {
    occurredAt: tashkentToUtcIso(dateIso, hour, minute),
    direction,
    source,
    siteName: source === 'Mobile' ? MOCK_SITE_NAME : null,
  }
}

/** Kun raqamidan barqaror «tasodif» — har ochilganda bir xil chiqsin. */
function jitter(dateIso: string, span: number): number {
  let hash = 0
  for (let i = 0; i < dateIso.length; i += 1) {
    hash = (hash * 31 + dateIso.charCodeAt(i)) % 100003
  }
  return hash % span
}

function emptyDay(dateIso: string, status: AttendanceDayDto['status']): AttendanceDayDto {
  return {
    date: dateIso,
    status,
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    breakDeductionMinutes: 0,
    firstInAt: null,
    lastOutAt: null,
    planStartAt: null,
    planEndAt: null,
    marks: [],
  }
}

/** Lunch window from the schedule (Tashkent time), minutes since midnight. */
const BREAK_WINDOW_START = 13 * 60
const BREAK_WINDOW_END = BREAK_WINDOW_START + BREAK_MINUTES

/**
 * Extra trips out of the building during the day, [out, in] as minutes
 * since midnight. An employee can go in and out many times a day — the
 * day card has to show all of it, so days vary between none, one and
 * two extra trips on top of lunch.
 */
const EXTRA_TRIPS: Array<Array<[number, number]>> = [
  [],
  [[10 * 60 + 42, 10 * 60 + 58]],
  [[15 * 60 + 20, 15 * 60 + 34]],
  [
    [10 * 60 + 5, 10 * 60 + 17],
    [16 * 60 + 15, 16 * 60 + 31],
  ],
]

/**
 * Worked minutes the way the server pairs passes (SPEC §5.5): sum of
 * closed in→out intervals, minus the break deduction
 * `max(0, min(break − breakTaken, presentInWindow))` — a lunch spent
 * outside the building is not deducted twice.
 */
function workedFromIntervals(intervals: Array<[number, number]>): { worked: number; deduction: number } {
  let closed = 0
  let presentInWindow = 0

  intervals.forEach(([from, to]) => {
    closed += to - from
    presentInWindow += Math.max(0, Math.min(to, BREAK_WINDOW_END) - Math.max(from, BREAK_WINDOW_START))
  })

  const breakTaken = BREAK_MINUTES - presentInWindow
  const deduction = Math.max(0, Math.min(BREAK_MINUTES - breakTaken, presentInWindow))
  return { worked: closed - deduction, deduction }
}

/**
 * One worked day: entry, lunch out/in, possibly extra trips out, exit.
 * Verifix's day card is multi-row exactly like this — a day has far more
 * than two passes.
 */
function workedDay(dateIso: string, lateMinutes: number, mobile: boolean): AttendanceDayDto {
  const inAt = PLAN_START_HOUR * 60 + PLAN_START_MINUTE + lateMinutes - 5 + jitter(dateIso, 4)
  const outAt = PLAN_END_HOUR * 60 + jitter(dateIso, 12)

  // Lunch trip may or may not happen — some days the employee eats inside.
  const lunch: Array<[number, number]> = jitter(dateIso, 5) === 0 ? [] : [[13 * 60 + 1, 13 * 60 + 56]]
  const trips = (EXTRA_TRIPS[jitter(`${dateIso}x`, EXTRA_TRIPS.length)] ?? []).concat(lunch)
  trips.sort((a, b) => a[0] - b[0])

  // Building intervals: from entry to the first trip out, between trips, to the exit.
  const intervals: Array<[number, number]> = []
  let cursor = inAt
  trips.forEach(([out, back]) => {
    intervals.push([cursor, out])
    cursor = back
  })
  intervals.push([cursor, outAt])

  const { worked, deduction } = workedFromIntervals(intervals)
  const at = (minutes: number, direction: 'In' | 'Out', source?: 'Terminal' | 'Mobile') =>
    mark(dateIso, Math.floor(minutes / 60), minutes % 60, direction, source)

  const marks: AttendanceMarkDto[] = [at(inAt, 'In', mobile ? 'Mobile' : 'Terminal')]
  trips.forEach(([out, back]) => {
    marks.push(at(out, 'Out'))
    marks.push(at(back, 'In'))
  })
  marks.push(at(outAt, 'Out', mobile ? 'Mobile' : 'Terminal'))

  return {
    date: dateIso,
    status: 'Present',
    workedMinutes: worked,
    lateMinutes,
    earlyLeaveMinutes: 0,
    breakDeductionMinutes: deduction,
    firstInAt: at(inAt, 'In').occurredAt,
    lastOutAt: at(outAt, 'Out').occurredAt,
    planStartAt: tashkentToUtcIso(dateIso, PLAN_START_HOUR, PLAN_START_MINUTE),
    planEndAt: tashkentToUtcIso(dateIso, PLAN_END_HOUR, 0),
    marks: marks.reverse(),
  }
}

/**
 * Oyning kunlari.
 *
 * Qoidalar ataylab sodda va takrorlanadigan: hafta oxiri — dam olish,
 * kelajak — «Kun kelmagan», qolgani ishlangan kun; oyning ichida bitta
 * kelmagan kun, ikkita kechikish, bitta «Chiqish yo'q» va tasdiqlangan
 * so'rov tushgan ikki «Sababli» kun bor. Bu to'plam taqvimning barcha
 * katak holatlarini bitta ekranda ko'rsatadi.
 */
export function buildMonth(year: number, month: number, excusedDates: string[]): AttendanceDayDto[] {
  const today = todayIso()
  const total = daysInMonth(year, month)
  const days: AttendanceDayDto[] = []

  for (let day = 1; day <= total; day += 1) {
    const dateIso = `${year}-${pad(month)}-${pad(day)}`
    const parts = parseIsoDate(dateIso)
    if (!parts) continue

    const isWeekend = parts.weekday >= 6
    const isFuture = dateIso > today

    if (excusedDates.indexOf(dateIso) >= 0) {
      days.push(emptyDay(dateIso, 'Excused'))
      continue
    }

    if (isWeekend) {
      // Oyda bitta shanba ishlangan bo'lsin — «Dam olish kuni ishladi»
      // katagi ham ko'rinsin (UX §D.4 dagi binafsha holat).
      if (parts.weekday === 6 && day % 14 === 6 && !isFuture) {
        const worked = workedDay(dateIso, 0, false)
        days.push({ ...worked, status: 'DayOffWorked' })
      } else {
        days.push(emptyDay(dateIso, 'DayOff'))
      }
      continue
    }

    if (isFuture) {
      days.push(emptyDay(dateIso, 'Planned'))
      continue
    }

    if (dateIso === today) {
      // Bugungi kun ALOHIDA yig'iladi — belgilash holatiga bog'liq
      continue
    }

    if (day % 17 === 3) {
      days.push(emptyDay(dateIso, 'Absent'))
      continue
    }

    if (day % 11 === 5) {
      const worked = workedDay(dateIso, 0, false)
      days.push({
        ...worked,
        status: 'MissingExit',
        lastOutAt: null,
        workedMinutes: 0,
        // The final exit never got read — every other pass stays.
        marks: worked.marks.slice(1),
      })
      continue
    }

    const late = day % 7 === 2 ? 12 : day % 13 === 4 ? 25 : 0
    days.push(workedDay(dateIso, late, day % 9 === 0))
  }

  return days
}

/**
 * Sample passes for today so the home screen and today's calendar cell
 * are not empty on first open: a turnstile entry in the morning and
 * several trips out and back (a short errand, lunch, another errand).
 * Only passes already in the past (Tashkent time) are
 * returned, ascending — check-ins from the app are appended after them.
 * Weekends stay empty.
 */
export function buildTodaySeed(): AttendanceMarkDto[] {
  const dateIso = todayIso()
  const parts = parseIsoDate(dateIso)
  if (!parts || parts.weekday >= 6) return []

  const nowMs = Date.now()
  const seed = [
    mark(dateIso, PLAN_START_HOUR, PLAN_START_MINUTE - 4, 'In'),
    mark(dateIso, 10, 38, 'Out'),
    mark(dateIso, 10, 51, 'In'),
    mark(dateIso, 13, 1, 'Out'),
    mark(dateIso, 13, 56, 'In'),
    mark(dateIso, 15, 12, 'Out'),
    mark(dateIso, 15, 27, 'In'),
  ]

  return seed.filter((item) => new Date(item.occurredAt).getTime() <= nowMs)
}

/** Bugungi kun — belgilangan o'tishlardan yig'iladi. */
export function buildToday(marks: AttendanceMarkDto[]): AttendanceDayDto {
  const dateIso = todayIso()
  const parts = parseIsoDate(dateIso)
  const isWeekend = parts ? parts.weekday >= 6 : false

  const base: AttendanceDayDto = {
    date: dateIso,
    status: isWeekend ? 'DayOff' : 'Planned',
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    breakDeductionMinutes: 0,
    firstInAt: null,
    lastOutAt: null,
    planStartAt: isWeekend ? null : tashkentToUtcIso(dateIso, PLAN_START_HOUR, PLAN_START_MINUTE),
    planEndAt: isWeekend ? null : tashkentToUtcIso(dateIso, PLAN_END_HOUR, 0),
    marks,
  }

  if (marks.length === 0) return base

  const sorted = marks.slice().sort((a, b) => a.occurredAt.localeCompare(b.occurredAt))
  const firstIn = sorted.filter((item) => item.direction === 'In')[0] ?? null
  const outs = sorted.filter((item) => item.direction === 'Out')
  const lastOut = outs.length > 0 ? outs[outs.length - 1] ?? null : null

  const firstInMs = firstIn ? new Date(firstIn.occurredAt).getTime() : 0

  // Pair passes into closed in→out intervals (minutes since Tashkent midnight).
  const dayStartMs = new Date(tashkentToUtcIso(dateIso, 0, 0)).getTime()
  const intervals: Array<[number, number]> = []
  let openAt: number | null = null
  sorted.forEach((item) => {
    const minute = Math.round((new Date(item.occurredAt).getTime() - dayStartMs) / 60_000)
    if (item.direction === 'In') openAt = openAt ?? minute
    else if (openAt !== null) {
      intervals.push([openAt, minute])
      openAt = null
    }
  })
  const pairing = workedFromIntervals(intervals)
  const workedMinutes = pairing.worked

  const planStartMs = base.planStartAt ? new Date(base.planStartAt).getTime() : 0
  const lateMinutes =
    firstIn && planStartMs > 0 && firstInMs > planStartMs
      ? Math.round((firstInMs - planStartMs) / 60_000)
      : 0

  return {
    ...base,
    status: isWeekend ? 'DayOffWorked' : openAt === null ? 'Present' : 'MissingExit',
    workedMinutes,
    lateMinutes,
    breakDeductionMinutes: pairing.deduction,
    firstInAt: firstIn ? firstIn.occurredAt : null,
    lastOutAt: lastOut ? lastOut.occurredAt : null,
  }
}

/**
 * So'rovlar. Sanalar bugundan hisoblanadi — ro'yxat hech qachon
 * «o'tgan yilgi» bo'lib qolmaydi.
 */
export function buildRequests(): AbsenceRequestDto[] {
  const today = todayIso()

  return [
    {
      id: 'req-1',
      absenceTypeId: 'at-1',
      absenceTypeName: 'Ishxona ishi bilan',
      fromDate: addDays(today, 3),
      toDate: addDays(today, 4),
      fromTime: null,
      toTime: null,
      note: 'Buyurtmachi omboriga boraman, kelishuv bo‘yicha.',
      status: 'Pending',
      decisionNote: null,
      decidedByName: null,
      decidedAt: null,
      createdAt: tashkentToUtcIso(addDays(today, -1), 9, 14),
    },
    {
      id: 'req-2',
      absenceTypeId: 'at-2',
      absenceTypeName: 'Kasallik',
      fromDate: addDays(today, -9),
      toDate: addDays(today, -8),
      fromTime: null,
      toTime: null,
      note: 'Shifokorga bordim.',
      status: 'Approved',
      decisionNote: 'Ma’lumotnoma qabul qilindi.',
      decidedByName: 'Karimova Nodira',
      decidedAt: tashkentToUtcIso(addDays(today, -7), 11, 2),
      createdAt: tashkentToUtcIso(addDays(today, -10), 8, 41),
    },
    {
      id: 'req-3',
      absenceTypeId: 'at-4',
      absenceTypeName: 'O‘z hisobidan',
      fromDate: addDays(today, -20),
      toDate: addDays(today, -20),
      fromTime: '09:00',
      toTime: '13:00',
      note: null,
      status: 'Rejected',
      decisionNote: 'Oy yopilayotgan kun — boshqa kunni tanlang.',
      decidedByName: 'Karimova Nodira',
      decidedAt: tashkentToUtcIso(addDays(today, -21), 16, 30),
      createdAt: tashkentToUtcIso(addDays(today, -22), 15, 5),
    },
    {
      id: 'req-4',
      absenceTypeId: 'at-5',
      absenceTypeName: 'Oilaviy sabab',
      fromDate: addDays(today, -31),
      toDate: addDays(today, -31),
      fromTime: null,
      toTime: null,
      note: 'To‘yga boraman.',
      status: 'Cancelled',
      decisionNote: null,
      decidedByName: null,
      decidedAt: null,
      createdAt: tashkentToUtcIso(addDays(today, -33), 10, 12),
    },
  ]
}
