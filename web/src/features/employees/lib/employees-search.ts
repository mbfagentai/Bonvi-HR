import type { EmployeeStatus } from '../api/employees-api'

/**
 * Xodimlar ro'yxatining holati — manzil satrida (URL search parametrlari).
 *
 * NEGA komponent ichidagi `useState` emas: `EmployeesPage` marshrut
 * komponenti, ya'ni xodim kartasiga o'tilganda u BUTUNLAY unmount bo'ladi.
 * Holat komponent ichida bo'lsa, «orqaga» bosgan HR 3-sahifadan
 * 1-sahifaga tushib qolardi — filtri va qidiruvi bilan birga. 45 emas,
 * 2300 xodim bo'lganda bu kunlik ishni buzadi.
 *
 * Manzilda saqlangan holat esa: brauzer tarixi bilan qaytadi, sahifa
 * yangilanganda saqlanadi va havola qilib bo'lishiladi («mana shu filtr
 * bilan ro'yxat»).
 *
 * Barcha maydonlar IXTIYORIY va standart qiymat manzilga yozilmaydi:
 * `?page=1&view=table&sort=fullName` — foydasi yo'q, shovqin ko'p.
 * TanStack Router `undefined` maydonlarni satrga umuman qo'shmaydi.
 */
export interface EmployeesSearch {
  /** 1 dan boshlanadi. 1 — standart, shu sababli manzilda ko'rinmaydi. */
  page?: number
  /** Qidiruv matni: F.I.Sh., JSHSHIR, telefon yoki pasport. */
  q?: string
  department?: string
  position?: string
  direction?: string
  /** Yuridik shaxs (firma). Manzilda qisqa nom — `legalEntity` juda uzun. */
  entity?: string
  /**
   * Yo'q — standart, ya'ni faqat doimiy xodimlar. `all` — holatiga
   * qaramasdan hammasi (sinovdagilar va ishdan ketganlar ham).
   */
  status?: EmployeesStatusFilter
  /** Faqat «tekshirish kerak» belgisi qo'yilganlar. */
  review?: boolean
  sort?: EmployeesSortParam
  view?: EmployeesView
}

/** `all` — «holat bo'yicha filtrlama», ya'ni butun baza. */
export type EmployeesStatusFilter = EmployeeStatus | 'all'

/**
 * Ro'yxatning standart holati: faqat DOIMIY xodimlar.
 *
 * NEGA: «xodimlar» bo'limi — kundalik ish ro'yxati, undagi odam hozir
 * ishlab turgan bo'lishi kerak. Sinovdagilar `/sinov` sahifasida
 * yakunlanadi, ishdan ketganlar esa tarix.
 *
 * Filtr paneldan boshqa holatni tanlash IMKONI QOLDIRILGAN va bu ataylab:
 * agar ishdan ketgan odamni tizimdan umuman topib bo'lmasa, HR uni
 * «bazada yo'q» deb o'ylab dublikat yaratadi — loyihaning eng asosiy
 * muammosi shundan boshlangan edi.
 */
export const DEFAULT_STATUS: EmployeeStatus = 'active'

export type EmployeesView = 'table' | 'cards'

/**
 * Server tanidigan saralash kalitlari.
 *
 * ⚠️ AYNAN shu uchtasi — noma'lum kalit yuborilsa 400 qaytishi mumkin.
 * Shu sababli ro'yxat manzildan kelgan qiymat uchun ham, jadval tugmasi
 * uchun ham shu ro'yxatni yagona filtr sifatida ishlatadi.
 *
 * `status` jadvalda endi ustun bo'lib ko'rinmaydi (ro'yxatda faqat doimiy
 * xodimlar turadi), lekin kalit qoldirildi: shunday havola bilan
 * bo'lishilgan bo'lsa, u ochilganda buzilmasin.
 */
export type EmployeesSortKey = 'fullName' | 'hireDate' | 'status'

/** «fullName» — o'sish, «-hireDate» — kamayish tartibida. */
export type EmployeesSortParam = EmployeesSortKey | `-${EmployeesSortKey}`

/**
 * Standart saralash: familiya bo'yicha alifbo tartibida.
 *
 * NEGA aynan shu: HR ro'yxatda odamni ISM bo'yicha izlaydi, sana bo'yicha
 * emas. Alifbo tartibi esa qidiruv maydoniga yozmasdan ham ko'z bilan
 * topish imkonini beradi.
 */
export const DEFAULT_SORT: EmployeesSortParam = 'fullName'

/** Toolbar ishlaydigan ko'rinish: bo'sh satr — «filtr qo'yilmagan». */
export interface EmployeesFilterState {
  search: string
  departmentId: string
  positionId: string
  directionId: string
  legalEntityId: string
  /** Bo'sh satr — «barcha holatlar» (`Select` dagi placeholder qiymati). */
  status: EmployeeStatus | ''
  needsReview: boolean
}

