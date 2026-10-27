import { mockApi } from '@/mock'

import { realApi } from './real'
import type {
  AbsenceRequestDto,
  AttendanceMonthDto,
  CheckInRequest,
  CheckInResult,
  CreateAbsenceRequest,
  CreateScheduleChangeRequest,
  DayPlanDto,
  LoginRequest,
  LoginResult,
  MeDto,
  PagedResult,
  ScheduleChangeRequestDto,
  TeamBoardDto,
  TeamBoardQuery,
  TeamMemberAttendanceDto,
  TeamScopeDto,
  UpdateAbsenceRequest,
  UpdateScheduleChangeRequest,
} from './types'

/**
 * ⭐ ILOVANING YAGONA MA'LUMOT CHOKI.
 *
 * Har bir ekran ma'lumotni SHU interfeys orqali oladi va boshqa hech
 * qaysi yo'l bilan olmaydi. Ikkita amalga oshirish bor va ekranlarning
 * birortasi qaysi biri ulanganini bilmaydi:
 *
 *  · `real` — haqiqiy server (`api/real.ts`), SUKUT bo'yicha;
 *  · `mock` — namoyish va qo'lda sinash uchun (`src/mock/`), faqat
 *    `VITE_API_MODE=mock` bilan yig'ilganda.
 *
 * Ikkalasi ham shu interfeysga bog'langan: mock server shartnomasidan
 * chetga chiqsa, kompilyator buni ushlaydi.
 */
export interface EmployeeApi {
  /** `POST /api/employee-auth/login` */
  login: (request: LoginRequest) => Promise<LoginResult>
  /** `POST /api/employee-auth/refresh` — eski token bekor bo'ladi, yangi juftlik qaytadi. */
  refresh: (refreshToken: string) => Promise<LoginResult>
  /** `POST /api/employee-auth/logout` — shu qurilmaning sessiyasini serverda yopadi (204). */
  logout: (refreshToken: string) => Promise<void>
  /** `GET /api/me` */
  getMe: () => Promise<MeDto>
  /** `GET /api/me/photo` — o'z rasmi; rasm yo'q bo'lsa `null`. */
  getPhoto: () => Promise<Blob | null>
  /** `GET /api/me/attendance?year&month` */
  getMonth: (year: number, month: number) => Promise<AttendanceMonthDto>
  /** `POST /api/me/check-in` */
  checkIn: (request: CheckInRequest) => Promise<CheckInResult>
  /** `GET /api/me/requests?page&pageSize` — bitta sahifa. */
  getRequests: (page: number, pageSize: number) => Promise<PagedResult<AbsenceRequestDto>>
  /** `POST /api/me/requests` */
  createRequest: (request: CreateAbsenceRequest) => Promise<AbsenceRequestDto>
  /**
   * `PUT /api/me/requests/{id}` — qaror kutayotgan o'z so'rovini o'zgartiradi
   * (SPEC-sorov-tahriri §3). 409 `davomat.sorov_ozgargan` — ko'rsatilgan
   * versiya eskirgan; 409 `davomat.sorov_qaror_qabul_qilingan` — endi kutmayapti.
   */
  updateRequest: (id: string, request: UpdateAbsenceRequest) => Promise<AbsenceRequestDto>
  /** `POST /api/me/requests/{id}/cancel` — 204, tanasiz. */
  cancelRequest: (id: string) => Promise<void>
  /** `GET /api/me/schedule-requests?page&pageSize` — jadval so'rovlari, bitta sahifa. */
  getScheduleRequests: (page: number, pageSize: number) => Promise<PagedResult<ScheduleChangeRequestDto>>
  /** `POST /api/me/schedule-requests` */
  createScheduleRequest: (request: CreateScheduleChangeRequest) => Promise<ScheduleChangeRequestDto>
  /** `PUT /api/me/schedule-requests/{id}` — yo'qlik so'rovidagi qoidaning o'zi. */
  updateScheduleRequest: (id: string, request: UpdateScheduleChangeRequest) => Promise<ScheduleChangeRequestDto>
  /** `POST /api/me/schedule-requests/{id}/cancel` — 204, tanasiz. */
  cancelScheduleRequest: (id: string) => Promise<void>
  /** `GET /api/me/plan?date=YYYY-MM-DD` — sananing amaldagi rejasi. */
  getPlan: (date: string) => Promise<DayPlanDto>
  /**
   * `GET /api/me/team` — boshliqning qamrovi (SPEC-jamoam §C1). Boshliq
   * bo'lmasa 403 `jamoa.boshliq_emas`. Qamrovni SERVER hisoblaydi — ilova
   * «qaysi bo'limlarni ko'raman» degan narsani hech qachon yubormaydi.
   */
  getTeam: () => Promise<TeamScopeDto>
  /** `GET /api/me/team/board?date&departmentId&status&search&page&pageSize` — bitta sahifa. */
  getTeamBoard: (query: TeamBoardQuery) => Promise<TeamBoardDto>
  /** `GET /api/me/team/members/{id}/attendance?year&month` — 404 `jamoa.xodim_topilmadi`. */
  getTeamMemberMonth: (employeeId: string, year: number, month: number) => Promise<TeamMemberAttendanceDto>
  /** `GET /api/me/team/members/{id}/photo` — rasm yo'q bo'lsa `null`. */
  getTeamMemberPhoto: (employeeId: string) => Promise<Blob | null>
}

/** Qaysi amalga oshirish ulangan — ekranlar faqat sinov belgisi uchun o'qiydi. */
export const API_MODE: 'real' | 'mock' = import.meta.env.VITE_API_MODE === 'mock' ? 'mock' : 'real'

export const api: EmployeeApi = API_MODE === 'mock' ? mockApi : realApi
