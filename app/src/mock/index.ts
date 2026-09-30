import { ApiError, NoAnswerError } from '@/api/errors'
import type { EmployeeApi } from '@/api/client'
import type {
  AbsenceRequestDto,
  AttendanceMarkDto,
  AttendanceMonthDto,
  CheckInRequest,
  CheckInResult,
  CreateAbsenceRequest,
  LoginResult,
  MeDto,
  MyCurrentStateDto,
  PassDirection,
  TimesheetTotalsDto,
} from '@/api/types'
import { daysBetween, monthKey, todayIso } from '@/lib/time'
import mockAvatarUrl from './avatar.jpg'
import {
  MOCK_ABSENCE_TYPES,
  MOCK_ME,
  MOCK_SITE_NAME,
  buildMonth,
  buildRequests,
  buildToday,
  buildTodaySeed,
  type MockDay,
} from './data'
import { readScenario } from './scenario'

/**
 * Mock server — namoyish va qo'lda sinash uchun (`VITE_API_MODE=mock`).
 *
 * ⚠️ Javob shakllari va xato KODLARI haqiqiy server bilan AYNAN bir xil
 * (`api/types.ts`, backend `*Errors`): mock'da ishlagan ekran serverda
 * ham ishlashi kerak. Bu yerda ekranlarga tegishli hech qanday qoida yo'q.
 *
 * Javoblar ataylab SEKIN (400–800 ms): skelet, «Yuborilmoqda…» va fon
 * yangilanishi kabi holatlarni tez tarmoqda umuman ko'rib bo'lmaydi.
 */
const MIN_DELAY_MS = 400
const MAX_DELAY_MS = 800

/** Ikki belgi orasidagi eng kam vaqt — `AttendanceRules.BounceSeconds`. */
const BOUNCE_SECONDS = 60

/** Mock login phone (local 9 digits). */
const MOCK_PHONE = '976662675'

/** Mock parol — HR beradigan 6 xonali parol kabi. */
const MOCK_PASSWORD = '482915'

interface MockState {
  todayMarks: AttendanceMarkDto[]
  requests: AbsenceRequestDto[]
  lastCheckInMs: number | null
  nextRequestNo: number
}

const state: MockState = {
  todayMarks: buildTodaySeed(),
  requests: buildRequests(),
  lastCheckInMs: null,
  nextRequestNo: 5,
}

function delay<T>(value: T): Promise<T> {
  const ms = MIN_DELAY_MS + Math.round(Math.random() * (MAX_DELAY_MS - MIN_DELAY_MS))
  return new Promise((resolve) => window.setTimeout(() => resolve(value), ms))
}

function fail(ms: number, error: ApiError): Promise<never> {
  return new Promise((_resolve, reject) => window.setTimeout(() => reject(error), ms))
}

function rejected(status: number, code: string, details?: Record<string, string | number>): ApiError {
  return new ApiError('rejected', { code, status, details })
}

function tokenPair(): LoginResult {
  const now = Date.now()

  return {
    accessToken: `mock-access-${now}`,
    expiresAt: new Date(now + 15 * 60_000).toISOString(),
    refreshToken: `mock-refresh-${now}`,
  }
}

/** Tasdiqlangan so'rovlar tushgan kunlar — taqvimda «Sababli» bo'ladi. */
function excusedDates(): string[] {
  const dates: string[] = []

  state.requests
    .filter((request) => request.status === 'Approved')
    .forEach((request) => {
      const count = daysBetween(request.fromDate, request.toDate)
      for (let index = 0; index < count; index += 1) {
        const moved = new Date(`${request.fromDate}T00:00:00Z`)
        moved.setUTCDate(moved.getUTCDate() + index)
        dates.push(moved.toISOString().slice(0, 10))
      }
    })

  return dates
}

function mobileAllowed(): boolean {
  return readScenario() !== 'ruxsatyoq'
}