/**
 * «Filtrlarni tozalash» qaytadigan holat.
 *
 * Diqqat: holat BO'SH emas — tozalash ham standart ko'rinishga, ya'ni
 * doimiy xodimlarga qaytaradi. Aks holda «tozalash» ro'yxatni
 * kengaytirib, ishdan ketganlarni ham olib kelardi.
 */
export const DEFAULT_FILTERS: EmployeesFilterState = {
  search: '',
  departmentId: '',
  positionId: '',
  directionId: '',
  legalEntityId: '',
  status: DEFAULT_STATUS,
  needsReview: false,
}

/** Manzildagi filtr maydonlari — sahifa, saralash va ko'rinishdan tashqarisi. */
type EmployeesFilterSearch = Pick<
  EmployeesSearch,
  'q' | 'department' | 'position' | 'direction' | 'entity' | 'status' | 'review'
>

/**
 * Manzil satridan kelgan qiymatlarni tekshiradi.
 *
 * NEGA hech qachon xato tashlamaydi: `?page=abc` yoki `?status=xyz` ni
 * istalgan odam yozishi mumkin (yoki eski havola shunday qolgan bo'ladi).
 * Bunda ekran «oq sahifa» bo'lib qolmasligi kerak — noto'g'ri qiymat
 * shunchaki tashlab yuboriladi va standart qiymat ishlaydi.
 */
export function validateEmployeesSearch(search: Record<string, unknown>): EmployeesSearch {
  return {
    page: readPage(search['page']),
    q: readText(search['q']),
    department: readId(search['department']),
    position: readId(search['position']),
    direction: readId(search['direction']),
    entity: readId(search['entity']),
    status: readStatus(search['status']),
    review: readFlag(search['review']),
    sort: normalizeSort(search['sort']),
    view: readView(search['view']),
  }
}

/**
 * Manzil → toolbar. Bo'sh maydon bu yerda bo'sh SATR bo'ladi.
 *
 * Holat teskari o'giriladi: manzildagi `all` toolbar uchun bo'sh satr
 * («barcha holatlar»), manzilda holat umuman bo'lmasa — standart `active`.
 */
export function searchToFilters(search: EmployeesSearch): EmployeesFilterState {
  return {
    search: search.q ?? '',
    departmentId: search.department ?? '',
    positionId: search.position ?? '',
    directionId: search.direction ?? '',
    legalEntityId: search.entity ?? '',
    status: search.status === 'all' ? '' : (search.status ?? DEFAULT_STATUS),
    needsReview: search.review ?? false,
  }
}

/**
 * Foydalanuvchi standart ko'rinishdan chetga chiqqanmi.
 *
 * Standart qiymatlar manzilda saqlanmagani uchun tekshiruv oddiy: biror
 * filtr maydoni manzilda bor bo'lsa — u ataylab tanlangan. Sahifa,
 * saralash va ko'rinish hisobga OLINMAYDI: ular ro'yxatni qisqartirmaydi,
 * ya'ni bo'sh natijaning sababi ham bo'lolmaydi.
 */
export function isFiltered(search: EmployeesSearch): boolean {
  return (
    search.q !== undefined ||
    search.department !== undefined ||
    search.position !== undefined ||
    search.direction !== undefined ||
    search.entity !== undefined ||
    search.status !== undefined ||
    search.review !== undefined
  )
}

/**
 * Toolbar → manzil.
 *
 * Bo'sh maydon `undefined` bo'lib qaytadi va shu holda manzildan
 * BUTUNLAY yo'qoladi — aks holda tozalangan filtr `?department=` bo'lib
 * osilib qolardi.
 */
export function filtersToSearch(filters: EmployeesFilterState): EmployeesFilterSearch {
  return {
    q: readText(filters.search),
    department: emptyToUndefined(filters.departmentId),
    position: emptyToUndefined(filters.positionId),
    direction: emptyToUndefined(filters.directionId),
    entity: emptyToUndefined(filters.legalEntityId),
    // Bo'sh satr — «barcha holatlar»: bu STANDART EMAS, shu sababli
    // manzilda `all` bo'lib ko'rinadi; standart `active` esa yozilmaydi
    status: filters.status === '' ? 'all' : statusToSearch(filters.status),
    review: filters.needsReview ? true : undefined,
  }
}

function statusToSearch(status: EmployeeStatus): EmployeesStatusFilter | undefined {
  return status === DEFAULT_STATUS ? undefined : status
}

