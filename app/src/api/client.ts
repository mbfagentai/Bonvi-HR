import { mockApi } from '@/mock'

import { realApi } from './real'
import type {
  AbsenceRequestDto,
  AttendanceMonthDto,
  ChangePasswordRequest,
  CheckInRequest,
  CheckInResult,
  CreateAbsenceRequest,
  LoginRequest,
  LoginResult,
  MeDto,
  PagedResult,
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
  /**
   * `POST /api/employee-auth/password` — javobda YANGI juftlik: server
   * barcha eski sessiyalarni yopadi, ya'ni eski yangilash tokeni o'ldi.
   */
  changePassword: (request: ChangePasswordRequest) => Promise<LoginResult>
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
  /** `POST /api/me/requests/{id}/cancel` — 204, tanasiz. */
  cancelRequest: (id: string) => Promise<void>
}

/** Qaysi amalga oshirish ulangan — ekranlar faqat sinov belgisi uchun o'qiydi. */
export const API_MODE: 'real' | 'mock' = import.meta.env.VITE_API_MODE === 'mock' ? 'mock' : 'real'

export const api: EmployeeApi = API_MODE === 'mock' ? mockApi : realApi
