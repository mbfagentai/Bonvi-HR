/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN. `./bonvi gen` bilan almashtiriladi.
 *
 * Yozilgan payt (22.08.2026) `GET /openapi/v1.json` da xodimlar endpoint'lari
 * HALI YO'Q — faqat auth va ma'lumotnomalar bor. Shu sababli tiplar
 * OpenAPI'dan emas, backend'ning DOMEN QATLAMIDAN olingan:
 *   api/src/Bonvi.Domain/Employees/{Employee,Employment,EmploymentEvent,EmployeeFlag}.cs
 * Ya'ni maydon nomlari va ma'nolari taxmin emas — ular haqiqiy modeldan
 * ko'chirilgan. Taxminiy qism faqat bitta: DTO qatlami bu maydonlarni
 * QANDAY yassilashi (masalan `FullName` obyektimi yoki tayyor satrmi).
 *
 * Backend tayyor bo'lgach:
 *   1. `./bonvi gen` — `@/shared/api/generated/employees/*` hosil bo'ladi
 *   2. Shu fayl O'CHIRILADI, `employees-queries.ts` generatsiya qilingan
 *      hook'larga o'tkaziladi
 *   3. Quyidagi «kutilayotgan shartnoma» izohlari tekshiriladi
 */
import axios from 'axios'

import { api } from '@/shared/api/http-client'
import { fileNameFromDisposition } from '@/shared/lib/download'

/* ═══════════════════════════════════════════════════════════ SANOQ TIPLARI */

/**
 * NEGA satr birlashmasi, C# enum'ining o'zi emas: `Bonvi.Api` da hozircha
 * `JsonStringEnumConverter` ro'yxatdan o'tkazilmagan, ya'ni `System.Text.Json`
 * enum'ni RAQAM qilib yuboradi (`EmploymentStatus.Active` → `2`). Backend
 * agenti konverter qo'shsa — satr keladi (`"Active"`).
 *
 * Ikkalasini ham qabul qilamiz (`enumValue`): shartnoma qaysi tomonga
 * hal bo'lishidan qat'i nazar interfeys ishlaydi va `./bonvi gen` dan
 * keyin faqat shu joy tozalanadi.
 */
export const EMPLOYEE_STATUSES = ['probation', 'active', 'terminated'] as const
export type EmployeeStatus = (typeof EMPLOYEE_STATUSES)[number]

export const EMPLOYMENT_EVENT_TYPES = [
  'hire',
  'transfer',
  'promotion',
  'additionalDuty',
  'salaryChange',
  'probationResult',
  'termination',
  'rehire',
] as const
export type EmploymentEventType = (typeof EMPLOYMENT_EVENT_TYPES)[number]

export const EMPLOYEE_FLAG_TYPES = [
  'reserve',
  'blacklist',
  'monitoring',
  'capable',
] as const
export type EmployeeFlagType = (typeof EMPLOYEE_FLAG_TYPES)[number]

export const GENDERS = ['male', 'female'] as const
export type Gender = (typeof GENDERS)[number]

/**
 * Nomzodning bosqichdagi natijasi (`StageOutcome`).
 *
 * Tartib C# enum'ining RAQAMLI qiymatlariga mos (1 dan): `Pending = 1`
 * → `names[0]` — `enumValue` shu qoidaga tayanadi.
 */
export const STAGE_OUTCOMES = ['pending', 'passed', 'failed'] as const
export type StageOutcome = (typeof STAGE_OUTCOMES)[number]

/* ═══════════════════════════════════════════════════════════════ MODELLAR */

/** Pul: `Money` value object'ining yassi ko'rinishi. */
export interface MoneyValue {
  amount: number
  currency: string
}

/** Ma'lumotnomaga havola — ro'yxatda nom, tahrirlashda `id` kerak. */
export interface NamedRef {
  id: string
  name: string
}

/** Xodimlar ro'yxatidagi bitta qator. Kartochka ko'rinishi ham shundan chiziladi. */
export interface EmployeeListItem {
  id: string
  /** «Мамажонов Бобуржон Фазлитдин ўғли». */
  fullName: string
  /** «Мамажонов Б.Ф.» — tor ustunlar uchun. Server bermasa — `null`. */
  shortName: string | null
  /**
   * Rasmning SAQLASH KALITI — havola emas.
   *
   * Rasmning o'zi alohida so'rov bilan olinadi (`fetchEmployeePhoto`),
   * chunki tizim `Authorization` sarlavhasi bilan ishlaydi va brauzer uni
   * `<img src>` so'roviga qo'shmaydi. Kalit esa ikki ish qiladi: rasm
   * bor-yo'qligini bildiradi (`null` bo'lsa so'rov umuman yuborilmaydi) va
   * kesh kalitiga kirib, rasm almashtirilganda keshni o'z-o'zidan eskirtiradi.
   */
  photoKey: string | null
  positionName: string | null
  departmentName: string | null
  legalEntityName: string | null
  /** `DateOnly` → «2024-01-15». */
  hireDate: string | null
  status: EmployeeStatus
  /** Excel'dan ko'chirilgan va tekshirilishi kerak bo'lgan yozuv. */
  needsReview: boolean
  pinfl: string | null
  phone: string | null
  directions: NamedRef[]
  /** Faqat KUCHDAGI belgilar — ro'yxatda ogohlantirish uchun. */
  activeFlags: EmployeeFlagType[]
}

/** Bitta mehnat munosabati. Bitta xodimda bir nechta bo'lishi mumkin. */
export interface EmploymentSummary {
  id: string
  legalEntity: NamedRef | null
  department: NamedRef | null
  position: NamedRef | null
  hireDate: string
  status: EmployeeStatus
  /** 1.0 — to'liq stavka, 0.5 — o'rindoshlik. */
  rate: number
  isPrimary: boolean
  /**
   * ⚠️ `salary.view` ruxsati yo'q foydalanuvchiga server `null` yuborishi
   * kerak. Klient ham baribir maydonni ko'rsatmaydi — ikki qavatli himoya.
   */
  salary: MoneyValue | null
  hireOrderNo: string | null
  hireOrderDate: string | null
  terminationDate: string | null
  terminationReasonName: string | null
  /** Sinov muddati tugash sanasi (`Probation` holatidagilar uchun). */
  probationEndDate: string | null
  /**
   * Sinov muddati KETISH bilan yakunlanganmi («o'tolmadi» yoki «o'zi ketdi»).
   *
   * Bu maydon bitta jimgina nomuvofiqlikni ko'rsatish uchun: sinovni
   * yakunlash mehnat munosabatini TUGATMAYDI (u alohida buyruq bilan
   * rasmiylashtiriladi), ya'ni sinovdan yiqilgan odam `/sinov`
   * ro'yxatidan chiqib ketadi-yu, xodimlar ro'yxatida «Sinovda» bo'lib
   * turaverardi. Karta endi buni ochiq aytadi va bo'shatish tugmasini
   * yonida ko'rsatadi.
   */
  probationEndedByLeaving: boolean
  /**
   * OCHIQ sinov muddatining identifikatori — kartadagi «yakunlash» va
   * «uzaytirish» amallari shusiz ishlamaydi.
   *
   * <b>Yakunlangan sinovda ATAYLAB `null`.</b> «Sinov bormi» degan savolga
   * emas, «unga hali amal qilish mumkinmi» degan savolga javob beradi:
   * domen yakunlangan muddatni na yopadi, na uzaytiradi. Alohida `status`
   * maydoni bo'lsa, uni tekshirishni har bir chaqiruv joyida takrorlashga
   * to'g'ri kelardi va bittasida unutilishi aniq edi — u holda tugma
   * ko'rinardi-yu, bosilganda xato chiqardi.
   */
  probationId: string | null
  needsReview: boolean
}

