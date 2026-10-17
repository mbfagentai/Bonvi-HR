import type { EmployeeApi } from './client'
import { ApiError } from './errors'
import { request } from './http'
import type {
  AbsenceRequestDto,
  AttendanceMonthDto,
  CheckInResult,
  DayPlanDto,
  LoginResult,
  MeDto,
  PagedResult,
  ScheduleChangeRequestDto,
} from './types'

/**
 * `EmployeeApi` ning HAQIQIY server bilan amalga oshirilishi.
 *
 * Bu yerda hech qanday qoida yo'q — faqat yo'l, metod va tana. Token,
 * yangilash va xato tarjimasi `http.ts` da; ekranga qulay shakl `data/` da.
 */
export const realApi: EmployeeApi = {
  login: (body) =>
    request<LoginResult>('/api/employee-auth/login', { method: 'POST', body, auth: false }),

  refresh: (refreshToken) =>
    request<LoginResult>('/api/employee-auth/refresh', {
      method: 'POST',
      body: { refreshToken },
      auth: false,
    }),

  logout: (refreshToken) =>
    request<void>('/api/employee-auth/logout', { method: 'POST', body: { refreshToken }, auth: false }),

  getMe: () => request<MeDto>('/api/me'),

  getPhoto: () =>
    request<Blob | null>('/api/me/photo', { as: 'blob' }).catch((cause: unknown) => {
      // Rasmsiz xodim — odatiy hol (`xodim.rasm_yoq`, 404), xato emas.
      if (cause instanceof ApiError && cause.status === 404) return null
      throw cause
    }),

  getMonth: (year, month) =>
    request<AttendanceMonthDto>(`/api/me/attendance?year=${year}&month=${month}`),

  checkIn: (body) =>
    request<CheckInResult>('/api/me/check-in', { method: 'POST', body, uncertainOnFailure: true }),

  getRequests: (page, pageSize) =>
    request<PagedResult<AbsenceRequestDto>>(`/api/me/requests?page=${page}&pageSize=${pageSize}`),

  createRequest: (body) =>
    request<AbsenceRequestDto>('/api/me/requests', { method: 'POST', body }),

  cancelRequest: (id) =>
    request<void>(`/api/me/requests/${encodeURIComponent(id)}/cancel`, { method: 'POST' }),

  getScheduleRequests: (page, pageSize) =>
    request<PagedResult<ScheduleChangeRequestDto>>(
      `/api/me/schedule-requests?page=${page}&pageSize=${pageSize}`,
    ),

  createScheduleRequest: (body) =>
    request<ScheduleChangeRequestDto>('/api/me/schedule-requests', { method: 'POST', body }),

  cancelScheduleRequest: (id) =>
    request<void>(`/api/me/schedule-requests/${encodeURIComponent(id)}/cancel`, { method: 'POST' }),

  getPlan: (date) => request<DayPlanDto>(`/api/me/plan?date=${encodeURIComponent(date)}`),
}
