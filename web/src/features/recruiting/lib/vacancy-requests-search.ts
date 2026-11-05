import { VACANCY_REQUEST_STATUSES, type VacancyRequestStatus } from '../api/vacancy-requests-api'

/**
 * «Vakansiya so'rovlari» ro'yxatining holati — manzil satrida
 * (`employments-search.ts` naqshi, sabab ham bir xil: «Orqaga» o'sha filtrga qaytaradi).
 *
 * Barcha maydonlar IXTIYORIY va sukut qiymati manzilga YOZILMAYDI. Holat tabi sukut
 * bo'yicha «Yangi» (`Pending`) — HR menyuni aynan kutayotgan so'rovlar uchun ochadi;
 * hammasini ko'rish — `status=all`.
 */
export interface VacancyRequestsSearch {
  page?: number
  q?: string
  status?: VacancyRequestStatus | 'all'
  from?: string
  to?: string
  /**
   * Shu so'rovni ochadi — «Mening ishlarim» qadamidan kelgan havola
   * (`docs/SPEC-mening-ishlarim.md` §11.6). Oyna yopilganda manzildan olinadi.
   */
  id?: string
}

export type VacancyRequestTab = VacancyRequestStatus | 'all'

export interface VacancyRequestsFilterState {
  search: string
  from: string
  to: string
}

export const DEFAULT_FILTERS: VacancyRequestsFilterState = { search: '', from: '', to: '' }

export function validateVacancyRequestsSearch(search: Record<string, unknown>): VacancyRequestsSearch {
  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    status: readStatus(search['status']),
    from: readDate(search['from']),
    to: readDate(search['to']),
    id: readText(search['id']),
  }
}

/** Tab: manzilda yo'q — «Yangi». */
export function searchToTab(search: VacancyRequestsSearch): VacancyRequestTab {
  return search.status ?? 'Pending'
}

export function searchToFilters(search: VacancyRequestsSearch): VacancyRequestsFilterState {
  return { search: search.q ?? '', from: search.from ?? '', to: search.to ?? '' }
}

export function filtersToSearch(
  filters: VacancyRequestsFilterState,
): Pick<VacancyRequestsSearch, 'q' | 'from' | 'to'> {
  return {
    q: emptyToUndefined(filters.search),
    from: emptyToUndefined(filters.from),
    to: emptyToUndefined(filters.to),
  }
}

export function isFiltered(search: VacancyRequestsSearch): boolean {
  return search.q !== undefined || search.from !== undefined || search.to !== undefined
}

/* ─────────────────────────────────────────────────────────── o'qish */

function readStatus(value: unknown): VacancyRequestTab | undefined {
  if (value === 'all') return 'all'
  return typeof value === 'string' && (VACANCY_REQUEST_STATUSES as readonly string[]).includes(value)
    ? (value as VacancyRequestStatus)
    : undefined
}

function readPage(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 2) return undefined
  return value
}

function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/u

function readDate(value: unknown): string | undefined {
  return typeof value === 'string' && DATE_ONLY.test(value) ? value : undefined
}

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