/** Xodim kartasi — shaxs ma'lumotlari va uning barcha mehnat munosabatlari. */
export interface EmployeeDetails {
  id: string
  lastName: string
  firstName: string
  middleName: string | null
  fullName: string
  shortName: string | null
  /** Saqlash kaliti — `EmployeeListItem.photoKey` bilan bir xil ma'noda. */
  photoKey: string | null
  birthDate: string | null
  gender: Gender | null
  pinfl: string | null
  passport: string | null
  phone: string | null
  secondPhone: string | null
  address: string | null
  speciality: string | null
  telegramUrl: string | null
  /** Sudlanganlik haqidagi erkin matn (Excel: «Судланганлик»). */
  criminalRecordNote: string | null
  /** Tekshiruvdan o'tganmi (Excel: «Текширув»). `null` — ma'lum emas. */
  /** Sudlanganmi: `true` / `false` / `null` — noma'lum. */
  hasCriminalRecord: boolean | null
  /**
   * mehnat.uz profiliga havola. `null` — kiritilmagan.
   *
   * Ilgari uning yonida «mehnat.uz: ha/yo'q» degan alohida javob ham
   * bor edi. U olib tashlandi: bir xil savol ekranda ikki marta
   * so'ralardi, holbuki havola va skrinshot javobning O'ZI ham,
   * dalili ham bo'lib xizmat qiladi.
   */
  mehnatUzUrl: string | null
  /**
   * Skrinshotning SAQLASH KALITI — havola emas.
   *
   * `photoKey` bilan aynan bir xil ma'noda va bir xil sababdan: tizim
   * `Authorization` sarlavhasi bilan ishlaydi, brauzer esa `<img src>`
   * so'roviga uni qo'sha olmaydi. Kalit skrinshot bor-yo'qligini bildiradi
   * (`null` bo'lsa so'rov yuborilmaydi) va kesh kalitiga kirib, fayl
   * almashtirilganda keshni o'z-o'zidan eskirtiradi.
   */
  mehnatUzScreenshotKey: string | null
  directions: NamedRef[]
  /**
   * Yo'nalishlarning faqat `id` lari — tahrirlash formasi uchun.
   *
   * NEGA `directions` dan alohida: karta javobida yo'nalishlar NOMSIZ,
   * faqat `directionIds` bo'lib keladi (`EmployeeDto.DirectionIds`).
   * Ko'rsatish uchun nom kerak, tanlovni oldindan to'ldirish uchun esa
   * `id` — ikkalasini bitta maydonga siqib bo'lmaydi.
   */
  directionIds: string[]
  needsReview: boolean
  /** Nima tekshirilishi kerakligi — ETL hisobotidan kelgan matn. */
  reviewNote: string | null
  employments: EmploymentSummary[]
}

/**
 * Mehnat yo'lidagi hodisa.
 *
 * Bu tizimning yuragi: bitta hodisa yoziladi, qolgan barcha ro'yxatlar
 * (yangi kelganlar, lavozimi o'zgarganlar, arxiv) undan hosil bo'ladi.
 */
export interface TimelineEvent {
  id: string
  employmentId: string
  type: EmploymentEventType
  /** Kuchga kirgan sana — buyruq sanasi bilan bir xil bo'lmasligi mumkin. */
  effectiveDate: string
  orderNo: string | null
  orderDate: string | null
  fromDepartmentName: string | null
  toDepartmentName: string | null
  fromPositionName: string | null
  toPositionName: string | null
  fromSalary: MoneyValue | null
  toSalary: MoneyValue | null
  /** Izoh. Qo'shimcha vazifada — vazifaning matni. */
  note: string | null
  /** Excel'dan ko'chirilgan tarixiy yozuv — aniqligi kafolatlanmaydi. */
  isImported: boolean
  recordedAt: string | null
  recordedByName: string | null
  legalEntityName: string | null
}

export interface EmployeeFlag {
  id: string
  employeeId: string
  type: EmployeeFlagType
  startDate: string
  /** `null` — muddatsiz, hali yopilmagan. */
  endDate: string | null
  reason: string | null
  /** Server bugungi sanaga nisbatan hisoblaydi. */
  isActive: boolean
}

/* ═════════════════════════════════════════════════════════ SO'ROV TIPLARI */

/**
 * Ro'yxatni TORAYTIRADIGAN parametrlar — sahifalashsiz.
 *
 * NEGA sahifalashdan ajratilgan: aynan shu to'plam Excel eksportiga ham
 * boradi (`exportEmployees`), eksport esa sahifalanmaydi — u butun mos
 * ro'yxatni bitta faylda beradi. Ikkala so'rov bitta tipdan va bitta
 * yig'uvchi funksiyadan (`filterParams`) foydalansa, yangi filtr
 * qo'shilganda u ikkalasiga ham O'ZI tushadi va «ekranda boshqa, faylda
 * boshqa» holati kelib chiqmaydi. Backend ham xuddi shu sababdan filtr
 * qurish kodini bo'lishadi — `SearchEmployeesQueryHandler.BuildFilter`.
 */
export interface EmployeeFilterParams {
  search?: string
  departmentId?: string
  positionId?: string
  directionId?: string
  legalEntityId?: string
  status?: EmployeeStatus
  needsReview?: boolean
  /** «fullName» — o'sish, «-hireDate» — kamayish tartibida. */
  sort?: string
}

export interface EmployeeListFilters extends EmployeeFilterParams {
  page: number
  pageSize: number
}

/** Eksport javobi: baytlar va ular qanday nom bilan saqlanishi. */
export interface EmployeeExportFile {
  blob: Blob
  fileName: string
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/**
 * Ishga qabul — HR ning «bitta blanka» talabi. Bitta so'rovda hammasi.
 *
 * ⚠️ Bu FORMA shakli, so'rov tanasi EMAS. Forma yassi: yigirmata maydon
 * bitta obyektda yotadi va `hire-dialog.tsx` uni shundayligicha yig'adi.
 * Server esa shaxsni ichma-ich `person` obyektida kutadi
 * (`HireEmployeeCommand.Person`). O'girish `toHireBody` da — pastdagi
 * izohga qarang.
 */
export interface HireRequest {
  /**
   * Mavjud shaxsni qayta ishga olishda to'ldiriladi. `null` bo'lsa server
   * yangi `Employee` yaratadi (JSHSHIR unikalligi bilan).
   */
  employeeId: string | null