/** Oy jami — server kabi: rejalar va ishlangan daqiqalar yig'indisi. */
function totalsOf(days: readonly MockDay[]): TimesheetTotalsDto {
  return {
    workedMinutes: days.reduce((sum, day) => sum + day.workedMinutes, 0),
    plannedMinutes: days.reduce((sum, day) => {
      if (!day.planStartAt || !day.planEndAt) return sum
      const planned = Math.round((Date.parse(day.planEndAt) - Date.parse(day.planStartAt)) / 60_000)
      return sum + Math.max(0, planned - 60)
    }, 0),
    presentDays: days.filter((day) => day.status === 'Present').length,
    absentDays: days.filter((day) => day.status === 'Absent').length,
    lateMinutes: days.reduce((sum, day) => sum + day.lateMinutes, 0),
    earlyLeaveMinutes: days.reduce((sum, day) => sum + day.earlyLeaveMinutes, 0),
  }
}

/** «Hozir» — server kabi: oxirgi belgidan keyingi yo'nalish. */
function currentOf(): MyCurrentStateDto {
  const sorted = state.todayMarks.slice().sort((a, b) => Date.parse(a.at) - Date.parse(b.at))
  const last = sorted[sorted.length - 1] ?? null
  const next: PassDirection = last && last.direction === 'In' ? 'Out' : 'In'

  return { date: todayIso(), nextDirection: next, lastMark: last }
}

