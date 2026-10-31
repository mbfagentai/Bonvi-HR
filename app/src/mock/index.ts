import { ApiError, NoAnswerError } from '@/api/errors'
import type { EmployeeApi } from '@/api/client'
import type {
  AbsenceRequestDto,
  AttendanceMarkDto,
  AttendanceMonthDto,
  CheckInRequest,
  CheckInResult,
  CreateAbsenceRequest,
  CreateScheduleChangeRequest,
  DayPlanDto,
  LoginResult,
  MeDto,
  MyCurrentStateDto,
  PassDirection,
  ScheduleChangeDayDto,
  ScheduleChangeRequestDto,
  TimesheetTotalsDto,
  UpdateScheduleChangeRequest,
} from '@/api/types'
import {
  MIN_REQUIRED_MINUTES,
  breakWindowMinutes,
  maxRequiredMinutes,
  requestBreakProblem,
} from '@/data/schedule-requests'
import { readStorage } from '@/lib/storage'
import { daysBetween, monthKey, todayIso } from '@/lib/time'
import mockAvatarUrl from './avatar.jpg'
import {
  MOCK_ABSENCE_TYPES,
  MOCK_ME,
  MOCK_SITE_NAME,
  type MockPersonalTime,
  buildMonth,
  buildRequests,
  buildScheduleRequests,
  buildToday,
  buildTodaySeed,
  mockPlanOf,
  planAsDay,
  scheduleRequestOf,
  type MockDay,
} from './data'
import { readScenario } from './scenario'
import {
  mockCancelTeamVacancyRequest,
  mockCreateTeamVacancyRequest,
  mockTeamBoard,
  mockTeamHasPhoto,
  mockTeamMemberMonth,
  mockTeamScope,
  mockTeamVacancyRequests,
  type MockTeamResult,
} from './team'

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
  scheduleRequests: ScheduleChangeRequestDto[]
  lastCheckInMs: number | null
  nextRequestNo: number
  nextScheduleRequestNo: number
}

const state: MockState = {
  todayMarks: buildTodaySeed(),
  requests: buildRequests(),
  scheduleRequests: buildScheduleRequests(),
  lastCheckInMs: null,
  nextRequestNo: 7,
  nextScheduleRequestNo: 3,
}

/** Jadval so'rovidagi sanalar soni chegarasi — server bilan bir xil. */
const MAX_SCHEDULE_DAYS = 31
const MAX_SWAP_PAIRS = 15