  // ── 1-bosqich: shaxs ──
  lastName: string
  firstName: string
  middleName: string | null
  birthDate: string | null
  pinfl: string | null
  passport: string | null
  phone: string | null
  secondPhone: string | null
  address: string | null
  speciality: string | null
  /** ⚠️ Serverda maydon nomi boshqacha — `telegramUrl` (`toHireBody`). */
  telegram: string | null
  directionIds: string[]

  // ── 2-bosqich: ish joyi ──
  legalEntityId: string
  departmentId: string | null
  positionId: string | null
  hireDate: string
  rate: number
  isPrimary: boolean

  // ── 3-bosqich: oklad va buyruq ──
  /** `null` — oklad kiritilmagan yoki `salary.view` ruxsati yo'q. */
  salaryAmount: number | null
  salaryCurrency: string
  orderNo: string | null
  orderDate: string | null
}

/**
 * Xodim kartasini tahrirlash (`PUT /api/employees/{id}`).
 *
 * <b>To'liq almashtirish, «patch» emas.</b> Server buyruqning har bir
 * maydonini xodimga qo'yadi, shu sababli yuborilmagan maydon «tegilmadi»
 * emas, «bo'shatildi» degani. Forma barcha maydonlarni to'ldirilgan
 * holda ochilishi va hammasini qaytarib yuborishi shundan.
 *
 * ⚠️ To'rtta maydon bu qoidadan CHETDA — ularda `null` «tegilmasin»
 * degani: `birthDate`, `photoKey`, `gender`, `directionIds`. Sababi
 * har birining o'z izohida. Shunga tayanib maydonni tushirib qoldirish
 * MUMKIN EMAS: qolgan o'n bittasida bitta unutilgan maydon jonli
 * ma'lumotni jimgina o'chiradi.
 */
export interface UpdateEmployeeRequest {
  lastName: string
  firstName: string
  middleName: string | null
  /**
   * Tug'ilgan sana — TAHRIRLANADI, lekin boshqa maydonlardan farqli
   * qoida bo'yicha.
   *
   * `null` bu yerda «bo'shat» emas, «tegilmasin» degani: sanani
   * o'chirish HR uchun ma'noli amal emas, tuzatish esa — ha.
   * Server sanani JSHSHIR bilan BIRGA baholaydi
   * (`Employee.SetBirthDateAndPinfl`), ya'ni ikkalasi bir so'rovda
   * kelganda tekshiruv YANGI juftlikni ko'radi. Bittasi eski qolib
   * mos kelmasa — `xodim.jshshir_sana_mos_emas` (400).
   */
  birthDate: string | null
  /**
   * `null` — «ko'rsatilmagan», va u mavjud qiymatni O'CHIRMAYDI:
   * server `SetGender` ni faqat qiymat kelganda chaqiradi. Jins
   * odatda JSHSHIRdan o'zi aniqlanadi, shu sababli uni qo'lda
   * bo'shatish yo'li ataylab qoldirilmagan.
   */
  gender: Gender | null
  pinfl: string | null
  passport: string | null
  phone: string | null
  secondPhone: string | null
  address: string | null
  speciality: string | null
  telegramUrl: string | null
  /**
   * Rasm bu forma orqali O'ZGARTIRILMAYDI — u alohida endpoint bilan
   * boshqariladi (`POST`/`DELETE /api/employees/{id}/photo`).
   *
   * Server bo'sh kalitni e'tiborsiz qoldiradi
   * (`if (!string.IsNullOrWhiteSpace(command.PhotoKey))`), ya'ni bu
   * yagona maydon bo'ylab «yuborilmadi» = «tegilmasin». Baribir
   * kartadagi JORIY kalit qaytariladi: so'rov kartaning to'liq holatini
   * ko'rsatib tursin.
   */
  photoKey: string | null
  criminalRecordNote: string | null
  /** `null` — «ma'lum emas», `false` — «tekshiruvdan o'tmagan». */
  /** Sudlanganmi: `true` / `false` / `null` — noma'lum. */
  hasCriminalRecord: boolean | null
  /**
   * mehnat.uz havolasi.
   *
   * Bu maydon oddiy qoidaga bo'ysunadi (`photoKey` dan farqli): bo'sh
   * yuborish HAVOLANI O'CHIRADI. Ataylab shunday — noto'g'ri manzil
   * kiritilgan bo'lsa, HR uni olib tashlay olishi kerak; skrinshotdan
   * farqli o'laroq havola uchun alohida «o'chirish» endpoint'i yo'q.
   *
   * Server `https://` talab qiladi (sxemasiz matnga uni o'zi qo'shadi),
   * aks holda `xodim.mehnat_havola` (400).
   */
  mehnatUzUrl: string | null
  /**
   * To'liq ro'yxat — server eskilarini tozalab, kelganini biriktiradi.
   * Bo'sh massiv «yo'nalishlarni olib tashla» degani va bu ataylab
   * shunday; «yubormaslik» esa (`null`) «tegilmasin». Forma doim
   * massiv yuboradi, ya'ni tanlovni bo'shatish ishlaydi.
   */
  directionIds: string[]
}

export interface TerminateRequest {
  terminationDate: string
  /**
   * Ketish sababi.
   *
   * Nomi ATAYLAB `reasonId` — server tanasi shunday
   * (`TerminateRequest(DateOnly? TerminationDate, Guid? ReasonId, …)`).
   * Ilgari bu yerda `terminationReasonId` deb yozilgan edi va u
   * serverga umuman yetib bormasdi: JSON'da mos maydon topilmay, sabab
   * doim bo'sh qolardi. Bo'shatish hali ekranga ulanmagani uchun bu
   * jimgina turgan.
   */
  reasonId: string | null
  orderNo: string | null
  orderDate: string | null
  note: string | null
}

/** Lavozim yoki bo'lim o'zgarishi (Excel: «91.3»). */
export interface TransferRequest {
  effectiveDate: string
  departmentId: string | null
  positionId: string | null
  salaryAmount: number | null
  orderNo: string | null
  orderDate: string | null
  note: string | null
}

/** Qo'shimcha vazifa berish (Excel: «91.4»). */
export interface AdditionalDutyRequest {
  duty: string
  effectiveDate: string
  extraSalaryAmount: number | null
  orderNo: string | null
  orderDate: string | null
}

/** Oklad o'zgarishi. */
export interface ChangeSalaryRequest {
  /** `null` — oklad olib tashlanadi (masalan ishbay to'lovga o'tdi). */
  amount: number | null
  effectiveDate: string
  orderNo: string | null
  orderDate: string | null
}

export interface CreateFlagRequest {
  type: EmployeeFlagType
  startDate: string
  endDate: string | null
  reason: string | null
  /**
   * Kuchdagi belgini ALMASHTIRISHGA rozilik.
   *
   * Xodimda bir vaqtda atigi bitta kuchdagi belgi bo'ladi (qora
   * ro'yxatdagi odam rezervda ham bo'lolmaydi). Eskisi bo'lsa, tasdiqsiz
   * so'rov `belgi.boshqa_belgi_bor` (409) bilan qaytadi; shu bayroq bilan
   * esa eskisi yopilib yangisi ochiladi — ikkalasi bitta tranzaksiyada.
   *
   * ⚠️ Interfeysdagi tasdiqlash faqat QULAYLIK. Asosiy himoya serverda:
   * bu maydonsiz yuborilgan so'rov ham to'xtatiladi.
   */
  confirm: boolean
}

/**
 * Xodimning nomzodlik davridagi bitta bosqich yozuvi («Kengash xulosasi»).
 *
 * FAQAT O'QISH: bu yozuvni xodim kartasidan yaratib ham, tahrirlab ham
 * bo'lmaydi — u rekruting voronkasida, odam hali nomzod bo'lgan paytda
 * yozilgan. Xodim rekruting orqali kelmagan bo'lsa ro'yxat BO'SH keladi
 * va bu xato emas.
 */
export interface EmployeeStageEntry {
  id: string
  candidateId: string
  stageId: string
  /** Bosqich nomi. `null` — ma'lumotnomadan topilmadi (o'chirilgan). */
  stage: string | null
  /** Bosqich o'tkazilgan sana — kengash kuni, suhbat kuni. */
  date: string
  outcome: StageOutcome
  /** Xulosa matni. `null` — hali yozilmagan («kutilmoqda» yozuvida odatiy). */
  conclusion: string | null
  recordedBy: string | null
  /** Kim yozgani — tizim foydalanuvchisining F.I.Sh. */
  recordedByName: string | null
  recordedAt: string
}

/* ══════════════════════════════════════════════════════════ NORMALIZATSIYA */

/**
 * Serverdan kelgan sanoq qiymatini frontend birlashmasiga keltiradi.
 *
 * `names` tartibi C# enum'ining RAQAMLI qiymatlariga mos (1 dan boshlanadi):
 * `EmploymentStatus.Probation = 1` → `names[0]`. Shu sababli raqam ham,
 * nom ham tushuniladi.
 */
/** Belgilar massivi — noma'lum qiymat tashlab yuborilmaydi, tanilgani olinadi. */
function flagList(raw: unknown): EmployeeFlagType[] {
  return Array.isArray(raw)
    ? raw.map((flag) => enumValue(flag, EMPLOYEE_FLAG_TYPES, 'monitoring'))
    : []
}

function enumValue<T extends string>(
  raw: unknown,
  names: readonly T[],
  fallback: T,
): T {
  if (typeof raw === 'number') {
    return names[raw - 1] ?? fallback
  }

  if (typeof raw === 'string') {
    const needle = raw.toLowerCase()
    return names.find((name) => name.toLowerCase() === needle) ?? fallback
  }

  return fallback
}

/**
 * Frontend birlashmasini C# enum nomiga qaytaradi: «male» → «Male».
 *
 * ASP.NET Core enum'ni NOM bo'yicha bog'laydi. Ro'yxat qaytadan
 * yozilmaydi — bosh harf qoidasi ikkala tomonda ham bir xil va
 * shu sababli hisoblab olinadi.
 */
function enumName(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

/**
 * Sahifalangan javobni bir ko'rinishga keltiradi.
 *
 * NEGA kerak: `PagedResult` uchun .NET tomonida yagona kelishuv yo'q —
 * `items`/`total` ham, `data`/`totalCount` ham keng tarqalgan. Shartnoma
 * hali qotmagan ekan, bitta nomga qattiq bog'lanib qolib butun sahifani
 * yiqitgandan ko'ra, ikkalasini ham o'qigan ma'qul. `./bonvi gen` dan
 * keyin bu funksiya o'chadi.
 */
function readPage<TRaw, TItem>(
  data: unknown,
  map: (raw: TRaw) => TItem,
  fallbackPage: number,
  fallbackPageSize: number,
): PagedResult<TItem> {
  // Server sahifalashsiz oddiy massiv qaytarsa ham ro'yxat ko'rinadi
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

/** `null`, `undefined` va bo'sh satrni bitta ko'rinishga — `null` ga keltiradi. */
function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function bool(value: unknown): boolean {
  return value === true
}

/** `bool?` — uch holatli: ha / yo'q / ma'lum emas. */
function nullableBool(value: unknown): boolean | null {
  return typeof value === 'boolean' ? value : null
}

function number(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

/**
 * Pulni o'qiydi. Server `Money` ni obyekt (`{amount, currency}`) yoki
 * oddiy son qilib berishi mumkin — ikkalasi ham tushuniladi.
 */
function money(value: unknown): MoneyValue | null {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return { amount: value, currency: 'UZS' }
  }

  if (value === null || typeof value !== 'object') return null

  const box = value as Record<string, unknown>
  const amount = firstNumber(box['amount'], box['value'])
  if (amount === null) return null

  return { amount, currency: text(box['currency']) ?? 'UZS' }
}

function namedRefs(value: unknown): NamedRef[] {
  if (!Array.isArray(value)) return []

  return value.flatMap((raw) => {
    const box = (raw ?? {}) as Record<string, unknown>
    const id = text(box['id'])
    const name = text(box['name'])
    return id === null || name === null ? [] : [{ id, name }]
  })
}

function namedRef(value: unknown): NamedRef | null {
  const [first] = namedRefs([value])
  return first ?? null
}

/**
 * Ma'lumotnoma nomini oladi — qiymat qanday kelishidan qat'i nazar.
 *
 * Ro'yxat endpoint'i bo'lim, lavozim va yuridik shaxsni **tekis satr**
 * qilib qaytaradi (`"department": "1-Департамент"`), profil endpoint'i esa
 * `{ id, name }` obyekti sifatida. Ilgari bu yerda faqat obyekt shakli
 * sinalardi va satr kelganda ustun jimgina bo'sh chiqardi — jadvalda uchta
 * ustun to'liq «—» bo'lib turgan edi.
 */
function refName(value: unknown): string | null {
  return text(value) ?? namedRef(value)?.name ?? null
}

/* ─────────────────────────────────────────────────────── xaritalash (map) */

function toListItem(raw: unknown): EmployeeListItem {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    fullName: text(box['fullName']) ?? '—',
    shortName: text(box['shortName']),
    photoKey: text(box['photoKey']),
    positionName: text(box['positionName']) ?? refName(box['position']),
    departmentName: text(box['departmentName']) ?? refName(box['department']),
    legalEntityName: text(box['legalEntityName']) ?? refName(box['legalEntity']),
    hireDate: text(box['hireDate']),
    status: enumValue(box['status'], EMPLOYEE_STATUSES, 'active'),
    needsReview: bool(box['needsReview']),
    pinfl: text(box['pinfl']),
    phone: text(box['phone']),
    directions: namedRefs(box['directions']),

    /*
      ⚠️ Server maydonni «flags» deb ataydi, «activeFlags» deb emas.
      Ilgari bu yerda faqat «activeFlags» o'qilardi va ro'yxatdagi
      «Belgilar» ustuni HAR DOIM bo'sh chiqardi — «Rezerv», «Qora
      ro'yxat» va «Nazorat» nishonlari ekranga umuman tushmasdi.
      Hech qanday xato ham chiqmasdi: bo'sh massiv bo'sh ustun demak.

      Ikkala nom ham o'qiladi — eski nom shartnoma qaytib kelishi
      ehtimoliga qarshi emas, balki xodim KARTASI (`EmployeeDto`) uni
      shu nom bilan berishi mumkinligi uchun.
    */
    activeFlags: flagList(box['flags'] ?? box['activeFlags']),
  }
}

/**
 * Ma'lumotnomaga havolani yig'adi — server qanday shaklda bersa ham.
 *
 * `EmploymentDto` nomni va identifikatorni ALOHIDA maydonda beradi
 * (`legalEntityId` + `legalEntity: "MBF"`), boshqa endpoint'lar esa
 * tayyor `{ id, name }` obyektini. Ilgari bu yerda faqat obyekt shakli
 * sinalardi va natijada xodim kartasidagi mehnat munosabati kartochkasida
 * firma, bo'lim va lavozim jimgina BO'SH chiqardi.
 */
function refPair(box: Record<string, unknown>, idKey: string, nameKey: string): NamedRef | null {
  const asObject = namedRef(box[nameKey])
  if (asObject !== null) return asObject

  const name = text(box[nameKey])
  if (name === null) return null

  return { id: text(box[idKey]) ?? '', name }
}

/**
 * Sinov muddati OCHIQ bo'lsa — uning identifikatori, aks holda `null`.
 *
 * NEGA holat shu yerda tekshiriladi: kartadagi amallar («yakunlash»,
 * «uzaytirish») faqat davom etayotgan sinovda ma'noga ega va domen
 * yakunlanganini baribir rad etadi. Shartni bitta joyda yechib, tashqariga
 * «amal qilish mumkinmi» degan bitta qiymat chiqarish — uni har bir
 * chaqiruv joyida qaytarish xavfini butunlay yo'q qiladi.
 */
function openProbationId(probation: Record<string, unknown> | null): string | null {
  if (probation === null) return null

  /*
    Enum ikki xil kelishi mumkin: hozir SON (`1`), `JsonStringEnumConverter`
    qo'shilsa NOM («InProgress»). Ikkalasi ham tushuniladi — bu faylning
    boshqa joylaridagi `enumValue` bilan bir xil qoida.
  */
  const status = probation['status']
  const open =
    status === 1 || (typeof status === 'string' && status.toLowerCase() === 'inprogress')

  return open ? text(probation['id']) : null
}

/**
 * Sinov KETISH bilan yakunlanganmi: «o'tolmadi» (3) yoki «o'zi ketdi» (4).
 *
 * Enum ikki xil kelishi mumkin — `openProbationId` dagi bilan bir xil
 * qoida va bir xil sabab.
 */
function probationEndedByLeaving(probation: Record<string, unknown> | null): boolean {
  if (probation === null) return false

  const status = probation['status']

  if (typeof status === 'number') return status === 3 || status === 4
  if (typeof status !== 'string') return false

  const name = status.toLowerCase()
  return name === 'failed' || name === 'leftearly'
}

function toEmployment(raw: unknown): EmploymentSummary {
  const box = (raw ?? {}) as Record<string, unknown>

  // Sinov muddati kichik obyekt bo'lib keladi — kartochkaga tugash sanasi
  // va (ochiq bo'lsa) identifikatori chiqadi, qolgani sinov muddatlari
  // sahifasida ko'rinadi.
  const probation = (box['probation'] ?? null) as Record<string, unknown> | null

  return {
    id: text(box['id']) ?? '',
    legalEntity: refPair(box, 'legalEntityId', 'legalEntity'),
    department: refPair(box, 'departmentId', 'department'),
    position: refPair(box, 'positionId', 'position'),
    hireDate: text(box['hireDate']) ?? '',
    status: enumValue(box['status'], EMPLOYEE_STATUSES, 'active'),
    rate: number(box['rate'], 1),
    isPrimary: bool(box['isPrimary']),
    salary: money(box['salary']),
    hireOrderNo: text(box['hireOrderNo']),
    hireOrderDate: text(box['hireOrderDate']),
    terminationDate: text(box['terminationDate']),
    terminationReasonName: text(box['terminationReasonName']),
    probationEndDate: probation === null ? null : text(probation['endDate']),
    probationId: openProbationId(probation),
    probationEndedByLeaving: probationEndedByLeaving(probation),
    needsReview: bool(box['needsReview']),
  }
}

function toDetails(raw: unknown): EmployeeDetails {
  const envelope = (raw ?? {}) as Record<string, unknown>

  /*
   * Karta so'rovi shaxsni KONVERT ichida qaytaradi:
   * `{ employee: {…}, employments: […], flags: […] }` (`EmployeeCardDto`).
   * Tahrirlash so'rovi esa shaxsning o'zini tekis qaytaradi (`EmployeeDto`).
   *
   * Ikkalasi ham tushuniladi: konvert bo'lsa ochiladi, bo'lmasa qiymat
   * o'z holicha o'qiladi. Bitta shaklga qattiq bog'lanish sahifani
   * jimgina bo'shatib qo'yardi — maydon topilmasa xato ham chiqmaydi,
   * shunchaki hamma joyda «—» turadi.
   */
  const box = (envelope['employee'] ?? envelope) as Record<string, unknown>

  const directions = namedRefs(box['directions'])

  const lastName = text(box['lastName']) ?? ''
  const firstName = text(box['firstName']) ?? ''
  const middleName = text(box['middleName'])

  return {
    id: text(box['id']) ?? '',
    lastName,
    firstName,
    middleName,
    // Server tayyor satr bermasa — o'zimiz yig'amiz. `FullName.Full` bilan
    // bir xil tartib: familiya, ism, otasining ismi.
    fullName:
      text(box['fullName']) ??
      [lastName, firstName, middleName].filter((part) => part !== null).join(' ').trim(),
    shortName: text(box['shortName']),
    photoKey: text(box['photoKey']),
    birthDate: text(box['birthDate']),
    gender: box['gender'] == null ? null : enumValue(box['gender'], GENDERS, 'male'),
    pinfl: text(box['pinfl']),
    passport: text(box['passport']),
    phone: text(box['phone']),
    secondPhone: text(box['secondPhone']),
    address: text(box['address']),
    speciality: text(box['speciality']),
    telegramUrl: text(box['telegramUrl']) ?? text(box['telegram']),
    criminalRecordNote: text(box['criminalRecordNote']),
    hasCriminalRecord: nullableBool(box['hasCriminalRecord']),
    mehnatUzUrl: text(box['mehnatUzUrl']),
    mehnatUzScreenshotKey: text(box['mehnatUzScreenshotKey']),
    directions,
    // Server nom bermasa ham `id` lar keladi; nom bergan holatda esa
    // ularni qaytadan so'ramaymiz — `id` o'sha ro'yxatdan olinadi
    directionIds: Array.isArray(box['directionIds'])
      ? box['directionIds'].flatMap((id) => {
          const value = text(id)
          return value === null ? [] : [value]
        })
      : directions.map((direction) => direction.id),
    needsReview: bool(box['needsReview']),
    reviewNote: text(box['reviewNote']),
    // Munosabatlar konvertda, shaxs ichida emas — shu sababli `envelope`
    employments: Array.isArray(envelope['employments'])
      ? envelope['employments'].map(toEmployment)
      : [],
  }
}

function toTimelineEvent(raw: unknown): TimelineEvent {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employmentId: text(box['employmentId']) ?? '',
    type: enumValue(box['type'], EMPLOYMENT_EVENT_TYPES, 'transfer'),
    effectiveDate: text(box['effectiveDate']) ?? '',
    orderNo: text(box['orderNo']),
    orderDate: text(box['orderDate']),
    fromDepartmentName: text(box['fromDepartmentName']),
    toDepartmentName: text(box['toDepartmentName']),
    fromPositionName: text(box['fromPositionName']),
    toPositionName: text(box['toPositionName']),
    fromSalary: money(box['fromSalary']),
    toSalary: money(box['toSalary']),
    note: text(box['note']),
    isImported: bool(box['isImported']),
    recordedAt: text(box['recordedAt']),
    recordedByName: text(box['recordedByName']),
    legalEntityName: text(box['legalEntityName']),
  }
}

function toFlag(raw: unknown): EmployeeFlag {
  const box = (raw ?? {}) as Record<string, unknown>
  const endDate = text(box['endDate'])

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    type: enumValue(box['type'], EMPLOYEE_FLAG_TYPES, 'monitoring'),
    startDate: text(box['startDate']) ?? '',
    endDate,
    reason: text(box['reason']),
    // Server hisoblab bermasa — yopilmagan belgi kuchda deb qaraladi
    isActive: typeof box['isActive'] === 'boolean' ? box['isActive'] : endDate === null,
  }
}

