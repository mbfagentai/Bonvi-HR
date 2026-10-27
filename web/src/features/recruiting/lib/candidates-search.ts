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
  /**
   * ⭐ «Bugun kim bilan gaplashish kerak» — keyingi aloqa sanasi bugun
   * yoki undan oldin bo'lganlar (`contactDue` so'rov parametri).
   *
   * FILTR: bo'sh natija foydalanuvchining o'z tanlovi oqibati, shu
   * sababli `isFiltered` uni SANAYDI — ekranda «topilmadi» matni
   * chiqishi kerak.
   *
   * «Bugun»ni SERVER hisoblaydi (Toshkent kalendari): brauzer boshqa
   * mintaqada bo'lsa ro'yxat bir kunga surilib ketardi
   * (`CandidateFilters.contactDue` izohi).
   *
   * Tipi `true` — `false` manzilda ATAYLAB saqlanmaydi: standart holat
   * manzilda ko'rinmaydi (`readView` bilan bir xil qoida).
   */
  due?: true
  /**
   * ⭐ «Rad etilganlar» BO'LIMI: `Rejected` VA `Withdrawn`
   * (`rejected=true` so'rov parametri, SPEC §4.7).
   *
   * ⚠️ Bu FILTR EMAS, BO'LIM — shu sababli `isFiltered` uni ATAYLAB
   * sanamaydi. Bo'sh natija bu yerda «hech kim rad etilmagan» degani,
   * «qidiruvingiz hech narsa topmadi» degani emas, va ekrandagi bo'sh
   * holat matni aynan shu farqqa tayanadi.
   *
   * NEGA `status=Rejected,Withdrawn` emas: bitta `<select>` ikkita
   * qiymatni ifodalay olmaydi, ya'ni butun manzil sxemasi UI hech qachon
   * hosil qila olmaydigan ko'p qiymatli holatni tashib yurardi. Bayroq
   * esa holat filtri bilan BIRGA ishlaydi: bo'lim ichida HR «O'zi voz
   * kechgan»ni tanlab, faqat ularni ko'ra oladi.
   */
  rejected?: true
  /**
   * Sana oralig'i — `DateOnly` («2026-10-01»), ikkala chegara ham kiradi.
   *
   * ⚠️ MA'NOSI bo'limga qarab o'zgaradi (server qoidasi): «Rad etilganlar»
   * bo'limida (`rejected`) — rad etilgan / voz kechgan kun, qolgan hamma
   * joyda — ariza kelgan kun. Holat tanlovi ma'noni o'zgartirMAYDI.
   * Shu sababli toolbar yorlig'i ham bo'limga qarab tanlanadi.
   */
  from?: string
  to?: string
  /**
   * «Qayerdan bilgan» filter (SPEC-qayerdan-bilgan §6): a catalog channel id,
   * or `none` — «Ko'rsatilmagan» (legacy and Excel candidates without one).
   */
  referral?: string
  sort?: CandidatesSortParam
  view?: CandidatesView
}

/** `referral=none` in the address — candidates without a channel (`referralMissing=true`). */
export const REFERRAL_MISSING = 'none'

/** `table` — jadval (standart), `funnel` — bosqichlar bo'yicha ustunlar. */
export type CandidatesView = 'table' | 'funnel'

/**
 * Server tanidigan saralash kalitlari — AYNAN shu to'rttasi
 * (`RecruitingQueryParsing.ParseCandidateSort`).
 *
 * `nextContact` — keyingi aloqa sanasi. Serverda sanasi YO'Q qatorlar
 * ikkala yo'nalishda ham OXIRIDA turadi (SPEC §4.6): usiz «keyingi aloqa,
 * kamayish bo'yicha» butun bir ekran chiziqcha bilan ochilardi.
 */
export type CandidatesSortKey = 'createdAt' | 'name' | 'stage' | 'nextContact'

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
  /**
   * «Bugun aloqa kerak» katakchasi (`EmployeesFilterState.needsReview`
   * bilan bir xil naqsh: mantiqiy filtr shu yerda `boolean`, manzilda esa
   * faqat yoqilgan holatda ko'rinadi).
   *
   * ⚠️ «Rad etilganlar» bo'limi bu tarkibda ATAYLAB YO'Q: u toolbar
   * boshqaradigan filtr emas, ekranning o'zi. Bu yerga qo'shilsa,
   * «filtrlarni tozalash» HR ni bo'limdan chiqarib yuborardi.
   */
  contactDue: boolean
  /** Sana oralig'i — bo'sh satr «chegara yo'q» (`CandidatesSearch.from` izohi). */
  from: string
  to: string
  /** `''` — any, `REFERRAL_MISSING` — none recorded, otherwise a channel id. */
  referral: string
}

