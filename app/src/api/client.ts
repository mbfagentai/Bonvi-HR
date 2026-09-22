import { mockApi } from '@/mock'
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
} from './types'

/**
 * ⭐ ILOVANING YAGONA MA'LUMOT CHOKI.
 *
 * Har bir ekran ma'lumotni SHU interfeys orqali oladi va boshqa hech
 * qaysi yo'l bilan olmaydi. Keyingi to'lqinda pastdagi bitta qator
 * haqiqiy HTTP mijoziga almashadi — ekranlarning birortasi
 * o'zgarmaydi. Agar biror ekran `mock/` dan to'g'ridan-to'g'ri o'qisa,
 * o'sha almashtirish bitta qator bo'lmay qolardi.
 */
export interface EmployeeApi {
  /** `POST /api/employee-auth/login` */
  login: (request: LoginRequest) => Promise<LoginResult>
  /** `POST /api/employee-auth/password` */
  changePassword: (request: ChangePasswordRequest) => Promise<void>
  /** `GET /api/me` */
  getMe: () => Promise<MeDto>
  /** `GET /api/me/attendance?year&month` */
  getMonth: (year: number, month: number) => Promise<AttendanceMonthDto>
  /** `POST /api/me/check-in` */
  checkIn: (request: CheckInRequest) => Promise<CheckInResult>
  /** `GET /api/me/requests` */
  getRequests: () => Promise<AbsenceRequestDto[]>
  /** `POST /api/me/requests` */
  createRequest: (request: CreateAbsenceRequest) => Promise<AbsenceRequestDto>
  /** `POST /api/me/requests/{id}/cancel` */
  cancelRequest: (id: string) => Promise<AbsenceRequestDto>
}

export const api: EmployeeApi = mockApi
