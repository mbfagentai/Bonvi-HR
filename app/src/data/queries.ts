import { api } from '@/api/client'
import type { AbsenceRequestDto, MeDto } from '@/api/types'
import { invalidateQuery, useQuery, type QueryResult } from '@/lib/use-query'
import { monthKey } from '@/lib/time'
import { toMonth, type AttendanceMonth } from './attendance'

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

export function useMonth(year: number, month: number): QueryResult<AttendanceMonth> {
  return useQuery(`oy:${monthKey(year, month)}`, () => api.getMonth(year, month).then(toMonth))
}

export function useRequests(): QueryResult<AbsenceRequestDto[]> {
  return useQuery('sorovlar', fetchAllRequests)
}

/** Bitta so'rovdagi sahifa hajmi (server chegarasi — 200). */
const REQUESTS_PAGE_SIZE = 100

/** Nazorat chegarasi — xodimning so'rovlari hech qachon bunchalik ko'p bo'lmaydi. */
const REQUESTS_MAX_PAGES = 20

/**
 * Xodimning BARCHA so'rovlari — sahifalar ketma-ket o'qiladi.
 *
 * ⚠️ Server ro'yxatni sahifalab beradi (sukut 25 ta). Faqat birinchi
 * sahifa olinsa, bir yildan keyin eski so'rovlar ro'yxatdan jimgina
 * yo'qolib qolardi — xodim ularni «o'chib ketdi» deb o'ylardi.
 */
function fetchAllRequests(): Promise<AbsenceRequestDto[]> {
  const collected: AbsenceRequestDto[] = []

  const next = (page: number): Promise<AbsenceRequestDto[]> =>
    api.getRequests(page, REQUESTS_PAGE_SIZE).then((result) => {
      result.items.forEach((item) => collected.push(item))

      return page < result.pageCount && page < REQUESTS_MAX_PAGES ? next(page + 1) : collected
    })

  return next(1)
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