/** Sananing amaldagi rejasi: tasdiqlangan jadval so'rovi jadval ustidan yoziladi (§C2). */
function planOf(dateIso: string): DayPlanDto {
  const approved = state.scheduleRequests
    .filter((request) => request.status === 'Approved')
    .map((request) => request.days.filter((day) => day.date === dateIso)[0])
    .filter((day): day is ScheduleChangeDayDto => day !== undefined)[0]

  if (!approved) return mockPlanOf(dateIso)

  return {
    date: dateIso,
    hasSchedule: true,
    isWorking: approved.isWorking,
    startTime: approved.startTime,
    endTime: approved.endTime,
    breakStart: approved.breakStart,
    breakEnd: approved.breakEnd,
    breakMinutes: approved.breakMinutes,
    // So'rov kuni rejani TO'LIQ almashtiradi — erkinlik ham undan (SPEC-erkin-jadval §4)
    isFlexible: approved.isFlexible ?? false,
    requiredMinutes: approved.requiredMinutes ?? null,
  }
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

/** Tur ish vaqti hisoblanadimi (`counts_as_work_time`); `false` — shaxsiy tur. */
function countsAsWorkTime(absenceTypeId: string): boolean {
  return MOCK_ABSENCE_TYPES.some((item) => item.id === absenceTypeId && item.countsAsWorkTime === true)
}

/** Tasdiqlangan VAQTLI shaxsiy so'rov — kunni oqlamaydi, faqat soatlarini (SPEC-sorov-tahriri §5.2). */
function isPersonalTime(request: AbsenceRequestDto): boolean {
  return (
    request.status === 'Approved' &&
    request.fromTime !== null &&
    request.toTime !== null &&
    request.fromDate === request.toDate &&
    !countsAsWorkTime(request.absenceTypeId)
  )
}

/** «14:00:00» → 840. */
function minuteOfDay(value: string | null): number {
  return value ? Number(value.slice(0, 2)) * 60 + Number(value.slice(3, 5)) : 0
}

function personalTimes(): MockPersonalTime[] {
  return state.requests.filter(isPersonalTime).map((request) => ({
    date: request.fromDate,
    fromMinute: minuteOfDay(request.fromTime),
    toMinute: minuteOfDay(request.toTime),
  }))
}

/**
 * Tasdiqlangan so'rovlar tushgan kunlar — taqvimda «Sababli» bo'ladi.
 * Vaqtli shaxsiy so'rov bundan mustasno: u kunni emas, soatlarini oqlaydi.
 */
function excusedDates(): string[] {
  const dates: string[] = []

  state.requests
    .filter((request) => request.status === 'Approved' && !isPersonalTime(request))
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

/**
 * Mock xodim boshliqmi (SPEC-jamoam §F). Sukut — ha; boshliq BO'LMAGAN
 * xodimni ko'rish uchun brauzer konsolidan:
 *     localStorage.setItem('bonvi.ilova.boshliq', 'yoq'); location.reload()
 */
function isTeamLead(): boolean {
  return readStorage('bonvi.ilova.boshliq') !== 'yoq'
}

/** Jamoa javobi — boshliq bo'lmasa server kabi 403 `jamoa.boshliq_emas`. */
function teamReply<T>(result: () => MockTeamResult<T>): Promise<T> {
  if (!isTeamLead()) return fail(MIN_DELAY_MS, rejected(403, 'jamoa.boshliq_emas'))

  const reply = result()
  return reply.ok ? delay(reply.value) : fail(MIN_DELAY_MS, rejected(reply.status, reply.code))
}

/**
 * Oy jami — server kabi: kataklardagi rejalar (erkin kunda «Ish vaqti»),
 * ishlangan va «Kam ishlangan» daqiqalar yig'indisi.
 */
function totalsOf(days: readonly MockDay[]): TimesheetTotalsDto {
  return {
    workedMinutes: days.reduce((sum, day) => sum + day.workedMinutes, 0),
    plannedMinutes: days.reduce((sum, day) => sum + (day.plannedMinutes ?? 0), 0),
    presentDays: days.filter((day) => day.status === 'Present').length,
    absentDays: days.filter((day) => day.status === 'Absent').length,
    lateMinutes: days.reduce((sum, day) => sum + day.lateMinutes, 0),
    earlyLeaveMinutes: days.reduce((sum, day) => sum + day.earlyLeaveMinutes, 0),
    shortfallMinutes: days.reduce((sum, day) => sum + (day.shortfallMinutes ?? 0), 0),
    personalMinutes: days.reduce((sum, day) => sum + (day.personalMinutes ?? 0), 0),
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
      isTeamLead: isTeamLead(),
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
    const days = buildMonth(year, month, excusedDates(), personalTimes())

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
    const problem = absenceProblem(request, null)
    const type = MOCK_ABSENCE_TYPES.filter((item) => item.id === request.absenceTypeId)[0]

    if (problem || !type) {
      return fail(MIN_DELAY_MS, problem ?? rejected(404, 'davomat.yoqlik_turi_topilmadi'))
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

  updateRequest: (id, request) => {
    const found = state.requests.filter((item) => item.id === id)[0]

    // Server tartibi (SPEC-sorov-tahriri §3.3): topilmadi → holat → versiya → yaratish qoidalari
    if (!found) {
      return fail(MIN_DELAY_MS, rejected(404, 'davomat.sorov_topilmadi'))
    }
    if (found.status !== 'Pending') {
      return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_qaror_qabul_qilingan'))
    }
    if (request.version !== (found.version ?? 0)) {
      return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_ozgargan'))
    }

    const problem = absenceProblem(request, id)
    const type = MOCK_ABSENCE_TYPES.filter((item) => item.id === request.absenceTypeId)[0]

    if (problem || !type) {
      return fail(MIN_DELAY_MS, problem ?? rejected(404, 'davomat.yoqlik_turi_topilmadi'))
    }

    const updated: AbsenceRequestDto = {
      ...found,
      absenceTypeId: type.id,
      absenceTypeName: type.name,
      fromDate: request.fromDate,
      toDate: request.toDate,
      fromTime: request.fromTime,
      toTime: request.toTime,
      note: request.note,
      version: (found.version ?? 0) + 1,
      editedAt: new Date().toISOString(),
      editedByEmployee: true,
    }
    state.requests = state.requests.map((item) => (item.id === id ? updated : item))

    return delay(updated)
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

  getScheduleRequests: (page, pageSize) => {
    const total = state.scheduleRequests.length
    const start = (page - 1) * pageSize

    return delay({
      items: state.scheduleRequests.slice(start, start + pageSize),
      page,
      pageSize,
      total,
      pageCount: Math.max(1, Math.ceil(total / pageSize)),
    })
  },

  createScheduleRequest: (request) => createScheduleRequest(request),

  updateScheduleRequest: (id, request) => updateScheduleRequest(id, request),

  cancelScheduleRequest: (id) => {
    const found = state.scheduleRequests.filter((item) => item.id === id)[0]

    if (!found) {
      return fail(MIN_DELAY_MS, rejected(404, 'davomat.sorov_topilmadi'))
    }
    if (found.status === 'Cancelled') {
      return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_bekor_qilingan'))
    }
    if (found.status !== 'Pending') {
      return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_qaror_qabul_qilingan'))
    }

    const cancelled: ScheduleChangeRequestDto = {
      ...found,
      status: 'Cancelled',
      decidedAt: new Date().toISOString(),
      cancelledByEmployee: true,
    }
    state.scheduleRequests = state.scheduleRequests.map((item) => (item.id === id ? cancelled : item))

    return delay(undefined)
  },

  getPlan: (date) => delay(planOf(date)),

  getTeam: () => teamReply(() => ({ ok: true, value: mockTeamScope() })),

  getTeamBoard: (query) => teamReply(() => mockTeamBoard(query)),

  getTeamMemberMonth: (employeeId, year, month) => teamReply(() => mockTeamMemberMonth(employeeId, year, month)),

  // Hamma rasmli a'zoga bitta namuna rasm — namoyish uchun yetarli
  getTeamVacancyRequests: (page, pageSize) =>
    teamReply(() => {
      const all = mockTeamVacancyRequests()
      return {
        ok: true,
        value: {
          items: all.slice((page - 1) * pageSize, page * pageSize),
          page,
          pageSize,
          total: all.length,
          pageCount: Math.max(1, Math.ceil(all.length / pageSize)),
        },
      }
    }),

  createTeamVacancyRequest: (request) => teamReply(() => mockCreateTeamVacancyRequest(request)),

  cancelTeamVacancyRequest: (id) => teamReply(() => mockCancelTeamVacancyRequest(id)),

  getTeamMemberPhoto: (employeeId) =>
    isTeamLead() && mockTeamHasPhoto(employeeId)
      ? window
          .fetch(mockAvatarUrl)
          .then((response) => (response.ok ? response.blob() : null))
          .catch(() => null)
      : Promise.resolve(null),
}

/**
 * Yo'qlik so'rovi — yaratish va tahrirdagi umumiy qoidalar: tur, davr, boshqa
 * faol so'rov bilan to'qnashuv (tahrirda so'rovning o'zi hisobga olinmaydi).
 */
function absenceProblem(request: CreateAbsenceRequest, ignoreId: string | null): ApiError | null {
  const type = MOCK_ABSENCE_TYPES.filter((item) => item.id === request.absenceTypeId)[0]

  if (!type) return rejected(404, 'davomat.yoqlik_turi_topilmadi')
  if (request.fromDate > request.toDate) return rejected(400, 'davomat.sorov_davri_notogri')

  const overlaps = state.requests.some(
    (item) =>
      item.id !== ignoreId &&
      (item.status === 'Pending' || item.status === 'Approved') &&
      item.fromDate <= request.toDate &&
      request.fromDate <= item.toDate,
  )

  return overlaps ? rejected(409, 'davomat.sorov_takroriy') : null
}

/**
 * Jadval so'rovi — server qoidalarining mock nusxasi (tartibi ham bir xil):
 * tur, kun yo'q, ko'p, sana takrori, smena va tanaffus, «Ish vaqti»,
 * tanaffus qoidasi, keyin faol so'rov bilan to'qnashuv.
 */
function createScheduleRequest(request: CreateScheduleChangeRequest): Promise<ScheduleChangeRequestDto> {
  const days = scheduleDaysOf(request, null)
  if (days instanceof ApiError) return fail(MIN_DELAY_MS, days)

  const created = scheduleRequestOf({
    id: `jsr-${state.nextScheduleRequestNo}`,
    kind: request.kind,
    days,
    note: request.note,
    status: 'Pending',
    decisionNote: null,
    decidedByName: null,
    decidedAt: null,
    createdAt: new Date().toISOString(),
  })

  state.nextScheduleRequestNo += 1
  state.scheduleRequests = [created].concat(state.scheduleRequests)

  return delay(created)
}

/**
 * Kutayotgan jadval so'rovini o'zgartirish — server tartibi: topilmadi →
 * holat → versiya → yaratishdagi barcha qoidalar (almashtirish kunlari
 * JORIY rejadan qayta olinadi). Versiya +1, tahrir belgisi qo'yiladi.
 */
function updateScheduleRequest(
  id: string,
  request: UpdateScheduleChangeRequest,
): Promise<ScheduleChangeRequestDto> {
  const found = state.scheduleRequests.filter((item) => item.id === id)[0]

  if (!found) {
    return fail(MIN_DELAY_MS, rejected(404, 'davomat.sorov_topilmadi'))
  }
  if (found.status !== 'Pending') {
    return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_qaror_qabul_qilingan'))
  }
  if (request.version !== (found.version ?? 0)) {
    return fail(MIN_DELAY_MS, rejected(409, 'davomat.sorov_ozgargan'))
  }

  const days = scheduleDaysOf(request, id)
  if (days instanceof ApiError) return fail(MIN_DELAY_MS, days)

  const updated: ScheduleChangeRequestDto = {
    ...found,
    kind: request.kind,
    days,
    note: request.note,
    version: (found.version ?? 0) + 1,
    editedAt: new Date().toISOString(),
    editedByEmployee: true,
  }
  state.scheduleRequests = state.scheduleRequests.map((item) => (item.id === id ? updated : item))

  return delay(updated)
}

/** So'rov kunlari (sana tartibida) yoki birinchi buzilgan qoida. */
function scheduleDaysOf(
  request: CreateScheduleChangeRequest,
  ignoreId: string | null,
): ScheduleChangeDayDto[] | ApiError {
  if (request.kind !== 'Change' && request.kind !== 'Swap') {
    return rejected(400, 'davomat.jadval_sorovi_turi')
  }

  if (request.kind === 'Swap' && request.swaps.some((pair) => pair.date === pair.withDate)) {
    return rejected(400, 'davomat.jadval_sorovi_bir_xil_sana')
  }

  const days: ScheduleChangeDayDto[] =
    request.kind === 'Change'
      ? request.days.map((day) => {
          // Server qoidasi: berilmagan uzunlik — oynaning o'zi, dam olish kunida 0
          const breakMinutes = day.isWorking ? (day.breakMinutes ?? breakWindowMinutes(day.breakStart, day.breakEnd)) : 0
          // Erkinlik faqat ish kunida; berilmagan «Ish vaqti» — sukut (smena − tanaffus)
          const flexible = day.isWorking && day.isFlexible
          const required =
            flexible && day.startTime && day.endTime
              ? (day.requiredMinutes ?? maxRequiredMinutes(day.startTime, day.endTime, breakMinutes))
              : null

          return {
            ...day,
            swapWith: null,
            breakMinutes,
            isFlexible: flexible,
            requiredMinutes: required,
          }
        })
      : request.swaps.reduce<ScheduleChangeDayDto[]>(
          (list, pair) =>
            list.concat([
              { ...planAsDay(planOf(pair.withDate)), date: pair.date, swapWith: pair.withDate },
              { ...planAsDay(planOf(pair.date)), date: pair.withDate, swapWith: pair.date },
            ]),
          [],
        )

  const limit = request.kind === 'Swap' ? MAX_SWAP_PAIRS * 2 : MAX_SCHEDULE_DAYS

  if (days.length === 0) {
    return rejected(400, 'davomat.jadval_sorovi_kun_yoq')
  }
  if (days.length > limit) {
    return rejected(400, 'davomat.jadval_sorovi_kop')
  }

  const dates = days.map((day) => day.date)
  if (dates.some((date, index) => dates.indexOf(date) !== index)) {
    return rejected(400, 'davomat.jadval_sorovi_sana_takror')
  }

  const working = days.filter((day) => day.isWorking)
  if (working.some((day) => !day.startTime || !day.endTime)) {
    return rejected(400, 'davomat.jadval_vaqt_bosh')
  }
  if (working.some((day) => day.startTime === day.endTime)) {
    return rejected(400, 'davomat.jadval_vaqt_teng')
  }
  if (working.some((day) => (day.breakStart === null) !== (day.breakEnd === null))) {
    return rejected(400, 'davomat.jadval_tanaffus_toliq_emas')
  }
  if (
    working.some((day) => {
      if (!day.isFlexible || day.requiredMinutes === undefined || day.requiredMinutes === null) return false
      const max = maxRequiredMinutes(day.startTime ?? '', day.endTime ?? '', day.breakMinutes)
      return day.requiredMinutes < MIN_REQUIRED_MINUTES || day.requiredMinutes > max
    })
  ) {
    return rejected(400, 'davomat.jadval_erkin_vaqti')
  }

  // Tanaffus qoidasi (SPEC-sorov-tahriri §4) — faqat «Kunni o'zgartirish» da;
  // almashtirishda kunni server jadvaldan oladi, xodim tanaffus tanlamaydi
  const rule = MOCK_ME.requestBreakRule ?? null
  const breakProblem =
    request.kind === 'Change' && rule
      ? days.map((day) => requestBreakProblem(day, rule)).filter((problem) => problem !== null)[0] ?? null
      : null
  if (breakProblem) {
    return rejected(
      400,
      breakProblem === 'window'
        ? 'davomat.jadval_sorovi_tanaffus_oraligi'
        : 'davomat.jadval_sorovi_tanaffus_davomiyligi',
    )
  }

  const taken = state.scheduleRequests.some(
    (item) =>
      item.id !== ignoreId &&
      (item.status === 'Pending' || item.status === 'Approved') &&
      item.days.some((day) => dates.indexOf(day.date) >= 0),
  )
  if (taken) {
    return rejected(409, 'davomat.jadval_sorovi_takroriy')
  }

  return days.slice().sort((a, b) => a.date.localeCompare(b.date))
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
