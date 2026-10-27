/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Farqi: bu yerda endpoint ALLAQACHON TAYYOR va testlangan (boshqa agent
 * backend'ni yozgan), shu sababli maydon nomlari «kutilayotgan shartnoma»
 * emas — ular haqiqiy manbadan olingan:
 *   api/src/Bonvi.Api/Endpoints/EmploymentEndpoints.cs:185-231 (GET /events)
 *   api/src/Bonvi.Application/Employees/Employments/ListEmploymentEventsQuery.cs
 *   api/src/Bonvi.Application/Employees/Dtos/EmploymentDtos.cs (EmploymentEventListItemDto)
 *
 * Sanoq turi (`EmploymentEventType`) va pul tipi (`MoneyValue`) qayta
 * yozilmaydi — `employees-api.ts` da ALLAQACHON bor (mehnat tarixi
 * lentasi ham xuddi shu hodisalarni ishlatadi), shu sababli shu yerdan
 * import qilinadi.
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `employments-queries.ts` generatsiya qilingan hook'ga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'
import {
  EMPLOYMENT_EVENT_TYPES,
  type EmploymentEventType,
  type MoneyValue,
  type PagedResult,
} from '@/features/employees/api/employees-api'

/** Buyruqlar oqimidagi bitta qator — xodim nomi bilan birga. */
export interface EmploymentEventItem {
  id: string
  employmentId: string
  employeeId: string
  employeeName: string
  photoKey: string | null
  type: EmploymentEventType
  /** Kuchga kirgan sana — buyruq sanasi bilan bir xil bo'lmasligi mumkin. */
  effectiveDate: string
  orderNo: string | null
  orderDate: string | null
  fromDepartment: string | null
  toDepartment: string | null
  fromPosition: string | null
  toPosition: string | null
  /** `null` — `salary.view` ruxsati yo'q YOKI bu hodisada oklad umuman o'zgarmagan. */
  fromSalary: MoneyValue | null
  toSalary: MoneyValue | null
  note: string | null
  isImported: boolean
}

export interface EmploymentEventFilters {
  /** Buyruq raqami bo'yicha (F.I.Sh. emas — backend'ning o'zi shunday qidiradi). */
  search?: string
  type?: EmploymentEventType
  /** `DateOnly` → «2026-08-25». Ikki tomondan yopiq oraliq. */
  from?: string
  to?: string
  departmentId?: string
  legalEntityId?: string
  page: number
  pageSize: number
}

export async function fetchEmploymentEvents(
  filters: EmploymentEventFilters,
  signal?: AbortSignal,
): Promise<PagedResult<EmploymentEventItem>> {
  const { data } = await api.get<unknown>('/api/employments/events', {
    params: listParams(filters),
    signal,
  })

  return readPage(data, toEvent, filters.page, filters.pageSize)
}

function listParams(filters: EmploymentEventFilters): Record<string, unknown> {
  return { ...employmentEventListParams(filters), page: filters.page, pageSize: filters.pageSize }
}

/** Ro'yxat filtri sahifasiz — Excel eksporti shu bilan so'raladi. */
export type EmploymentEventListFilters = Omit<EmploymentEventFilters, 'page' | 'pageSize'>

/**
 * Ro'yxat so'rovining filtr parametrlari — ro'yxat ham, Excel eksporti ham
 * AYNAN shu funksiyadan (`docs/SPEC-excel-eksport.md` §3).
 */
export function employmentEventListParams(
  filters: EmploymentEventListFilters,
): Record<string, unknown> {
  const params: Record<string, unknown> = {}

  if (filters.search) params['search'] = filters.search
  if (filters.from) params['from'] = filters.from
  if (filters.to) params['to'] = filters.to
  if (filters.departmentId) params['departmentId'] = filters.departmentId
  if (filters.legalEntityId) params['legalEntityId'] = filters.legalEntityId

  // ASP.NET Core enum'ni NOM bo'yicha bog'laydi (registrga sezgir emas):
  // «additionalDuty» → «AdditionalDuty». Faqat birinchi harf o'zgaradi,
  // ichkarisi allaqachon PascalCase bilan mos (camelCase manba shunday
  // yozilgan — `EMPLOYMENT_EVENT_TYPES`).
  if (filters.type !== undefined) {
    params['type'] = filters.type.charAt(0).toUpperCase() + filters.type.slice(1)
  }

  return params
}

/* ─────────────────────────────────────────────────── xaritalash (map) */

function toEvent(raw: unknown): EmploymentEventItem {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employmentId: text(box['employmentId']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    employeeName: text(box['employeeName']) ?? '—',
    photoKey: text(box['photoKey']),
    type: toType(box['type']),
    effectiveDate: text(box['effectiveDate']) ?? '',
    orderNo: text(box['orderNo']),
    orderDate: text(box['orderDate']),
    fromDepartment: text(box['fromDepartment']),
    toDepartment: text(box['toDepartment']),
    fromPosition: text(box['fromPosition']),
    toPosition: text(box['toPosition']),
    fromSalary: money(box['fromSalary']),
    toSalary: money(box['toSalary']),
    note: text(box['note']),
    isImported: box['isImported'] === true,
  }
}

/**
 * Enum ikki xil ko'rinishda yuradi: javobda SON (`"type": 4`), so'rovda
 * NOM. Server tartibi C# qiymatlariga mos (1 dan boshlanadi):
 * `EmploymentEventType.Hire = 1` → `EMPLOYMENT_EVENT_TYPES[0]`.
 */
function toType(value: unknown): EmploymentEventType {
  if (typeof value === 'number') {
    return EMPLOYMENT_EVENT_TYPES[value - 1] ?? 'hire'
  }

  if (typeof value === 'string') {
    const lowered = value.toLowerCase()
    return EMPLOYMENT_EVENT_TYPES.find((name) => name.toLowerCase() === lowered) ?? 'hire'
  }

  return 'hire'
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/** Oklad — `MoneyDto` obyekti (`{amount, currency, formatted}`) yoki `null`. */
function money(value: unknown): MoneyValue | null {
  if (value === null || typeof value !== 'object') return null

  const box = value as Record<string, unknown>
  const amount = box['amount']
  if (typeof amount !== 'number' || !Number.isFinite(amount)) return null

  return { amount, currency: text(box['currency']) ?? 'UZS' }
}

/**
 * Sahifalangan javobni bir ko'rinishga keltiradi — `employees-api.ts`
 * dagi `readPage` bilan bir xil sabab: `PagedResult` uchun .NET tomonida
 * yagona kelishuv yo'q edi, endpoint yozilgan paytda tekshirilmagan.
 */
function readPage<TRaw, TItem>(
  data: unknown,
  map: (raw: TRaw) => TItem,
  fallbackPage: number,
  fallbackPageSize: number,
): PagedResult<TItem> {
  if (Array.isArray(data)) {
    const items = (data as TRaw[]).map(map)
    return { items, total: items.length, page: 1, pageSize: items.length }
  }

  const box = (data ?? {}) as Record<string, unknown>
  const rawItems = (Array.isArray(box['items']) ? box['items'] : box['data']) as
    | TRaw[]
    | undefined

  const items = (rawItems ?? []).map(map)

  const total = firstNumber(box['total'], box['totalCount'], box['totalItems'])
  const page = firstNumber(box['page'], box['pageNumber'], box['currentPage'])
  const pageSize = firstNumber(box['pageSize'], box['perPage'], box['size'])

  return {
    items,
    total: total ?? items.length,
    page: page ?? fallbackPage,
    pageSize: pageSize ?? fallbackPageSize,
  }
}

function firstNumber(...candidates: unknown[]): number | null {
  for (const candidate of candidates) {
    if (typeof candidate === 'number' && Number.isFinite(candidate)) return candidate
  }
  return null
}
