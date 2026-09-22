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
} from '@/api/types'
import { daysBetween, monthKey, todayIso } from '@/lib/time'
import {
  MOCK_ABSENCE_TYPES,
  MOCK_ME,
  MOCK_SITE_NAME,
  buildMonth,
  buildRequests,
  buildToday,
} from './data'
import { readScenario } from './scenario'

/**
 * Mock server.
 *
 * ⚠️ Bu papka VAQTINCHA. Haqiqiy mijoz kelganda `api/client.ts` dagi
 * bitta qator almashadi va `src/mock/` o'chadi — shu sababli bu yerda
 * ekranlarga tegishli hech qanday qoida yo'q, faqat javob shakllari.
 *
 * Javoblar ataylab SEKIN (400–800 ms): skelet, «Yuborilmoqda…» va fon
 * yangilanishi kabi holatlarni tez tarmoqda umuman ko'rib bo'lmaydi.
 */
const MIN_DELAY_MS = 400
const MAX_DELAY_MS = 800

/** Ikki belgi orasidagi eng kam vaqt — `AttendanceRules.BounceSeconds`. */
const BOUNCE_SECONDS = 60

interface MockState {
  todayMarks: AttendanceMarkDto[]
  requests: AbsenceRequestDto[]
  lastCheckInMs: number | null
  nextRequestNo: number
}

const state: MockState = {
  todayMarks: [],
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

export const mockApi: EmployeeApi = {
  login: (request) => {
    if (!request.login) {
      return fail(MIN_DELAY_MS, new ApiError('rejected', { code: 'auth.login_bosh', status: 400 }))
    }
    if (!request.password) {
      return fail(MIN_DELAY_MS, new ApiError('rejected', { code: 'auth.parol_bosh', status: 400 }))
    }
    // Parol «1111» — HR bergan bir martalik parol: undan keyin ilova
    // parol o'rnatish ekranidan boshqa joyga o'tkazmaydi.
    if (request.password.length < 4) {
      return fail(
        MAX_DELAY_MS,
        new ApiError('rejected', { code: 'auth.notogri_malumot', status: 401 }),
      )
    }

    const result: LoginResult = {
      accessToken: `mock-access-${Date.now()}`,
      refreshToken: `mock-refresh-${Date.now()}`,
      mustChangePassword: request.password === '1111',
    }

    return delay(result)
  },

  changePassword: (request) => {
    if (request.newPassword.length < 8) {
      return fail(
        MIN_DELAY_MS,
        new ApiError('rejected', { code: 'hisob.parol_qisqa', status: 400 }),
      )
    }

    return delay(undefined)
  },

  getMe: () => {
    const me: MeDto = {
      ...MOCK_ME,
      mobileCheckInAllowed: mobileAllowed(),
      absenceTypes: MOCK_ABSENCE_TYPES,
    }

    return delay(me)
  },

  getMonth: (year, month) => {
    const today = todayIso()
    const days = buildMonth(year, month, excusedDates())

    // Bugungi kun alohida yig'iladi: u belgilash holatiga bog'liq
    if (today.indexOf(monthKey(year, month)) === 0) {
      days.push(buildToday(state.todayMarks))
      days.sort((a, b) => a.date.localeCompare(b.date))
    }

    const result: AttendanceMonthDto = { year, month, days }
    return delay(result)
  },

  checkIn: (request: CheckInRequest) => {
    const scenario = readScenario()

    if (scenario === 'ruxsatyoq') {
      return fail(
        MIN_DELAY_MS,
        new ApiError('rejected', { code: 'davomat.mobil_ruxsat_yoq', status: 403 }),
      )
    }

    if (scenario === 'aniqemas') {
      return fail(
        MAX_DELAY_MS,
        new ApiError('rejected', {
          code: 'davomat.joylashuv_aniq_emas',
          status: 400,
          details: { accuracyMeters: Math.round(request.accuracyMeters) },
        }),
      )
    }

    if (scenario === 'uzoq') {
      return fail(
        MAX_DELAY_MS,
        new ApiError('rejected', {
          code: 'davomat.obyektdan_uzoq',
          status: 400,
          details: { siteName: MOCK_SITE_NAME, distanceMeters: 3420 },
        }),
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
        new ApiError('rejected', {
          code: 'davomat.takroriy_belgi',
          status: 409,
          details: { occurredAt: last ? last.occurredAt : new Date(nowMs).toISOString() },
        }),
      )
    }

    const accepted = pushMark(request)
    state.lastCheckInMs = nowMs

    const result: CheckInResult = {
      occurredAt: accepted.occurredAt,
      direction: accepted.direction,
      workSiteName: MOCK_SITE_NAME,
    }

    return delay(result)
  },

  getRequests: () => delay(state.requests.slice()),

  createRequest: (request: CreateAbsenceRequest) => {
    const type = MOCK_ABSENCE_TYPES.filter((item) => item.id === request.absenceTypeId)[0]

    if (!type) {
      return fail(MIN_DELAY_MS, new ApiError('rejected', { code: 'sorov.turi_bosh', status: 400 }))
    }

    const created: AbsenceRequestDto = {
      id: `req-${state.nextRequestNo}`,
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
    }

    state.nextRequestNo += 1
    state.requests = [created].concat(state.requests)

    return delay(created)
  },

  cancelRequest: (id) => {
    const found = state.requests.filter((item) => item.id === id)[0]

    if (!found) {
      return fail(MIN_DELAY_MS, new ApiError('rejected', { code: 'sorov.topilmadi', status: 404 }))
    }
    if (found.status !== 'Pending') {
      return fail(
        MIN_DELAY_MS,
        new ApiError('rejected', { code: 'sorov.holat_mos_emas', status: 409 }),
      )
    }

    const cancelled: AbsenceRequestDto = { ...found, status: 'Cancelled' }
    state.requests = state.requests.map((item) => (item.id === id ? cancelled : item))

    return delay(cancelled)
  },
}

/** Qabul qilingan belgi — vaqti SERVERNIKI (bu yerda mock'niki). */
function pushMark(request: CheckInRequest): AttendanceMarkDto {
  const accepted: AttendanceMarkDto = {
    occurredAt: new Date().toISOString(),
    direction: request.direction === 'Out' ? 'Out' : 'In',
    source: 'Mobile',
    siteName: MOCK_SITE_NAME,
  }

  state.todayMarks = state.todayMarks.concat([accepted])
  return accepted
}
