import { api } from '@/api/client'
import type { AbsenceRequestDto, AttendanceMonthDto, MeDto } from '@/api/types'
import { invalidateQuery, useQuery, type QueryResult } from '@/lib/use-query'
import { monthKey } from '@/lib/time'

/**
 * Ekranlar uchun o'qish nuqtalari.
 *
 * Har bir ekran ma'lumotni SHU yerdan oladi — `api` ni to'g'ridan-to'g'ri
 * chaqirmaydi. Shunda kesh kaliti, bekor qilish qoidasi va qayta
 * so'rash bitta joyda turadi.
 */
export function useMe(): QueryResult<MeDto> {
  return useQuery('me', () => api.getMe())
}

export function useMonth(year: number, month: number): QueryResult<AttendanceMonthDto> {
  return useQuery(`oy:${monthKey(year, month)}`, () => api.getMonth(year, month))
}

export function useRequests(): QueryResult<AbsenceRequestDto[]> {
  return useQuery('sorovlar', () => api.getRequests())
}

/**
 * Yozuvdan keyin nimani qayta o'qish kerak.
 *
 * Belgilash va so'rov — ikkalasi ham KUNGA tegadi, shu sababli oylar
 * keshi ham tozalanadi: aks holda taqvim eski kunni ko'rsatib turardi
 * va xodim belgisi «yo'qoldi» deb o'ylardi.
 */
export function invalidateAttendance(): void {
  invalidateQuery('oy')
}

export function invalidateRequests(): void {
  invalidateQuery('sorovlar')
  invalidateQuery('oy')
}

export function invalidateMe(): void {
  invalidateQuery('me')
}
