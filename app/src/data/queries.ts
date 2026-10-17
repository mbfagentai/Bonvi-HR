import { api } from '@/api/client'
import type {
  AbsenceRequestDto,
  DayPlanDto,
  MeDto,
  PagedResult,
  ScheduleChangeRequestDto,
} from '@/api/types'
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
  return fetchAllPages((page, pageSize) => api.getRequests(page, pageSize))
}

/** Sahifalarni ketma-ket o'qiydi — yo'qlik va jadval so'rovlari uchun bir xil qoida. */
function fetchAllPages<T>(load: (page: number, pageSize: number) => Promise<PagedResult<T>>): Promise<T[]> {
  const collected: T[] = []

  const next = (page: number): Promise<T[]> =>
    load(page, REQUESTS_PAGE_SIZE).then((result) => {
      result.items.forEach((item) => collected.push(item))

      return page < result.pageCount && page < REQUESTS_MAX_PAGES ? next(page + 1) : collected
    })

  return next(1)
}

/**
 * Jadval so'rovlari (SPEC-jadval-sorovi §C3) — yo'qlik so'rovlaridan
 * ALOHIDA kesh: ular boshqa endpoint'dan keladi va ro'yxatda faqat
 * ekranda birlashtiriladi. Sahifalash qoidasi bir xil.
 */
export function useScheduleRequests(): QueryResult<ScheduleChangeRequestDto[]> {
  return useQuery('jadval-sorovlar', () =>
    fetchAllPages((page, pageSize) => api.getScheduleRequests(page, pageSize)),
  )
}

/**
 * Sananing amaldagi rejasi — jadval so'rovi formasini xodimning hozirgi
 * soatlari bilan to'ldirish uchun. `null` — sana hali tanlanmagan.
 */
export function usePlan(date: string | null): QueryResult<DayPlanDto> {
  return useQuery(date ? `reja:${date}` : null, () => api.getPlan(date ?? ''))
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

/**
 * Jadval so'rovidan keyin: ro'yxat, kun rejalari va oylar. Tasdiqlangan
 * so'rov kunning REJASINI almashtiradi — taqvim ham, forma ham eski
 * soatlarni ko'rsatib qolmasin.
 */
export function invalidateScheduleRequests(): void {
  invalidateQuery('jadval-sorovlar')
  invalidateQuery('reja')
  invalidateQuery('oy')
}

export function invalidateMe(): void {
  invalidateQuery('me')
}