function toStageEntry(raw: unknown): EmployeeStageEntry {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    candidateId: text(box['candidateId']) ?? '',
    stageId: text(box['stageId']) ?? '',
    stage: text(box['stage']),
    date: text(box['date']) ?? '',
    // Noma'lum qiymat «kutilmoqda» ga tushadi: u eng zararsiz taxmin —
    // «o'tdi» yoki «o'tmadi» deb ko'rsatish yolg'on xulosa bo'lardi.
    outcome: enumValue(box['outcome'], STAGE_OUTCOMES, 'pending'),
    conclusion: text(box['conclusion']),
    recordedBy: text(box['recordedBy']),
    recordedByName: text(box['recordedByName']),
    recordedAt: text(box['recordedAt']) ?? '',
  }
}

/* ═══════════════════════════════════════════════════════════════ SO'ROVLAR */

/**
 * Bo'sh filtrlar so'rovga tushmaydi.
 *
 * NEGA: `?departmentId=` (bo'sh qiymat) bilan `?departmentId` yo'qligi
 * server uchun bir xil emas — birinchisi «bo'sh bo'limdagilarni ber»
 * deb tushunilishi mumkin.
 */
function filterParams(filters: EmployeeFilterParams): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {}

  if (filters.search !== undefined && filters.search.trim() !== '') {
    params['search'] = filters.search.trim()
  }
  if (filters.departmentId) params['departmentId'] = filters.departmentId
  if (filters.positionId) params['positionId'] = filters.positionId
  if (filters.directionId) params['directionId'] = filters.directionId
  if (filters.legalEntityId) params['legalEntityId'] = filters.legalEntityId
  if (filters.sort) params['sort'] = filters.sort

  if (filters.status !== undefined) {
    // ASP.NET Core enum'ni nom bo'yicha bog'laydi (registrga sezgir emas),
    // shu sababli C# dagi ko'rinishda yuboriladi: «Probation»
    params['status'] = filters.status.charAt(0).toUpperCase() + filters.status.slice(1)
  }

  if (filters.needsReview === true) params['needsReview'] = true

  return params
}

