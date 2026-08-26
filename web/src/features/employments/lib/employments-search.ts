import {
  EMPLOYMENT_EVENT_TYPES,
  type EmploymentEventType,
} from '@/features/employees/api/employees-api'

/**
 * Buyruqlar ro'yxatining holati — manzil satrida (`employees-search.ts`
 * naqshi). NEGA shu yerda ham kerak: filtr bilan sahifani ochib, boshqa
 * xodim kartasiga o'tib, «orqaga» bosgan HR bir xil filtrga qaytishi
 * kerak — aks holda har safar qidiruv va sana oralig'ini qayta terardi.
 *
 * Barcha maydonlar IXTIYORIY va standart qiymat manzilga yozilmaydi.
 */
export interface EmploymentEventsSearch {
  /** 1 dan boshlanadi. 1 — standart, manzilda ko'rinmaydi. */
  page?: number
  /** Qidiruv matni: BUYRUQ RAQAMI (F.I.Sh. emas — backend shunday qidiradi). */
  q?: string
  type?: EmploymentEventType
  /** `DateOnly` → «2026-08-25». */
  from?: string
  to?: string
  department?: string
  /** Yuridik shaxs. Manzilda qisqa nom — `legalEntity` juda uzun. */
  entity?: string
}

/** Toolbar ishlaydigan ko'rinish: bo'sh satr — «filtr qo'yilmagan». */
export interface EmploymentEventsFilterState {
  search: string
  type: EmploymentEventType | ''
  from: string
  to: string
  departmentId: string
  legalEntityId: string
}

export const DEFAULT_FILTERS: EmploymentEventsFilterState = {
  search: '',
  type: '',
  from: '',
  to: '',
  departmentId: '',
  legalEntityId: '',
}

/**
 * Manzil satridan kelgan qiymatlarni tekshiradi.
 *
 * Hech qachon xato tashlamaydi: noto'g'ri qiymat («?from=abc») shunchaki
 * tashlab yuboriladi, ekran «oq sahifa» bo'lib qolmaydi.
 */
export function validateEmploymentEventsSearch(
  search: Record<string, unknown>,
): EmploymentEventsSearch {
  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    type: readType(search['type']),
    from: readDate(search['from']),
    to: readDate(search['to']),
    department: readId(search['department']),
    entity: readId(search['entity']),
  }
}

export function searchToFilters(search: EmploymentEventsSearch): EmploymentEventsFilterState {
  return {
    search: search.q ?? '',
    type: search.type ?? '',
    from: search.from ?? '',
    to: search.to ?? '',
    departmentId: search.department ?? '',
    legalEntityId: search.entity ?? '',
  }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi. Sahifa hisobga OLINMAYDI: u
 * ro'yxatni qisqartirmaydi, ya'ni bo'sh natijaning sababi bo'lolmaydi.
 */
export function isFiltered(search: EmploymentEventsSearch): boolean {
  return (
    search.q !== undefined ||
    search.type !== undefined ||
    search.from !== undefined ||
    search.to !== undefined ||
    search.department !== undefined ||
    search.entity !== undefined
  )
}

/**
 * Toolbar → manzil. Bo'sh maydon `undefined` bo'lib qaytadi va manzildan
 * BUTUNLAY yo'qoladi — aks holda tozalangan filtr `?department=` bo'lib
 * osilib qolardi.
 */
export function filtersToSearch(
  filters: EmploymentEventsFilterState,
): Omit<EmploymentEventsSearch, 'page'> {
  return {
    q: emptyToUndefined(filters.search),
    type: filters.type === '' ? undefined : filters.type,
    from: emptyToUndefined(filters.from),
    to: emptyToUndefined(filters.to),
    department: emptyToUndefined(filters.departmentId),
    entity: emptyToUndefined(filters.legalEntityId),
  }
}

/* ─────────────────────────────────────────────────────────── o'qish */

const TYPES: ReadonlySet<string> = new Set(EMPLOYMENT_EVENT_TYPES)

function isType(value: string): value is EmploymentEventType {
  return TYPES.has(value)
}

function readType(value: unknown): EmploymentEventType | undefined {
  return typeof value === 'string' && isType(value) ? value : undefined
}

/**
 * Sahifa raqami. 1 dan kichigi, kasr va `NaN` — hammasi birinchi
 * sahifaga tushadi (`employees-search.ts` dagi bilan bir xil qoida).
 */
function readPage(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 2) return undefined
  return value
}

function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}

/** `DateOnly` shakli — boshqasi backend'ga 400 bilan qaytardi. */
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/u

function readDate(value: unknown): string | undefined {
  return typeof value === 'string' && DATE_ONLY.test(value) ? value : undefined
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function readId(value: unknown): string | undefined {
  return typeof value === 'string' && UUID.test(value) ? value : undefined
}

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