/**
 * Noma'lum qiymatdan saralash parametri.
 *
 * Ikki holatda `undefined` qaytadi va ikkalasi ham «standart saralash»
 * degani: (1) kalitni server tanimaydi — u holda so'rov 400 bo'lib
 * qaytmasin, (2) kalit aynan standart — u holda manzilda `?sort=fullName`
 * bo'lib osilib turishining ma'nosi yo'q.
 *
 * Manzildan o'qishda ham, jadval tugmasi bosilganda ham SHU funksiya
 * ishlaydi: qoida bitta joyda turadi.
 */
export function normalizeSort(value: unknown): EmployeesSortParam | undefined {
  if (typeof value !== 'string') return undefined

  const desc = value.startsWith('-')
  const key = desc ? value.slice(1) : value

  if (!isSortKey(key)) return undefined

  const param: EmployeesSortParam = desc ? `-${key}` : key
  return param === DEFAULT_SORT ? undefined : param
}

/*
 * Ruxsat etilgan qiymatlar massiv emas, KALITLI obyekt.
 *
 * NEGA: `Record<Union, true>` yangi qiymat qo'shilganda TypeScript'ni shu
 * yerni ham to'ldirishga majbur qiladi. `['a','b'].includes(x)` esa jimgina
 * eskirib qolardi — yangi holat qo'shilsa filtr uni tashlab yuborar,
 * lekin hech qanday xato chiqmasdi.
 */
const SORT_KEYS: Record<EmployeesSortKey, true> = {
  fullName: true,
  hireDate: true,
  status: true,
}

const STATUSES: Record<EmployeeStatus, true> = {
  probation: true,
  active: true,
  terminated: true,
}

const VIEWS: Record<EmployeesView, true> = {
  table: true,
  cards: true,
}

function isSortKey(value: string): value is EmployeesSortKey {
  return Object.hasOwn(SORT_KEYS, value)
}

function isStatus(value: string): value is EmployeeStatus {
  return Object.hasOwn(STATUSES, value)
}

function isView(value: string): value is EmployeesView {
  return Object.hasOwn(VIEWS, value)
}

/**
 * Sahifa raqami.
 *
 * 1 dan kichigi ham, kasr ham, `NaN` ham qabul qilinmaydi — barchasi
 * birinchi sahifaga tushadi. Yuqori chegara bu yerda tekshirilmaydi:
 * oxirgi sahifa nechanchiligi `total` kelmaguncha noma'lum, shu sababli
 * ortiqcha raqamni ro'yxat sahifasi ma'lumot kelgach o'zi qisadi.
 */
function readPage(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 2) return undefined
  return value
}

/**
 * Qidiruv matni.
 *
 * Faqat bo'sh (yoki bir nechta probeldan iborat) qiymat tashlanadi, matn
 * esa QIRQILMAYDI. NEGA: qirqilsa «Abbos » deb yozilgan probel 350 ms dan
 * keyin manzildan qaytib kelib maydondan yo'qolardi — foydalanuvchi
 * hali familiyasini yozib ulgurmasidan. Ortiqcha probelni server o'zi
 * hisobga olmaydi.
 */
function readText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined
}

/*
 * Ma'lumotnoma identifikatorlari — UUID.
 *
 * NEGA shakl tekshiriladi: manzildagi tasodifiy matn to'g'ridan-to'g'ri
 * so'rovga tushsa server 400 qaytaradi va HR «yuklab bo'lmadi» xatosini
 * ko'radi. Shaklga tushmagan qiymatni bu yerda tashlab yuborish
 * arzonroq — filtr shunchaki qo'llanmaydi.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function readId(value: unknown): string | undefined {
  return typeof value === 'string' && UUID.test(value) ? value : undefined
}

/**
 * Holat filtri: `all` yoki bitta holat.
 *
 * `active` — standart, shu sababli u ham `undefined` ga aylanadi:
 * manzilda `?status=active` ko'rinmaydi, natija esa aynan bir xil.
 */
function readStatus(value: unknown): EmployeesStatusFilter | undefined {
  if (value === 'all') return 'all'
  if (typeof value !== 'string' || !isStatus(value)) return undefined
  return value === DEFAULT_STATUS ? undefined : value
}

function readView(value: unknown): EmployeesView | undefined {
  // 'table' — standart ko'rinish, u manzilda saqlanmaydi
  if (typeof value !== 'string' || !isView(value)) return undefined
  return value === 'table' ? undefined : value
}

/*
 * `?review=true` → mantiqiy `true` bo'lib keladi (router qiymatlarni
 * JSON sifatida o'qiydi), lekin qo'lda yozilgan havolada satr bo'lishi
 * ham mumkin. Faqat «yoqilgan» holat manzilda turadi.
 */
function readFlag(value: unknown): boolean | undefined {
  return value === true || value === 'true' ? true : undefined
}

function emptyToUndefined(value: string): string | undefined {
  return value === '' ? undefined : value
}
