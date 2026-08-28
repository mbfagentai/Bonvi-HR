import {
  INCENTIVE_KINDS,
  type IncentiveKindValue,
} from '@/features/employees/api/incentives-api'

/**
 * «Rag'bat va jarima» ro'yxatining holati — manzil satrida
 * (`employments-search.ts` naqshi). NEGA shu yerda ham kerak: filtr bilan
 * sahifani ochib, xodim kartasiga o'tib, «orqaga» bosgan HR bir xil
 * filtrga qaytishi kerak — aks holda har safar qidiruv va sana oralig'ini
 * qayta terardi. Bu ekranda qator BOSILADIGAN bo'lgani uchun («xodim
 * kartasi ochiladi») bu talab yanada kuchliroq: kartaga o'tish va
 * qaytish — eng ko'p takrorlanadigan harakat.
 *
 * Barcha maydonlar IXTIYORIY va standart qiymat manzilga yozilmaydi.
 */
export interface IncentivesSearch {
  /** 1 dan boshlanadi. 1 — standart, manzilda ko'rinmaydi. */
  page?: number
  /**
   * Qidiruv matni: XODIM bo'yicha — F.I.Sh., JSHSHIR, pasport yoki
   * telefon (xodimlar ro'yxatidagi bilan aynan bir xil qoida). Buyruq
   * raqami bu ekranda qidirilmaydi: savol «kimga nima berilgan».
   */
  q?: string
  kind?: IncentiveKindValue
  /** `DateOnly` → «2026-08-26». */
  from?: string
  to?: string
  department?: string
  /** Yuridik shaxs. Manzilda qisqa nom — `legalEntity` juda uzun. */
  entity?: string
  /** Standart (`-date`) manzilga YOZILMAYDI — `normalizeSort` ga qara. */
  sort?: IncentivesSortParam
}

/**
 * Saralanadigan ustunlar.
 *
 * Qiymatlar jadval ustunlarining `id` si bilan AYNAN bir xil: `DataTable`
 * serverga o'sha `id` ni beradi va oraliq xarita bo'lmasa, ustun nomini
 * o'zgartirgan odam saralashni ham birga o'zgartiradi. Backend
 * «employeeName» ni ham, qisqa «employee» ni ham tushunadi
 * (`ListIncentivesQueryHandler.ParseSort`) — bu yerda uzun shakli
 * ishlatiladi.
 */
export type IncentivesSortKey = 'date' | 'employeeName'

/** «-» — kamayish tartibi (backend shu shaklni kutadi). */
export type IncentivesSortParam = IncentivesSortKey | `-${IncentivesSortKey}`

/**
 * Standart saralash: sana bo'yicha YANGIDAN ESKIGA.
 *
 * NEGA xodimlar ro'yxatidagidan (alifbo) farq qiladi: bu ekran «kimni
 * topay» degan savolga emas, «oxirgi paytda nima bo'ldi» degan savolga
 * javob beradi. Serverning sukuti ham aynan shu (`-date`), ya'ni ikkala
 * tomon bir xil narsani standart deb biladi.
 */
export const DEFAULT_SORT: IncentivesSortParam = '-date'

/** Toolbar ishlaydigan ko'rinish: bo'sh satr — «filtr qo'yilmagan». */
export interface IncentivesFilterState {
  search: string
  kind: IncentiveKindValue | ''
  from: string
  to: string
  departmentId: string
  legalEntityId: string
}

export const DEFAULT_FILTERS: IncentivesFilterState = {
  search: '',
  kind: '',
  from: '',
  to: '',
  departmentId: '',
  legalEntityId: '',
}

/**
 * Manzil satridan kelgan qiymatlarni tekshiradi.
 *
 * Hech qachon xato tashlamaydi: noto'g'ri qiymat («?from=abc»,
 * «?kind=mukofot») shunchaki tashlab yuboriladi, ekran «oq sahifa» bo'lib
 * qolmaydi. Bu qat'iy konvensiya — havolani qo'lda tahrirlagan yoki eski
 * xatcho'pdan kelgan odam ishlaydigan sahifani ko'rishi kerak.
 */
export function validateIncentivesSearch(search: Record<string, unknown>): IncentivesSearch {
  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    kind: readKind(search['kind']),
    from: readDate(search['from']),
    to: readDate(search['to']),
    department: readId(search['department']),
    entity: readId(search['entity']),
    sort: normalizeSort(search['sort']),
  }
}

export function searchToFilters(search: IncentivesSearch): IncentivesFilterState {
  return {
    search: search.q ?? '',
    kind: search.kind ?? '',
    from: search.from ?? '',
    to: search.to ?? '',
    departmentId: search.department ?? '',
    legalEntityId: search.entity ?? '',
  }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi. Sahifa va saralash hisobga
 * OLINMAYDI: ular ro'yxatni qisqartirmaydi, ya'ni bo'sh natijaning sababi
 * bo'lolmaydi — «hech narsa topilmadi» xabari faqat filtr ayblanadigan
 * holatda «filtrlarni o'zgartirib ko'ring» deyishi kerak.
 */
export function isFiltered(search: IncentivesSearch): boolean {
  return (
    search.q !== undefined ||
    search.kind !== undefined ||
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
  filters: IncentivesFilterState,
): Omit<IncentivesSearch, 'page' | 'sort'> {
  return {
    q: emptyToUndefined(filters.search),
    kind: filters.kind === '' ? undefined : filters.kind,
    from: emptyToUndefined(filters.from),
    to: emptyToUndefined(filters.to),
    department: emptyToUndefined(filters.departmentId),
    entity: emptyToUndefined(filters.legalEntityId),
  }
}

/* ─────────────────────────────────────────────────────────── o'qish */

const KINDS: ReadonlySet<string> = new Set(INCENTIVE_KINDS)

function readKind(value: unknown): IncentiveKindValue | undefined {
  return typeof value === 'string' && KINDS.has(value)
    ? (value as IncentiveKindValue)
    : undefined
}

/**
 * Saralash qiymatini tekshiradi va ikki holatda `undefined` qaytaradi:
 * (1) qiymat noto'g'ri — ro'yxat baribir ochiladi, xato qaytmaydi;
 * (2) qiymat aynan standart — u holda manzilda `?sort=-date` bo'lib
 * osilib turishi shovqindan boshqa narsa emas (`employees-search.ts`
 * dagi bilan bir xil qoida).
 */
export function normalizeSort(value: unknown): IncentivesSortParam | undefined {
  if (typeof value !== 'string') return undefined

  const desc = value.startsWith('-')
  const key = desc ? value.slice(1) : value

  if (!isSortKey(key)) return undefined

  const param: IncentivesSortParam = desc ? `-${key}` : key
  return param === DEFAULT_SORT ? undefined : param
}

/*
 * Ruxsat etilgan qiymatlar massiv emas, KALITLI obyekt.
 *
 * NEGA: `Record<Union, true>` yangi qiymat qo'shilganda TypeScript'ni shu
 * yerni ham to'ldirishga majbur qiladi. `['a','b'].includes(x)` esa
 * jimgina eskirib qolardi — yangi ustun qo'shilsa filtr uni tashlab
 * yuborar, lekin hech qanday xato chiqmasdi.
 */
const SORT_KEYS: Record<IncentivesSortKey, true> = {
  date: true,
  employeeName: true,
}

function isSortKey(value: string): value is IncentivesSortKey {
  return Object.hasOwn(SORT_KEYS, value)
}

/**
 * Sahifa raqami. 1 dan kichigi, kasr va `NaN` — hammasi birinchi
 * sahifaga tushadi (`employments-search.ts` dagi bilan bir xil qoida).
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
