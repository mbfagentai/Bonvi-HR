import type { PagedResult } from '@/features/employees/api/employees-api'
import { api } from '@/shared/api/http-client'

/**
 * «Vakansiya so'rovlari» — bo'lim boshliqlarining yangi vakansiya so'rovlari
 * (`docs/SPEC-vakansiya-sorovi.md`).
 *
 * Holat nomlari serverdagi sanoqning o'zi (`JsonStringEnumConverter`) —
 * `recruiting-api.ts` boshidagi izohdagi kelishuv.
 */
export const VACANCY_REQUEST_STATUSES = ['Pending', 'Approved', 'Rejected', 'Cancelled'] as const
export type VacancyRequestStatus = (typeof VACANCY_REQUEST_STATUSES)[number]

const BASE = '/api/recruiting/vacancy-requests'

export interface VacancyRequest {
  id: string
  departmentId: string
  departmentName: string
  /** To'liq yo'l: «Zavod / Sex / Bo'lim». */
  departmentPath: string | null
  requestedByEmployeeId: string
  requestedByName: string
  title: string
  openings: number
  /** `YYYY-MM-DD` yoki `null`. */
  neededBy: string | null
  note: string | null
  status: VacancyRequestStatus
  /** ISO, UTC. */
  createdAt: string
  decidedAt: string | null
  decidedByName: string | null
  decisionNote: string | null
  vacancyId: string | null
  vacancyTitle: string | null
  candidateCount: number
  hiredCount: number
}

/** Ro'yxat filtri sahifasiz — Excel eksporti ham aynan shu bilan so'raladi. */
export interface VacancyRequestListFilters {
  status?: VacancyRequestStatus
  search?: string
  from?: string
  to?: string
}

export interface VacancyRequestFilters extends VacancyRequestListFilters {
  page: number
  pageSize: number
}

export interface VacancyRequestInput {
  departmentId: string
  /** `null` — bo'lim boshlig'i (server o'zi qo'yadi). */
  requestedByEmployeeId: string | null
  title: string
  openings: number
  neededBy: string | null
  note: string | null
}

/** Tasdiqlash tanasi — `POST /api/recruiting/vacancies` bilan bir xil. */
export interface VacancyRequestApproveInput {
  title: string
  openings: number
  openedAt: string | null
  note: string | null
}

/** Ro'yxat ham, eksport ham AYNAN shu parametrlar bilan (`docs/SPEC-excel-eksport.md` §3). */
export function vacancyRequestListParams(filters: VacancyRequestListFilters): Record<string, unknown> {
  const result: Record<string, unknown> = {}
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && value !== '') result[key] = value
  }
  return result
}

export async function fetchVacancyRequests(
  filters: VacancyRequestFilters,
  signal?: AbortSignal,
): Promise<PagedResult<VacancyRequest>> {
  const { data } = await api.get<unknown>(BASE, {
    params: { ...vacancyRequestListParams(filters), page: filters.page, pageSize: filters.pageSize },
    signal,
  })

  const box = (data ?? {}) as Record<string, unknown>

  return {
    items: (Array.isArray(box['items']) ? box['items'] : []).map(toVacancyRequest),
    total: count(box['total']),
    page: count(box['page']) || filters.page,
    pageSize: count(box['pageSize']) || filters.pageSize,
  }
}

export async function createVacancyRequest(body: VacancyRequestInput): Promise<VacancyRequest> {
  const { data } = await api.post<unknown>(BASE, body)
  return toVacancyRequest(data)
}

export async function approveVacancyRequest(
  id: string,
  body: VacancyRequestApproveInput,
): Promise<VacancyRequest> {
  const { data } = await api.post<unknown>(`${BASE}/${id}/approve`, body)
  return toVacancyRequest(data)
}

export async function rejectVacancyRequest(id: string, reason: string): Promise<VacancyRequest> {
  const { data } = await api.post<unknown>(`${BASE}/${id}/reject`, { reason })
  return toVacancyRequest(data)
}

export async function cancelVacancyRequest(id: string): Promise<VacancyRequest> {
  const { data } = await api.post<unknown>(`${BASE}/${id}/cancel`)
  return toVacancyRequest(data)
}

/* ═════════════════════════════════════════════════ `unknown` dan o'qish */

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function count(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}

function toVacancyRequest(raw: unknown): VacancyRequest {
  const box = (raw ?? {}) as Record<string, unknown>
  const status = box['status']

  return {
    id: text(box['id']) ?? '',
    departmentId: text(box['departmentId']) ?? '',
    departmentName: text(box['departmentName']) ?? '—',
    departmentPath: text(box['departmentPath']),
    requestedByEmployeeId: text(box['requestedByEmployeeId']) ?? '',
    requestedByName: text(box['requestedByName']) ?? '—',
    title: text(box['title']) ?? '',
    openings: count(box['openings']),
    neededBy: text(box['neededBy']),
    note: text(box['note']),
    // Noma'lum holat eng zararsiz ko'rinishga — «Yangi» (`oneOf` dagi kelishuv)
    status: (VACANCY_REQUEST_STATUSES as readonly unknown[]).includes(status)
      ? (status as VacancyRequestStatus)
      : 'Pending',
    createdAt: text(box['createdAt']) ?? '',
    decidedAt: text(box['decidedAt']),
    decidedByName: text(box['decidedByName']),
    decisionNote: text(box['decisionNote']),
    vacancyId: text(box['vacancyId']),
    vacancyTitle: text(box['vacancyTitle']),
    candidateCount: count(box['candidateCount']),
    hiredCount: count(box['hiredCount']),
  }
}