/** Filtrlar + sahifa. Sahifalash faqat EKRANGA tegishli — eksportda yo'q. */
function listParams(filters: EmployeeListFilters): Record<string, string | number | boolean> {
  return { ...filterParams(filters), page: filters.page, pageSize: filters.pageSize }
}

export async function fetchEmployees(
  filters: EmployeeListFilters,
  signal?: AbortSignal,
): Promise<PagedResult<EmployeeListItem>> {
  const { data } = await api.get<unknown>('/api/employees', {
    params: listParams(filters),
    signal,
  })

  return readPage(data, toListItem, filters.page, filters.pageSize)
}

/* ─────────────────────────────────────────────────────── Excel eksport */

/**
 * Xodimlar ro'yxatini `.xlsx` qilib yuklab olish.
 *
 * `GET /api/reports/employees/export` — parametrlari `GET /api/employees`
 * bilan AYNAN bir xil (sahifalashdan tashqari), shu sababli bu yerda
 * yangi so'rov qurilmaydi: `filterParams` ro'yxat bilan BO'LISHILADI.
 * «Ekranda nima ko'rsang, faylda ham shu» talabini kod darajasida
 * kafolatlaydigan yagona usul — ikkinchi nusxa yozmaslik.
 *
 * Nom serverdan olinadi (`Content-Disposition`), lekin unga TAYANIB
 * qolinmaydi — sabab `fileNameFromDisposition` izohida.
 */