export const mockApi: EmployeeApi = {
  login: (request) => {
    // Mock credentials: phone +998 97 666 26 75 (any formatting, with or
    // without the country code) / password 482915 — HR beradigan 6 xonali
    // parol kabi. Parolni xodim almashtirmaydi. Server kabi: bo'sh,
    // noto'g'ri yoki notanish — hammasi BITTA javob (`xodim.notogri_malumot`, 400).
    const digits = request.login.replace(/\D/g, '')
    const phoneOk = digits === MOCK_PHONE || digits === `998${MOCK_PHONE}`

    if (!phoneOk || request.password !== MOCK_PASSWORD) {
      return fail(MAX_DELAY_MS, rejected(400, 'xodim.notogri_malumot'))
    }

    return delay(tokenPair())
  },

  refresh: () => delay(tokenPair()),

  logout: () => delay(undefined),

  getMe: () => {
    const me: MeDto = {
      ...MOCK_ME,
      mobileCheckInAllowed: mobileAllowed(),
      absenceTypes: MOCK_ABSENCE_TYPES,
    }

    return delay(me)
  },

  getPhoto: () =>
    window
      .fetch(mockAvatarUrl)
      .then((response) => (response.ok ? response.blob() : null))
      .catch(() => null),

  getMonth: (year, month) => {
    const today = todayIso()
    const days = buildMonth(year, month, excusedDates())

    // Bugungi kun alohida yig'iladi: u belgilash holatiga bog'liq
    if (today.indexOf(monthKey(year, month)) === 0) {
      days.push(buildToday(state.todayMarks))
      days.sort((a, b) => a.date.localeCompare(b.date))
    }

    const result: AttendanceMonthDto = {
      year,
      month,
      days: days.map(({ marks, ...day }) => ({ day, marks })),
      totals: totalsOf(days),
      current: currentOf(),
    }

    return delay(result)
  },

  checkIn: (request: CheckInRequest) => {
    const scenario = readScenario()

    if (scenario === 'ruxsatyoq') {
      return fail(MIN_DELAY_MS, rejected(403, 'davomat.mobil_ruxsat_yoq'))
    }

    if (scenario === 'aniqemas') {
      return fail(
        MAX_DELAY_MS,
        rejected(400, 'davomat.joylashuv_aniq_emas', {
          accuracyMeters: Math.round(request.accuracyMeters),
          maxAccuracyMeters: 100,
        }),
      )
    }

    if (scenario === 'uzoq') {
      return fail(
        MAX_DELAY_MS,
        rejected(400, 'davomat.obyektdan_uzoq', { siteName: MOCK_SITE_NAME, distanceMeters: 3420 }),
      )
    }

    if (scenario === 'server') {
      return fail(MAX_DELAY_MS, new ApiError('server', { status: 500 }))
    }

    if (scenario === 'javobyoq') {
      /*
        Javob yo'lda yo'qoldi, LEKIN belgi serverda yaratildi — UX §C.5
        dagi yagona noaniq holat aynan shu. Mock uni rostakamiga
        takrorlaydi: o'tish ro'yxatga qo'shiladi, javob esa kelmaydi.
        «Holatni tekshirish» tugmasi shundan keyin belgini topishi kerak.
      */
      pushMark(request)
      return fail(MAX_DELAY_MS, new NoAnswerError())
    }

    const nowMs = Date.now()
    const sinceLast = state.lastCheckInMs === null ? Infinity : (nowMs - state.lastCheckInMs) / 1000

    if (scenario === 'takror' || sinceLast < BOUNCE_SECONDS) {
      const last = state.todayMarks[state.todayMarks.length - 1]
      return fail(
        MIN_DELAY_MS,
        rejected(409, 'davomat.takroriy_belgi', {
          lastAt: last ? last.at : new Date(nowMs).toISOString(),
        }),
      )
    }

    const accepted = pushMark(request)
    state.lastCheckInMs = nowMs

    const result: CheckInResult = {
      occurredAt: accepted.at,
      direction: accepted.direction,
      workSiteName: MOCK_SITE_NAME,
    }

    return delay(result)
  },

  getRequests: (page, pageSize) => {
    const total = state.requests.length
    const start = (page - 1) * pageSize

    return delay({
      items: state.requests.slice(start, start + pageSize),
      page,
      pageSize,
      total,
      pageCount: Math.max(1, Math.ceil(total / pageSize)),
    })
  },

  createRequest: (request: CreateAbsenceRequest) => {
    const type = MOCK_ABSENCE_TYPES.filter((item) => item.id === request.absenceTypeId)[0]

    if (!type) {
      return fail(MIN_DELAY_MS, rejected(404, 'davomat.yoqlik_turi_topilmadi'))
    }
    if (request.fromDate > request.toDate) {
      return fail(MIN_DELAY_MS, rejected(400, 'davomat.sorov_davri_notogri'))
    }

    const overlaps = state.requests.some(
      (item) =>
        (item.status === 'Pending' || item.status === 'Approved') &&
        item.fromDate <= request.toDate &&
        request.fromDate <= item.toDate,
    )

    if (overlaps) {
      return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_takroriy'))
    }

    const created: AbsenceRequestDto = {
      id: `req-${state.nextRequestNo}`,
      employeeId: MOCK_ME.employeeId,
      employeeName: MOCK_ME.fullName,
      employeePhotoKey: MOCK_ME.photoKey,
      positionName: MOCK_ME.position,
      departmentName: MOCK_ME.department,
      absenceTypeId: type.id,
      absenceTypeName: type.name,
      fromDate: request.fromDate,
      toDate: request.toDate,
      fromTime: request.fromTime,
      toTime: request.toTime,
      note: request.note,
      status: 'Pending',
      decisionNote: null,
      decidedByName: null,
      decidedAt: null,
      createdAt: new Date().toISOString(),
      submittedByEmployee: true,
      cancelledByEmployee: false,
    }

    state.nextRequestNo += 1
    state.requests = [created].concat(state.requests)

    return delay(created)
  },

  cancelRequest: (id) => {
    const found = state.requests.filter((item) => item.id === id)[0]

    if (!found) {
      return fail(MIN_DELAY_MS, rejected(404, 'davomat.sorov_topilmadi'))
    }
    if (found.status === 'Cancelled') {
      return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_bekor_qilingan'))
    }
    if (found.status !== 'Pending') {
      return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_qaror_qabul_qilingan'))
    }

    const cancelled: AbsenceRequestDto = {
      ...found,
      status: 'Cancelled',
      decidedAt: new Date().toISOString(),
      cancelledByEmployee: true,
    }
    state.requests = state.requests.map((item) => (item.id === id ? cancelled : item))

    return delay(undefined)
  },
}

/** Qabul qilingan belgi — vaqti SERVERNIKI (bu yerda mock'niki). */
function pushMark(request: CheckInRequest): AttendanceMarkDto {
  const accepted: AttendanceMarkDto = {
    at: new Date().toISOString(),
    direction: request.direction === 'Out' ? 'Out' : 'In',
    source: 'Mobile',
    siteName: MOCK_SITE_NAME,
  }

  state.todayMarks = state.todayMarks.concat([accepted])
  return accepted
}
