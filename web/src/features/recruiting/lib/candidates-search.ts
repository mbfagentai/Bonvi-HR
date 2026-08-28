import {
  CANDIDATE_SOURCES,
  CANDIDATE_STATUSES,
  type CandidateSource,
  type CandidateStatus,
} from '../api/recruiting-api'

/**
 * Nomzodlar ro'yxatining holati — manzil satrida
 * (`employees-search.ts` naqshi).
 *
 * NEGA komponent ichidagi `useState` emas: bu ro'yxat marshrut
 * komponenti va nomzod kartasiga o'tilganda BUTUNLAY unmount bo'ladi.
 * Holat ichkarida bo'lsa, «orqaga» bosgan HR uchinchi sahifadan
 * birinchisiga, filtrsiz holatga tushib qolardi.
 *
 * ⭐ VORONKA KO'RINISHI ham shu yerda (`view`). Voronka alohida
 * endpoint EMAS — u shu ro'yxatning boshqa CHIZILISHI, ya'ni holat ham
 * bitta bo'lishi kerak. Ikkita ekran qilinsa, filtr, qidiruv va
 * sahifalash ikki marta yozilardi va vaqt o'tib bir-biridan ajralib
 * ketardi (`nav-items.ts` dagi «ishdan ketganlar» bandi bilan bir xil
 * mulohaza).
 */
export interface CandidatesSearch {
  /** 1 dan boshlanadi. 1 — standart, manzilda ko'rinmaydi. */
  page?: number
  /** Qidiruv matni: F.I.Sh. yoki TELEFON (nomzodda JSHSHIR va pasport yo'q). */
  q?: string
  vacancy?: string
  /**
   * Bosqich bo'yicha filtr.
   *
   * ⭐ Bu «Suhbatlar» bo'limining O'RNIDA turadi. Rejada suhbatlar
   * alohida bo'lim edi, lekin backendda alohida `Interview` agregati
   * YO'Q: suhbat ham shunchaki bosqich (`CandidateStageEntry` domen
   * izohi). Bosqichlar esa SOZLANADIGAN ma'lumotnoma — qaysi bosqich
   * «suhbat» ekanini kod bila olmaydi va hech qachon bilmaydi.
   * Shu sababli menyudagi band o'chirildi, o'rniga shu filtr keldi:
   * HR «Суҳбат 1» ni tanlaydi va aynan o'sha ro'yxatni oladi.
   */
  stage?: string
  status?: CandidateStatus
  source?: CandidateSource
  sort?: CandidatesSortParam
  view?: CandidatesView
}

/** `table` — jadval (standart), `funnel` — bosqichlar bo'yicha ustunlar. */
export type CandidatesView = 'table' | 'funnel'

/**
 * Server tanidigan saralash kalitlari — AYNAN shu uchtasi
 * (`RecruitingQueryParsing.ParseCandidateSort`).
 */
export type CandidatesSortKey = 'createdAt' | 'name' | 'stage'

export type CandidatesSortParam = CandidatesSortKey | `-${CandidatesSortKey}`

/**
 * Standart saralash: kiritilgan vaqt bo'yicha YANGIDAN ESKIGA.
 *
 * NEGA alifbo emas (xodimlar ro'yxatidan farqli): nomzodlar ro'yxati —
 * oqim, arxiv emas. HR «bugun kim tushdi?» deb qaraydi; ismni esa u
 * qidiruv maydoniga yozadi.
 */
export const DEFAULT_SORT: CandidatesSortParam = '-createdAt'

/** Toolbar ishlaydigan ko'rinish: bo'sh satr — «filtr qo'yilmagan». */
export interface CandidatesFilterState {
  search: string
  vacancyId: string
  stageId: string
  status: CandidateStatus | ''
  source: CandidateSource | ''
}

export const DEFAULT_FILTERS: CandidatesFilterState = {
  search: '',
  vacancyId: '',
  stageId: '',
  status: '',
  source: '',
}

/**
 * Manzil satridan kelgan qiymatlarni tekshiradi.
 *
 * Hech qachon xato TASHLAMAYDI — noto'g'ri qiymat tashlab yuboriladi.
 */
export function validateCandidatesSearch(search: Record<string, unknown>): CandidatesSearch {
  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    vacancy: readId(search['vacancy']),
    stage: readId(search['stage']),
    status: readOneOf(search['status'], CANDIDATE_STATUSES),
    source: readOneOf(search['source'], CANDIDATE_SOURCES),
    sort: normalizeSort(search['sort']),
    view: readView(search['view']),
  }
}

export function searchToFilters(search: CandidatesSearch): CandidatesFilterState {
  return {
    search: search.q ?? '',
    vacancyId: search.vacancy ?? '',
    stageId: search.stage ?? '',
    status: search.status ?? '',
    source: search.source ?? '',
  }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi.
 *
 * Sahifa, saralash va KO'RINISH hisobga olinmaydi: ular ro'yxatni
 * qisqartirmaydi, ya'ni bo'sh natijaning sababi bo'lolmaydi.
 */
export function isFiltered(search: CandidatesSearch): boolean {
  return (
    search.q !== undefined ||
    search.vacancy !== undefined ||
    search.stage !== undefined ||
    search.status !== undefined ||
    search.source !== undefined
  )
}

export function filtersToSearch(
  filters: CandidatesFilterState,
): Omit<CandidatesSearch, 'page' | 'sort' | 'view'> {
  return {
    q: readText(filters.search),
    vacancy: emptyToUndefined(filters.vacancyId),
    stage: emptyToUndefined(filters.stageId),
    status: filters.status === '' ? undefined : filters.status,
    source: filters.source === '' ? undefined : filters.source,
  }
}

/** `employees-search.ts` dagi `normalizeSort` bilan bir xil qoida. */
export function normalizeSort(value: unknown): CandidatesSortParam | undefined {
  if (typeof value !== 'string') return undefined

  const desc = value.startsWith('-')
  const key = desc ? value.slice(1) : value

  if (!isSortKey(key)) return undefined

  const param: CandidatesSortParam = desc ? `-${key}` : key
  return param === DEFAULT_SORT ? undefined : param
}

/* ─────────────────────────────────────────────────────────────── o'qish */

const SORT_KEYS: Record<CandidatesSortKey, true> = {
  createdAt: true,
  name: true,
  stage: true,
}

function isSortKey(value: string): value is CandidatesSortKey {
  return Object.hasOwn(SORT_KEYS, value)
}

const VIEWS: Record<CandidatesView, true> = {
  table: true,
  funnel: true,
}

/** `table` — standart ko'rinish, u manzilda saqlanmaydi. */
function readView(value: unknown): CandidatesView | undefined {
  if (typeof value !== 'string' || !Object.hasOwn(VIEWS, value)) return undefined
  return value === 'table' ? undefined : (value as CandidatesView)
}

function readOneOf<T extends string>(value: unknown, allowed: readonly T[]): T | undefined {
  return typeof value === 'string' && (allowed as readonly string[]).includes(value)
    ? (value as T)
    : undefined
}

function readPage(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 2) return undefined
  return value
}

/**
 * Qidiruv matni QIRQILMAYDI — faqat butunlay bo'sh qiymat tashlanadi.
 *
 * NEGA: qirqilsa «Karimov » deb yozilgan probel 350 ms dan keyin
 * manzildan qaytib kelib maydondan yo'qolardi — foydalanuvchi hali
 * ismini yozib ulgurmasidan (`employees-search.ts` dagi bilan bir xil).
 */
function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function readId(value: unknown): string | undefined {
  return typeof value === 'string' && UUID.test(value) ? value : undefined
}

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