export async function exportEmployees(
  filters: EmployeeFilterParams,
  signal?: AbortSignal,
): Promise<EmployeeExportFile> {
  try {
    const response = await api.get<Blob>('/api/reports/employees/export', {
      params: filterParams(filters),
      responseType: 'blob',
      signal,
    })

    const disposition = response.headers['content-disposition']

    return {
      blob: response.data,
      fileName:
        fileNameFromDisposition(typeof disposition === 'string' ? disposition : null) ??
        exportFileName(),
    }
  } catch (error) {
    await revealProblemDetails(error)
    throw error
  }
}

/**
 * Sarlavha kelmaganda ishlatiladigan zaxira nom.
 *
 * Serverning shakli takrorlanadi (`Bonvi_xodimlar_2026-08-26.xlsx`), lekin
 * SANA klientniki: HR fayllarni yuklab olish papkasida sana bo'yicha
 * ajratadi, «xodimlar.xlsx (3)» esa hech narsa aytmaydi. Sana `sv-SE`
 * lokali bilan olinadi — u ISO shaklini (`YYYY-MM-DD`) beradigan yagona
 * standart lokal, ya'ni qo'lda nol qo'shib yurish shart emas.
 */
function exportFileName(): string {
  return `Bonvi_xodimlar_${new Date().toLocaleDateString('sv-SE')}.xlsx`
}

