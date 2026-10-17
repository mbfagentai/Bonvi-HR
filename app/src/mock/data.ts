import { addDays, daysInMonth, pad, parseIsoDate, todayIso } from '@/lib/time'
import type {
  AbsenceRequestDto,
  AbsenceTypeDto,
  AttendanceMarkDto,
  DayPlanDto,
  MeDto,
  ScheduleChangeDayDto,
  ScheduleChangeRequestDto,
  TimesheetCellDto,
} from '@/api/types'

/**
 * Soxta ma'lumot — namoyish va qo'lda sinash uchun (`VITE_API_MODE=mock`).
 *
 * ⚠️ Bu yerda ekranga tegadigan hech narsa yo'q — faqat `api/types.ts`
 * dagi SIM shakllari to'ldiriladi (server bilan aynan bir xil): mock
 * shartnomadan chetga chiqsa, ekranlar faqat mock'da ishlab, serverda
 * sinib qolardi.
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
  shortName: 'Aliyev S.B.',
  // Rasm kaliti — baytlar `getPhoto` dan (mock'da `avatar.jpg`). Rasmsiz
  // xodim ham odatiy hol: `null` bo'lsa bosh harflar chiqadi.
  photoKey: 'mock/avatar.jpg',
  department: 'Ishlab chiqarish bo‘limi',
  position: 'Smena boshlig‘i',
  mobileCheckInAllowed: true,
  absenceTypes: MOCK_ABSENCE_TYPES,
}

/** Mock kuni — katak va uning belgilari (simda `{ day, marks }` bo'lib ketadi). */
export interface MockDay extends TimesheetCellDto {
  marks: AttendanceMarkDto[]
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
    at: tashkentToUtcIso(dateIso, hour, minute),
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

function emptyDay(dateIso: string, status: MockDay['status']): MockDay {
  return {
    date: dateIso,
    status,
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    breakTakenMinutes: 0,
    breakDeductionMinutes: 0,
    firstInAt: null,
    lastOutAt: null,
    planStartAt: null,
    planEndAt: null,
    mobileMarks: 0,
    marks: [],
  }
}

/** Kundagi telefon belgilari soni — tabel katagidagi `mobileMarks`. */
function mobileCount(marks: readonly AttendanceMarkDto[]): number {
  return marks.filter((item) => item.source === 'Mobile').length
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
function workedFromIntervals(
  intervals: Array<[number, number]>,
): { worked: number; deduction: number; breakTaken: number } {
  let closed = 0
  let presentInWindow = 0

  intervals.forEach(([from, to]) => {
    closed += to - from
    presentInWindow += Math.max(0, Math.min(to, BREAK_WINDOW_END) - Math.max(from, BREAK_WINDOW_START))
  })

  const breakTaken = BREAK_MINUTES - presentInWindow
  const deduction = Math.max(0, Math.min(BREAK_MINUTES - breakTaken, presentInWindow))
  return { worked: closed - deduction, deduction, breakTaken }
}

/**
 * One worked day: entry, lunch out/in, possibly extra trips out, exit.
 * Verifix's day card is multi-row exactly like this — a day has far more
 * than two passes.
 */
function workedDay(dateIso: string, lateMinutes: number, mobile: boolean): MockDay {
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

  const { worked, deduction, breakTaken } = workedFromIntervals(intervals)
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
    breakTakenMinutes: breakTaken,
    breakDeductionMinutes: deduction,
    firstInAt: at(inAt, 'In').at,
    lastOutAt: at(outAt, 'Out').at,
    planStartAt: tashkentToUtcIso(dateIso, PLAN_START_HOUR, PLAN_START_MINUTE),
    planEndAt: tashkentToUtcIso(dateIso, PLAN_END_HOUR, 0),
    mobileMarks: mobileCount(marks),
    marks,
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
export function buildMonth(year: number, month: number, excusedDates: string[]): MockDay[] {
  const today = todayIso()
  const total = daysInMonth(year, month)
  const days: MockDay[] = []

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
        marks: worked.marks.slice(0, -1),
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

  return seed.filter((item) => new Date(item.at).getTime() <= nowMs)
}

/** Bugungi kun — belgilangan o'tishlardan yig'iladi. */
export function buildToday(marks: AttendanceMarkDto[]): MockDay {
  const dateIso = todayIso()
  const parts = parseIsoDate(dateIso)
  const isWeekend = parts ? parts.weekday >= 6 : false

  const base: MockDay = {
    date: dateIso,
    status: isWeekend ? 'DayOff' : 'Planned',
    workedMinutes: 0,
    lateMinutes: 0,
    earlyLeaveMinutes: 0,
    breakTakenMinutes: 0,
    breakDeductionMinutes: 0,
    firstInAt: null,
    lastOutAt: null,
    planStartAt: isWeekend ? null : tashkentToUtcIso(dateIso, PLAN_START_HOUR, PLAN_START_MINUTE),
    planEndAt: isWeekend ? null : tashkentToUtcIso(dateIso, PLAN_END_HOUR, 0),
    mobileMarks: mobileCount(marks),
    marks,
  }

  if (marks.length === 0) return base

  const sorted = marks.slice().sort((a, b) => Date.parse(a.at) - Date.parse(b.at))
  const firstIn = sorted.filter((item) => item.direction === 'In')[0] ?? null
  const outs = sorted.filter((item) => item.direction === 'Out')
  const lastOut = outs.length > 0 ? outs[outs.length - 1] ?? null : null

  const firstInMs = firstIn ? new Date(firstIn.at).getTime() : 0

  // Pair passes into closed in→out intervals (minutes since Tashkent midnight).
  const dayStartMs = new Date(tashkentToUtcIso(dateIso, 0, 0)).getTime()
  const intervals: Array<[number, number]> = []
  let openAt: number | null = null
  sorted.forEach((item) => {
    const minute = Math.round((new Date(item.at).getTime() - dayStartMs) / 60_000)
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
    breakTakenMinutes: pairing.breakTaken,
    breakDeductionMinutes: pairing.deduction,
    firstInAt: firstIn ? firstIn.at : null,
    lastOutAt: lastOut ? lastOut.at : null,
  }
}

/**
 * So'rovlar. Sanalar bugundan hisoblanadi — ro'yxat hech qachon
 * «o'tgan yilgi» bo'lib qolmaydi.
 */
/** Mock xodimning so'rov yozuvidagi umumiy maydonlari. */
function requestOf(fields: {
  id: string
  absenceTypeId: string
  absenceTypeName: string
  fromDate: string
  toDate: string
  fromTime: string | null
  toTime: string | null
  note: string | null
  status: AbsenceRequestDto['status']
  decisionNote: string | null
  decidedByName: string | null
  decidedAt: string | null
  createdAt: string
}): AbsenceRequestDto {
  return {
    ...fields,
    employeeId: MOCK_ME.employeeId,
    employeeName: MOCK_ME.fullName,
    employeePhotoKey: MOCK_ME.photoKey,
    positionName: MOCK_ME.position,
    departmentName: MOCK_ME.department,
    submittedByEmployee: true,
    cancelledByEmployee: fields.status === 'Cancelled',
  }
}

export function buildRequests(): AbsenceRequestDto[] {
  const today = todayIso()

  return [
    requestOf({
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
    }),
    requestOf({
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
    }),
    requestOf({
      id: 'req-3',
      absenceTypeId: 'at-4',
      absenceTypeName: 'O‘z hisobidan',
      fromDate: addDays(today, -20),
      toDate: addDays(today, -20),
      fromTime: '09:00:00',
      toTime: '13:00:00',
      note: null,
      status: 'Rejected',
      decisionNote: 'Oy yopilayotgan kun — boshqa kunni tanlang.',
      decidedByName: 'Karimova Nodira',
      decidedAt: tashkentToUtcIso(addDays(today, -21), 16, 30),
      createdAt: tashkentToUtcIso(addDays(today, -22), 15, 5),
    }),
    requestOf({
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
    }),
  ]
}

/**
 * Mock jadvali — `buildMonth` dagi bilan bir xil: ish kunlari 08:30–18:00,
 * tanaffus 13:00–14:00, shanba va yakshanba — dam olish kuni.
 */
export function mockPlanOf(dateIso: string): DayPlanDto {
  const parts = parseIsoDate(dateIso)
  const working = parts ? parts.weekday < 6 : false

  return {
    date: dateIso,
    hasSchedule: true,
    isWorking: working,
    startTime: working ? `${pad(PLAN_START_HOUR)}:${pad(PLAN_START_MINUTE)}:00` : null,
    endTime: working ? `${pad(PLAN_END_HOUR)}:00:00` : null,
    breakStart: working ? `${pad(Math.floor(BREAK_WINDOW_START / 60))}:00:00` : null,
    breakEnd: working ? `${pad(Math.floor(BREAK_WINDOW_END / 60))}:00:00` : null,
    breakMinutes: working ? BREAK_MINUTES : 0,
  }
}

/** Mock xodimning jadval so'rovi yozuvidagi umumiy maydonlari. */
export function scheduleRequestOf(fields: {
  id: string
  kind: ScheduleChangeRequestDto['kind']
  days: ScheduleChangeDayDto[]
  note: string | null
  status: ScheduleChangeRequestDto['status']
  decisionNote: string | null
  decidedByName: string | null
  decidedAt: string | null
  createdAt: string
}): ScheduleChangeRequestDto {
  return {
    ...fields,
    employeeId: MOCK_ME.employeeId,
    employeeName: MOCK_ME.fullName,
    employeePhotoKey: MOCK_ME.photoKey,
    positionName: MOCK_ME.position,
    departmentName: MOCK_ME.department,
    submittedByEmployee: true,
    cancelledByEmployee: fields.status === 'Cancelled',
  }
}

/**
 * Jadval so'rovlari: bittasi qaror kutmoqda (tushlik surilgan kun —
 * Verifix'dagi eng ko'p uchraydigan holat), bittasi tasdiqlangan
 * almashtirish.
 */
export function buildScheduleRequests(): ScheduleChangeRequestDto[] {
  const today = todayIso()
  const moved = addDays(today, 2)
  const swapA = addDays(today, -6)
  const swapB = addDays(today, -4)

  return [
    scheduleRequestOf({
      id: 'jsr-1',
      kind: 'Change',
      days: [
        {
          date: moved,
          isWorking: true,
          startTime: '08:30:00',
          endTime: '18:00:00',
          breakStart: '12:20:00',
          breakEnd: '14:20:00',
          swapWith: null,
          breakMinutes: 120,
        },
      ],
      note: 'Juma namoziga boraman.',
      status: 'Pending',
      decisionNote: null,
      decidedByName: null,
      decidedAt: null,
      createdAt: tashkentToUtcIso(addDays(today, -1), 17, 40),
    }),
    scheduleRequestOf({
      id: 'jsr-2',
      kind: 'Swap',
      days: [
        { ...planAsDay(mockPlanOf(swapB)), date: swapA, swapWith: swapB },
        { ...planAsDay(mockPlanOf(swapA)), date: swapB, swapWith: swapA },
      ],
      note: null,
      status: 'Approved',
      decisionNote: null,
      decidedByName: 'Karimova Nodira',
      decidedAt: tashkentToUtcIso(addDays(today, -8), 10, 5),
      createdAt: tashkentToUtcIso(addDays(today, -9), 9, 30),
    }),
  ]
}

/** Reja → so'rov kuni (almashtirishda server shunday nusxa oladi). */
export function planAsDay(plan: DayPlanDto): ScheduleChangeDayDto {
  return {
    date: plan.date,
    isWorking: plan.isWorking,
    startTime: plan.startTime,
    endTime: plan.endTime,
    breakStart: plan.breakStart,
    breakEnd: plan.breakEnd,
    swapWith: null,
    breakMinutes: plan.breakMinutes,
  }
}