export const DEFAULT_FILTERS: CandidatesFilterState = {
  search: '',
  vacancyId: '',
  stageId: '',
  status: '',
  source: '',
  contactDue: false,
  from: '',
  to: '',
  referral: '',
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
    due: readFlag(search['due']),
    rejected: readFlag(search['rejected']),
    from: readDate(search['from']),
    to: readDate(search['to']),
    referral: readReferral(search['referral']),
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
    contactDue: search.due ?? false,
    from: search.from ?? '',
    to: search.to ?? '',
    referral: search.referral ?? '',
  }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi.
 *
 * Sahifa, saralash va KO'RINISH hisobga olinmaydi: ular ro'yxatni
 * qisqartirmaydi, ya'ni bo'sh natijaning sababi bo'lolmaydi.
 *
 * ⚠️ `rejected` ham ATAYLAB sanalmaydi — u filtr emas, BO'LIM
 * (`CandidatesSearch.rejected` izohi). Ilgari bu farq chaqiruv joyida,
 * `isFiltered({ ...search, status: undefined })` ko'rinishida qo'lda
 * yasalardi; endi qoida shu funksiyaning O'ZIDA turadi va sahifa
 * shunchaki `isFiltered(search)` deb so'raydi.
 */
export function isFiltered(search: CandidatesSearch): boolean {
  return (
    search.q !== undefined ||
    search.vacancy !== undefined ||
    search.stage !== undefined ||
    search.status !== undefined ||
    search.source !== undefined ||
    search.due !== undefined ||
    search.from !== undefined ||
    search.to !== undefined ||
    search.referral !== undefined
  )
}

/**
 * Toolbar → manzil.
 *
 * ⚠️ Qaytadigan tipdan `rejected` CHIQARIB TASHLANGAN, va bu tasodif
 * emas: natija sahifada avvalgi manzil ustiga yoyiladi
 * (`{ ...previous, ...filtersToSearch(next) }`). Bayroq bu ro'yxatga
 * tushsa, har filtr o'zgarishida `undefined` bo'lib ustiga yozilardi va
 * HR «Rad etilganlar» bo'limida vakansiyani tanlagan zahoti kundalik
 * ro'yxatga uchib chiqardi. Bo'lim bayrog'ini FAQAT menyu bandi va
 * manzilning o'zi qo'yadi.
 */
export function filtersToSearch(
  filters: CandidatesFilterState,
): Omit<CandidatesSearch, 'page' | 'sort' | 'view' | 'rejected'> {
  return {
    q: readText(filters.search),
    vacancy: emptyToUndefined(filters.vacancyId),
    stage: emptyToUndefined(filters.stageId),
    status: filters.status === '' ? undefined : filters.status,
    source: filters.source === '' ? undefined : filters.source,
    // Katakcha bo'shatilganda `undefined` YOZILISHI kerak — u avvalgi
    // manzildagi `due=true` ni o'chiradi.
    due: filters.contactDue ? true : undefined,
    from: emptyToUndefined(filters.from),
    to: emptyToUndefined(filters.to),
    referral: emptyToUndefined(filters.referral),
  }
}

/**
 * The «Qayerdan bilgan» filter as list/export parameters — one place, so the
 * table, the funnel and the Excel file filter the same way.
 */
export function referralFilter(referral: string): {
  referralChannelId?: string
  referralMissing?: boolean
} {
  if (referral === '') return {}
  return referral === REFERRAL_MISSING ? { referralMissing: true } : { referralChannelId: referral }
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
  nextContact: true,
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
 * Mantiqiy bayroq — `due` va `rejected` uchun (`employees-search.ts` dagi
 * `readFlag` bilan AYNAN bir xil qoida, ikkita ro'yxat bir xil o'qilsin).
 *
 * ⭐ IKKI xil qiymat qabul qilinadi. Router qidiruv parametrlarini JSON
 * sifatida o'qiydi, ya'ni `?rejected=true` bu yerga mantiqiy `true` bo'lib
 * keladi (`readPage` ning `typeof value !== 'number'` tekshiruvi ham shu
 * xatti-harakatga tayanadi). Lekin manzil qo'lda ham yozilishi mumkin —
 * satr varianti ham tan olinadi.
 *
 * Qolgan hamma narsa — `undefined`: yoqilmagan bayroq manzilda umuman
 * turmaydi (`?due=false` ni saqlash serverda hech narsani o'zgartirmagan
 * holda so'rov satrini shovqinga to'ldirardi).
 */
function readFlag(value: unknown): true | undefined {
  return value === true || value === 'true' ? true : undefined
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

/** `DateOnly` shakli — boshqasi backend'ga 400 bilan qaytardi. */
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/u

function readDate(value: unknown): string | undefined {
  return typeof value === 'string' && DATE_ONLY.test(value) ? value : undefined
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function readId(value: unknown): string | undefined {
  return typeof value === 'string' && UUID.test(value) ? value : undefined
}

/** A channel id or `none`; anything else is dropped. */
function readReferral(value: unknown): string | undefined {
  return value === REFERRAL_MISSING ? REFERRAL_MISSING : readId(value)
}

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