/**
 * Blob so'rovidagi XATO tanasini o'qiladigan holga keltiradi.
 *
 * NEGA kerak: `responseType: 'blob'` axios uchun javobning HAMMASIGA
 * tegishli — muvaffaqiyatga ham, xatoga ham. Server 400 bilan
 * `{"title":"eksport.juda_kop"}` qaytarganda ham tana `Blob` bo'lib
 * o'raladi va `problemCode` undan `title` ni topolmaydi. Natijada HR
 * «filtrni toraytiring» o'rniga umumiy «Nimadir noto'g'ri ketdi» ni
 * ko'rardi — ya'ni nima qilish kerakligini bilmasdi.
 *
 * Blob matnga o'girilib, javob tanasining O'RNIGA qo'yiladi: shundan
 * keyin butun mavjud xato zanjiri (`employeeErrorCode` → `useErrorMessage`)
 * hech qanday o'zgarishsiz ishlaydi.
 */
async function revealProblemDetails(error: unknown): Promise<void> {
  if (!axios.isAxiosError(error)) return

  const response = error.response
  if (response === undefined || !(response.data instanceof Blob)) return

  try {
    response.data = JSON.parse(await response.data.text())
  } catch {
    // Tana JSON emas (proksining HTML sahifasi yoki bo'sh javob) — kod
    // baribir topilmaydi, xato o'z holicha yuqoriga ketadi va umumiy
    // xabarga aylanadi. Bu yerda yiqilish eng yomon variant bo'lardi.
  }
}

export async function fetchEmployee(
  id: string,
  signal?: AbortSignal,
): Promise<EmployeeDetails> {
  const { data } = await api.get<unknown>(`/api/employees/${id}`, { signal })
  return toDetails(data)
}

/* ───────────────────────────────────────────────────────────────── rasm */

/**
 * Xodim rasmi — BAYTLAR, havola emas.
 *
 * NEGA `<img src="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q. Brauzer esa `<img>` so'roviga
 * hech qanday sarlavha qo'sha olmaydi va server 401 qaytaradi. Shu sababli
 * rasm oddiy so'rov bilan olinadi, ko'rsatish uchun esa `URL.createObjectURL`
 * ishlatiladi (`useObjectUrl`).
 *
 * `404` — XATO EMAS. U «bu xodimning rasmi yo'q» degani va `null` ga
 * aylanadi: chaqiruvchi jimgina bosh harflarga qaytadi.
 */
export async function fetchEmployeePhoto(
  id: string,
  signal?: AbortSignal,
): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>(`/api/employees/${id}/photo`, {
      responseType: 'blob',
      signal,
    })
    return data
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) return null
    throw error
  }
}

/**
 * Rasmni yuklash. Javobdagi yangi `photoKey` qaytariladi.
 *
 * `Content-Type` bu yerda QO'YILMAYDI va qo'yilmasligi kerak: axios uni
 * `FormData` ni ko'rib o'zi yozadi va **chegara (boundary)** satrini ham
 * qo'shadi. Sarlavhani qo'lda yozish chegarani yo'qotadi va server tanani
 * ajrata olmay qoladi — sabab `http-client.ts` da batafsil.
 */
export async function uploadEmployeePhoto(id: string, file: File): Promise<string | null> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<unknown>(`/api/employees/${id}/photo`, form)

  const box = (data ?? {}) as Record<string, unknown>
  return text(box['photoKey'])
}

/** Rasmni o'chirish. Server idempotent: rasm bo'lmasa ham `204` qaytaradi. */
export async function deleteEmployeePhoto(id: string): Promise<void> {
  await api.delete(`/api/employees/${id}/photo`)
}

/* ─────────────────────────────────────────────── mehnat.uz skrinshoti */

/**
 * mehnat.uz skrinshoti — BAYTLAR.
 *
 * Xodim rasmidagi bilan aynan bir xil sabab: `Authorization` sarlavhasi
 * `<img src>` so'roviga qo'shilmaydi. `404` — XATO EMAS, «skrinshot yo'q»
 * degani.
 *
 * ⚠️ Rasmdan farqli, tur oldindan MA'LUM EMAS: server faylni qayta
 * kodlamaydi va u PDF ham bo'lishi mumkin (sababi backend'da,
 * `Employee.MehnatUzScreenshotKey` izohida). Shu sababli `blob` ning
 * `type` iga qarab ko'rsatish usuli tanlanadi.
 */
export async function fetchMehnatUzScreenshot(
  id: string,
  signal?: AbortSignal,
): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>(`/api/employees/${id}/mehnat-uz-screenshot`, {
      responseType: 'blob',
      signal,
    })
    return data
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) return null
    throw error
  }
}

/**
 * Skrinshotni yuklash. Javobdagi yangi kalit qaytariladi.
 *
 * `Content-Type` QO'YILMAYDI — axios `FormData` ni ko'rib chegara
 * (boundary) satri bilan birga o'zi yozadi (sababi `http-client.ts` da).
 */
export async function uploadMehnatUzScreenshot(id: string, file: File): Promise<string | null> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<unknown>(`/api/employees/${id}/mehnat-uz-screenshot`, form)

  const box = (data ?? {}) as Record<string, unknown>
  return text(box['screenshotKey'])
}

/** Skrinshotni o'chirish. Server idempotent: yo'q bo'lsa ham `204`. */
export async function deleteMehnatUzScreenshot(id: string): Promise<void> {
  await api.delete(`/api/employees/${id}/mehnat-uz-screenshot`)
}

export async function fetchTimeline(
  id: string,
  signal?: AbortSignal,
): Promise<TimelineEvent[]> {
  const { data } = await api.get<unknown>(`/api/employees/${id}/timeline`, { signal })
  return Array.isArray(data) ? data.map(toTimelineEvent) : []
}

export async function fetchFlags(
  id: string,
  signal?: AbortSignal,
): Promise<EmployeeFlag[]> {
  const { data } = await api.get<unknown>(`/api/employees/${id}/flags`, { signal })
  return Array.isArray(data) ? data.map(toFlag) : []
}

/**
 * Belgi qo'yish.
 *
 * `confirm` so'rov QATORIDA ketadi, tanada emas — bo'shatishdagi bilan
 * bir xil naqsh (`terminate`). Sabab: bu forma qiymati emas, AMALNI
 * takrorlash sharti; tanaga qo'shilsa u boshqa maydonlar bilan bir
 * qatorda turib, «bu ham belgi haqidagi ma'lumot» bo'lib ko'rinardi.
 */
export async function createFlag(
  employeeId: string,
  body: CreateFlagRequest,
): Promise<EmployeeFlag> {
  const { confirm, ...rest } = body

  const { data } = await api.post<unknown>(
    `/api/employees/${employeeId}/flags`,
    { ...rest, type: enumName(rest.type) },
    { params: confirm ? { confirm: true } : undefined },
  )
  return toFlag(data)
}

/**
 * Xodimning nomzodlik bosqichlari — «Kengash xulosasi» bo'limi uchun.
 *
 * Ruxsat `recruiting.view`: chaqiruvchi uni OLDINDAN tekshiradi va
 * ruxsat yo'q bo'lsa so'rov umuman yuborilmaydi (bo'lim ham chizilmaydi).
 */
export async function fetchEmployeeStages(
  id: string,
  signal?: AbortSignal,
): Promise<EmployeeStageEntry[]> {
  const { data } = await api.get<unknown>(`/api/employees/${id}/stages`, { signal })
  return Array.isArray(data) ? data.map(toStageEntry) : []
}

/**
 * Xodim kartasini tahrirlash.
 *
 * <b>Javob ATAYLAB o'qilmaydi.</b> Server `EmployeeDto` qaytaradi — u
 * kartadan TOR: unda mehnat munosabatlari ham, yo'nalish nomlari ham
 * yo'q. Uni keshka yozib qo'ysak, saqlagandan keyin xodimning ish joyi
 * ekrandan yo'qolardi. Shu sababli keshdagi karta shunchaki bekor
 * qilinadi va toza holida qayta so'raladi.
 */
export async function updateEmployee(
  id: string,
  body: UpdateEmployeeRequest,
): Promise<void> {
  await api.put(`/api/employees/${id}`, {
    ...body,
    gender: body.gender === null ? null : enumName(body.gender),
  })
}

export async function closeFlag(
  employeeId: string,
  flagId: string,
  closedAt: string,
  note: string | null,
): Promise<void> {
  await api.post(`/api/employees/${employeeId}/flags/${flagId}/close`, {
    closedAt,
    note,
  })
}

/** Ishga qabul: shaxs + mehnat munosabati + buyruq — bitta so'rovda. */
/**
 * Forma qiymatlarini SERVER kutgan so'rov tanasiga o'giradi.
 *
 * <b>Nima uchun umuman kerak.</b> `HireEmployeeCommand` shaxsni ichma-ich
 * `person` obyektida kutadi va usiz «xodim.malumot_yoq» xatosini qaytaradi
 * — ya'ni yassi tana bilan yangi xodim HECH QACHON yaratilmasdi. Forma esa
 * yassi bo'lib qolgani ma'qul: HR bitta blanka to'ldiradi va `hire-dialog`
 * uning maydonlarini bir tekisda yig'adi. O'girish shu yerda — mijozdagi
 * boshqa shakl mosliklari («EmploymentDto» → `toEmployment`) bilan bir
 * joyda.
 *
 * <b>Nima uchun ALOHIDA va eksport qilingan funksiya.</b> Bu nomuvofiqlik
 * jimgina yashagan, chunki uni tekshiradigan joy yo'q edi: interfeys tipi
 * so'rov tanasini emas, faqat forma shaklini himoya qiladi. Endi tana
 * hosil qilish sinaladigan sof funksiya. Backend tomonida shu shaklni
 * qadab turadigan integratsiya testi bor:
 * `api/tests/Bonvi.Api.IntegrationTests/HireApiTests.cs`.
 *
 * <b>Ikki yo'l, bittasi tanlanadi.</b> `employeeId` berilgan bo'lsa —
 * mavjud shaxs (o'rindoshlik yoki qayta ishga olish) va `person` UMUMAN
 * yuborilmaydi: server u holda kartani qidirmaydi ham. Aks holda `person`
 * to'ldiriladi va server avval JSHSHIR bo'yicha qidirib, dublikat
 * yaratmaydi.
 */
export function toHireBody(request: HireRequest): Record<string, unknown> {
  const workplace = {
    legalEntityId: request.legalEntityId,
    departmentId: request.departmentId,
    positionId: request.positionId,
    hireDate: request.hireDate,
    rate: request.rate,
    isPrimary: request.isPrimary,
    salaryAmount: request.salaryAmount,
    salaryCurrency: request.salaryCurrency,
    orderNo: request.orderNo,
    orderDate: request.orderDate,
  }

  if (request.employeeId !== null) {
    return { employeeId: request.employeeId, ...workplace }
  }

  return {
    ...workplace,
    person: {
      lastName: request.lastName,
      firstName: request.firstName,
      middleName: request.middleName,
      birthDate: request.birthDate,
      pinfl: request.pinfl,
      passport: request.passport,
      phone: request.phone,
      secondPhone: request.secondPhone,
      address: request.address,
      speciality: request.speciality,
      // ⚠️ Nom farqi: formada `telegram`, serverda `telegramUrl`
      // (`SaveEmployeeCommand.TelegramUrl`). Yassi tanada bu maydon
      // jimgina yo'qolardi — xato ham chiqmasdi.
      telegramUrl: request.telegram,
      directionIds: request.directionIds,
    },
  }
}

/**
 * Ishga qabul.
 *
 * Javob KONVERT: `HireResultDto` = `{ employment, duties, warnings }`.
 * Ilgari bu yerda konvertning o'zi `toEmployment` ga berilardi va natijada
 * hamma maydoni bo'sh munosabat qaytardi — xato chiqmasdi, chunki
 * xaritalash yetishmagan maydonni jimgina `null` qiladi.
 */
export async function hire(body: HireRequest): Promise<EmploymentSummary> {
  const { data } = await api.post<unknown>('/api/employments/hire', toHireBody(body))

  const envelope = (data ?? {}) as Record<string, unknown>
  return toEmployment(envelope['employment'] ?? envelope)
}

/**
 * Ishdan bo'shatish.
 *
 * `confirm` — HR so'ragan qoida: xodim qora ro'yxatda yoki nazoratda bo'lsa
 * server 409 va ogohlantirish qaytaradi. Foydalanuvchi ko'rib, ongli
 * ravishda tasdiqlagandan keyingina `confirm=true` bilan qayta yuboriladi.
 */
export async function terminate(
  employmentId: string,
  body: TerminateRequest,
  confirm = false,
): Promise<void> {
  await api.post(`/api/employments/${employmentId}/terminate`, body, {
    params: confirm ? { confirm: true } : undefined,
  })
}

/**
 * Lavozim yoki bo'lim o'zgarishi.
 *
 * Server hodisa turini O'ZI aniqlaydi: lavozim o'zgargan bo'lsa
 * «Promotion», faqat bo'lim bo'lsa «Transfer». Mijoz buni takrorlamaydi —
 * qoida ikki joyda yozilsa, biri o'zgarganda ikkinchisi jimgina eskiradi.
 */
export async function transfer(employmentId: string, body: TransferRequest): Promise<void> {
  await api.post(`/api/employments/${employmentId}/transfer`, body)
}

export async function addAdditionalDuty(
  employmentId: string,
  body: AdditionalDutyRequest,
): Promise<void> {
  await api.post(`/api/employments/${employmentId}/additional-duty`, body)
}

export async function changeSalary(
  employmentId: string,
  body: ChangeSalaryRequest,
): Promise<void> {
  await api.post(`/api/employments/${employmentId}/salary`, body)
}
