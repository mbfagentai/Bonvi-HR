/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employments-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Maydon nomlari TAXMIN emas — ular haqiqiy manbadan olingan va jonli
 * endpoint'da tekshirilgan (`GET /api/recruiting/stages` javobi bilan
 * solishtirildi):
 *   api/src/Bonvi.Application/Recruiting/Dtos/RecruitingDtos.cs
 *   api/src/Bonvi.Api/Endpoints/{Vacancy,Candidate,PipelineStage}Endpoints.cs
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `recruiting-queries.ts` generatsiya qilingan hook'larga o'tkaziladi.
 *
 * NEGA butun rekruting BITTA faylda (vakansiya, nomzod, bosqich).
 * Uchala endpoint bitta shartnomaning bo'laklari: nomzod kartasi
 * javobida bosqich yozuvlari ham bor (`CandidateDto.stages`), ya'ni
 * tiplar bir-biriga bog'langan. Fayllarga bo'linsa,
 * ular orasida o'zaro import paydo bo'lardi va `unknown` dan o'qish
 * yordamchilari (`text`, `money`, `readPage`) ham nusxalanardi —
 * `employees-api.ts` da aynan shunday bir necha nusxa hosil bo'lgan edi.
 */
import axios from 'axios'

import {
  LANGUAGE_LEVELS,
  type LanguageLevel,
} from '@/features/employees/api/employee-competency-api'
import type { PagedResult } from '@/features/employees/api/employees-api'
import { api } from '@/shared/api/http-client'

/* ═══════════════════════════════════════════════════════════════ sanoqlar */

/*
 * Sanoqlar JSON'da NOM bo'lib yuradi (`JsonStringEnumConverter`,
 * `Program.cs`) — ya'ni javobda ham, so'rov satrida ham «Open»,
 * «InProgress», «Manual».
 *
 * NEGA klientda camelCase'ga o'girilmaydi (buyruqlar moduli shunday
 * qilgan edi): u yerda javob SON qaytarardi va nomga o'girish baribir
 * kerak edi. Bu yerda esa server nomni o'zi beradi — o'rtada yana bir
 * xarita qo'yish faqat ikkita nomlash tizimi hosil qiladi. Manzil
 * satridagi `?status=InProgress` ham backend hujjatidagi bilan aynan
 * bir xil ko'rinadi, ya'ni havolani qo'lda yozish ham mumkin.
 */

/*
 * Vakansiya holati — IKKITA. «Paused» OLIB TASHLANDI: pauza va yopish
 * tizim uchun ayni natijani berardi (ikkalasida ham yangi nomzod
 * kiritilmasdi), farq faqat HR ning niyatida edi — batafsil sabab
 * backend'dagi `VacancyStatus` domen izohida.
 */
export const VACANCY_STATUSES = ['Open', 'Closed'] as const
export type VacancyStatus = (typeof VACANCY_STATUSES)[number]

/*
 * Nomzodning yakunlari — BESHTA.
 *
 * ⚠️ `Reserve` ro'yxatda BO'LISHI SHART, garchi u faqat «yana bitta
 * holat» bo'lib ko'rinsa ham. Pastdagi `oneOf(...)` tanilmagan qiymatni
 * JIMGINA zaxiraga (`InProgress`) tushiradi — ya'ni ro'yxatdan tushib
 * qolgan holat xato bermaydi, u YOLG'ON ko'rsatadi: rezervdagi nomzod
 * ekranda «Jarayonda» bo'lib turadi va HR uni voronkada faol deb
 * o'qiydi. Aynan shu bo'lgan edi (`docs/PENDING_WIRING.md`).
 *
 * Tartib serverdagi sanoq bilan bir xil (`CandidateStatus` domen
 * izohi): `Reserve` OXIRIDA — u eng keyin qo'shilgan.
 */
export const CANDIDATE_STATUSES = [
  'InProgress',
  'Rejected',
  'Hired',
  'Withdrawn',
  'Reserve',
] as const
export type CandidateStatus = (typeof CANDIDATE_STATUSES)[number]

export const CANDIDATE_SOURCES = ['Manual', 'TelegramBot', 'Excel'] as const
export type CandidateSource = (typeof CANDIDATE_SOURCES)[number]

export const STAGE_OUTCOMES = ['Pending', 'Passed', 'Failed'] as const
export type StageOutcome = (typeof STAGE_OUTCOMES)[number]

/* ───────────────────────────────────────────────────── anketa sanoqlari */

/*
 * Anketa javoblari — YOPIQ to'plamlar: nomzod ularni botda tugmadan
 * tanlaydi. Qiymatlar backend'dagi nomlar bilan AYNAN bir xil
 * (`CandidateQuestionnaire.cs`) va tarjima kaliti ulardan
 * `lowerFirst` bilan hosil qilinadi — yuqoridagi holat sanoqlari
 * bilan bir xil qoida.
 */

export const EDUCATION_LEVELS = [
  'SecondarySchool',
  'VocationalSchool',
  'HigherComplete',
  'HigherIncomplete',
] as const
export type EducationLevel = (typeof EDUCATION_LEVELS)[number]

export const MARITAL_STATUSES = ['Married', 'Unmarried', 'Divorced'] as const
export type MaritalStatus = (typeof MARITAL_STATUSES)[number]

/**
 * Kutilayotgan maosh — ORALIQ, aniq son emas.
 *
 * ⚠️ Oraliqlar vaqt o'tishi bilan eskiradi va o'shanda backend YANGI
 * qiymat qo'shadi, eskisini o'chirmaydi (domen izohi). Shu sababli
 * tanilmagan qiymat bu yerda XATO EMAS — u `null` ga aylanadi va qator
 * umuman chizilmaydi (pastdagi `enumOrNull` izohi).
 */
export const SALARY_EXPECTATIONS = [
  'From1To2Million',
  'From2To3Million',
  'From3To4Million',
  'From5To7Million',
] as const
export type SalaryExpectation = (typeof SALARY_EXPECTATIONS)[number]

export const REFERRAL_SOURCES = [
  'Acquaintance',
  'SocialNetwork',
  'Olx',
  'HeadHunter',
  'Telegram',
  'Other',
] as const
export type ReferralSource = (typeof REFERRAL_SOURCES)[number]

/* ═══════════════════════════════════════════════════════════════ tiplar */

/** `PipelineStageDto` — voronka bosqichi. */
export interface PipelineStage {
  id: string
  name: string
  isActive: boolean
  note: string | null
  /**
   * Voronkadagi o'rin: kichik qiymat — oldinroq. Ro'yxat serverdan
   * ALLAQACHON shu tartibda keladi, klientda qayta saralanmaydi
   * (`catalog-api.ts` dagi bilan bir xil qoida: ikki xil tartib
   * bo'lmasin).
   */
  sortOrder: number
  /**
   * ⭐ Shu bosqichga o'tkazilganda nomzoddan SUHBAT VAQTI so'raladimi va
   * unga Telegram taklifi yuboriladimi.
   *
   * Bayroq ma'lumotnomada turadi, kodda emas: «Кенгашга чақирилди» —
   * HR o'zi qo'yadigan nom va uni qayta nomlash tugmaning xatti-harakatini
   * buzmasligi kerak (sabab `PipelineStage.InvitesToInterview` domen
   * izohida).
   */
  invitesToInterview: boolean
}

/**
 * `VacancyDto` — ro'yxat va karta uchun umumiy ko'rinish.
 *
 * ⚠️ Lavozim, bo'lim va firma maydonlari YO'Q va bu ataylab: vakansiya —
 * E'LON, kompaniya tuzilmasidan unda hech narsa saqlanmaydi. Uchalasi
 * nomzod ISHGA OLINAYOTGANDA tanlanadi (`candidate-hire-dialog.tsx`) va
 * o'sha yerdan xodim kartasiga tushadi. Batafsil sabab backend'dagi
 * `Vacancy` domen izohida.
 */
export interface Vacancy {
  id: string
  /** «vacancy-titles» ma'lumotnomasidagi yozuv — LAVOZIM emas. */
  vacancyTitleId: string
  /** E'lon nomining matni. */
  title: string | null
  openings: number
  status: VacancyStatus
  openedAt: string
  closedAt: string | null
  responsibleEmployeeId: string | null
  responsibleName: string | null
  note: string | null
  candidateCount: number
  /** `openings` bilan birga «2/3 to'ldirildi» ko'rinishini beradi. */
  hiredCount: number
}

/** `CandidateStageEntryDto` — nomzodning bitta bosqichdagi yozuvi. */
export interface CandidateStageEntry {
  id: string
  stageId: string
  /** Faolsizlantirilgan bosqich uchun ham qaytadi — tarix «noma'lum» bo'lib qolmasin. */
  stage: string | null
  date: string
  outcome: StageOutcome
  conclusion: string | null
  recordedBy: string | null
  recordedAt: string
}

/** `CandidateListItemDto` — nomzodlar ro'yxatining bitta qatori. */
export interface CandidateListItem {
  id: string
  fullName: string
  shortName: string
  phone: string | null
  birthDate: string | null
  speciality: string | null
  vacancyId: string
  vacancy: string | null
  currentStageId: string | null
  currentStage: string | null
  status: CandidateStatus
  source: CandidateSource
  hasResume: boolean
  createdAt: string
  /** Ishga olingan bo'lsa — yaratilgan XODIM kartasi. */
  hiredEmployeeId: string | null
}

/**
 * `CandidateQuestionnaireDto` — nomzod botda to'ldirgan anketa.
 *
 * <b>Alohida so'rov YO'Q</b>: anketa nomzod kartasi javobining ichida
 * keladi (server qarori — HR uni har doim nomzod bilan birga ko'radi).
 *
 * ⚠️ <b>Sanoq maydonlari `null` BO'LISHI MUMKIN</b>, garchi serverda
 * ular majburiy bo'lsa ham. Sabab `enumOrNull` da: backend yangi qiymat
 * qo'shishi mumkin va eski klient uni tanimaydi. Bunday paytda qator
 * NOTO'G'RI javob bilan chizilgandan ko'ra umuman chizilmagani ma'qul
 * (`candidate-resume-card.tsx` dagi bilan bir xil mulohaza).
 *
 * `hasDriverLicense` esa `false` bo'lsa ham KO'RSATILADI: «yo'q» —
 * bo'sh maydon emas, javobning o'zi.
 */
export interface CandidateQuestionnaire {
  education: EducationLevel | null
  /** Erkin matn — botda o'tkazib yuborilishi mumkin. */
  workExperience: string | null
  maritalStatus: MaritalStatus | null
  /**
   * Rus tili darajasi — xodim kartasidagi til darajasi bilan AYNAN bir
   * xil sanoq (server shuni qaytaradi). Shu sababli tipi ham,
   * tarjimasi ham qayta yozilmaydi: `competency.languageLevel.*`
   * kalitlari qayta ishlatiladi.
   */
  russianLevel: LanguageLevel | null
  otherLanguages: string | null
  officeSkills: string | null
  /** SON EMAS, matn: «kelishilgan», «3 mln» kabi javoblar uchun. */
  lastSalary: string | null
  extraNote: string | null
  hasDriverLicense: boolean
  /** Faqat `hasDriverLicense = true` da: B, BC, BCE, BCED. */
  driverLicenseCategories: string | null
  expectedSalary: SalaryExpectation | null
  referralSource: ReferralSource | null
  /** Ommaviy oferta bilan rozilik VAQTI — huquqiy fakt, bayroq emas. */
  offerAcceptedAt: string | null
}

/** `CandidateDto` — nomzod kartasi: bosqichlar tarixi bilan. */
export interface Candidate {
  id: string
  lastName: string
  firstName: string
  middleName: string | null
  fullName: string
  birthDate: string | null
  phone: string | null
  address: string | null
  speciality: string | null
  vacancyId: string
  vacancy: string | null
  currentStageId: string | null
  currentStage: string | null
  status: CandidateStatus
  source: CandidateSource
  note: string | null
  closedAt: string | null
  closingNote: string | null
  /** `null` — rezyume yo'q. */
  resumeFileName: string | null
  resumeContentType: string | null
  resumeSizeBytes: number | null
  /**
   * Surat bor-yo'qligi. Saqlash KALITI qaytarilmaydi (server qarori: u
   * ichki manzil), shu sababli rasm keshi xodimnikidan farq qiladi —
   * izohi `useCandidatePhoto` da.
   */
  hasPhoto: boolean
  hiredEmployeeId: string | null
  hiredEmploymentId: string | null
  /**
   * Chaqirilgan suhbat vaqti — ISO satr, UTC da. `null` — chaqirilmagan.
   * Ekranda Toshkent vaqtida chiziladi.
   */
  interviewAt: string | null
  /**
   * Taklif Telegram'ga qachon ketgani. Vaqt qo'yilgan, lekin bu bo'sh —
   * xabar hali navbatda (yoki nomzodning Telegram suhbati yo'q).
   */
  interviewNoticeSentAt: string | null
  /** Yozilgan tartibda. */
  stages: CandidateStageEntry[]
  /**
   * Botda to'ldirilgan anketa. `null` BO'LISHI ODATIY hol: HR qo'lda
   * kiritgan nomzodda ham, bot ishga tushgunicha yozilgan nomzodlarda
   * ham u yo'q.
   */
  questionnaire: CandidateQuestionnaire | null
}

/**
 * `HrWarning` — ishga qabulning ogohlantirishi (masalan stavka chegarasi).
 *
 * Bu XATO EMAS: amal bajarildi, lekin HR e'tibor berishi kerak bo'lgan
 * narsa bor. `code` — tarjima kaliti (`useErrorMessage`), `message` —
 * serverning o'zbekcha matni (tarjimasi topilmasa zaxira sifatida).
 */
export interface HrWarning {
  code: string
  message: string
}

/**
 * Ishga qabulning natijasi — `HireResultDto` ning ekranga kerak bo'lgan
 * qismi.
 *
 * `DirectHireResult` ichida turadi va ekran shu javobdan darhol yangi
 * xodim kartasiga havola qo'yadi — qo'shimcha so'rovsiz.
 */
export interface HireOutcome {
  /** Yaratilgan mehnat munosabati — bizga faqat `employeeId` kerak. */
  employeeId: string | null
  employmentId: string | null
  /** ⚠️ E'tiborsiz qolmasligi kerak: stavka chegarasi va h.k. */
  warnings: HrWarning[]
}

/** `DirectHireResultDto` — nomzod ishga olinganining natijasi. */
export interface DirectHireResult {
  candidate: Candidate
  hire: HireOutcome
}

/* ═══════════════════════════════════════════════════ so'rov tanalari */

export interface VacancyInput {
  vacancyTitleId: string
  openings: number
  openedAt: string | null
  responsibleEmployeeId: string | null
  note: string | null
}

export interface CandidateInput {
  lastName: string
  firstName: string
  middleName: string | null
  birthDate: string | null
  phone: string | null
  address: string | null
  speciality: string | null
  note: string | null
}

/** Yaratishda vakansiya, boshlang'ich bosqich va manba ham beriladi. */
export interface RegisterCandidateInput extends CandidateInput {
  vacancyId: string
  /** Bo'sh — voronkaning birinchi faol bosqichi (server tanlaydi). */
  stageId: string | null
}

/**
 * ⭐ Nomzodni ishga olishda so'raladigan qism (`HireCandidateRequest`).
 *
 * Firma, bo'lim va lavozim AYNAN shu yerda: vakansiyada ular umuman
 * saqlanmaydi (`Vacancy` tipidagi izohga qarang), ya'ni «bu odam qaysi
 * firmaga, qaysi bo'limga va qaysi shtat birligiga olinadi» degan qaror
 * shu formada qabul qilinadi.
 *
 * Shaxsiy ma'lumot (F.I.Sh., tug'ilgan sana, telefon, manzil,
 * mutaxassislik) NOMZODDAN ko'chadi va bu yerda umuman yo'q.
 */
export interface HireCandidateInput {
  /** MAJBURIY — «nomzod.yuridik_shaxs_yoq» (400). */
  legalEntityId: string
  /** MAJBURIY — «nomzod.lavozim_yoq» (400). */
  positionId: string
  /** Ixtiyoriy; ko'rsatilsa mavjud bo'lishi shart («nomzod.bolim_topilmadi»). */
  departmentId: string | null
  /** Bo'sh — server bugungi sanani oladi. */
  hireDate: string | null
  orderNo: string | null
  orderDate: string | null
  /** ⭐ Dublikat xodim qidiruvi AYNAN shunga tayanadi. */
  pinfl: string | null
  passport: string | null
  /** Bo'sh — oklad yozuvi umuman ochilmaydi. */
  salaryAmount: number | null
  rate: number | null
  isPrimary: boolean
  responsibleEmployeeId: string | null
  /** Bo'sh — lavozimdagi qiymat, u ham bo'lmasa 90 kun. */
  probationDays: number | null
}

/* ═══════════════════════════════════════════════════════════════ filtrlar */

export interface VacancyFilters {
  status?: VacancyStatus
  /**
   * E'lon nomi bo'yicha filtr.
   *
   * Lavozim, bo'lim va firma filtrlari OLIB TASHLANDI: server ularni
   * endi umuman qabul qilmaydi, chunki vakansiyada bunday maydonlar
   * yo'q. «Qaysi bo'limga kim olindi» degan savolga xodimlar ro'yxati
   * javob beradi.
   */
  vacancyTitleId?: string
  page: number
  pageSize: number
  /** `openedAt` (sukut, yangidan eskiga) yoki `title`; `-` — kamayish. */
  sort?: string
}

export interface CandidateFilters {
  /** F.I.Sh. yoki TELEFON bo'yicha — server ikkalasini ham qidiradi. */
  search?: string
  vacancyId?: string
  stageId?: string
  status?: CandidateStatus
  source?: CandidateSource
  page: number
  pageSize: number
  /** `createdAt` (sukut), `name`, `stage`; `-` — kamayish. */
  sort?: string
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

/**
 * Sanoqni tekshiradi: kutilmagan qiymat zaxiraga tushadi.
 *
 * NEGA zaxira, xato emas: sanoqqa yangi qiymat backend tomonda
 * qo'shilishi mumkin (masalan `CandidateSource` ga «hh.uz» —
 * domen izohida shunday deyilgan). Eski klient uni ko'rganda butun
 * ro'yxatni yiqitmasligi kerak; noma'lum qiymat shunchaki eng
 * zararsiz ko'rinishga tushadi.
 */
function oneOf<T extends string>(value: unknown, allowed: readonly T[], fallback: T): T {
  return typeof value === 'string' && (allowed as readonly string[]).includes(value)
    ? (value as T)
    : fallback
}

/**
 * `oneOf` ning zaxirasiz varianti — ANKETA sanoqlari uchun.
 *
 * NEGA zaxira qiymat berilmaydi: holat sanoqlarida («Open»,
 * «InProgress») eng zararsiz qiymat bor va noma'lum javob unga tushsa
 * ro'yxat baribir o'qiladi. Anketa esa NOMZODNING JAVOBI: tanilmagan
 * qiymatni «O'rta maktab» yoki «Tanishlar orqali» deb ko'rsatish
 * yolg'on ma'lumot bo'lardi va HR uni haqiqiy javobdan ajrata olmasdi.
 * `null` esa qatorni umuman chizdirmaydi.
 *
 * Server nomni PascalCase yuboradi; ro'yxatdagi qiymat kichik harf
 * bilan bo'lishi ham mumkin (`LANGUAGE_LEVELS`), shu sababli
 * solishtirish REGISTRGA BEFARQ — `employee-competency-api.ts` dagi
 * bilan bir xil kelishuv.
 */
function enumOrNull<T extends string>(value: unknown, allowed: readonly T[]): T | null {
  if (typeof value !== 'string') return null
  const needle = value.toLowerCase()
  return allowed.find((name) => name.toLowerCase() === needle) ?? null
}

/**
 * Sahifalangan javob.
 *
 * NEGA `employments-api.ts` dagi «himoyalangan» varianti KO'CHIRILMADI:
 * u yerda `PagedResult` ning shakli tekshirilmagan edi va o'qish uchta
 * ehtimoliy nomni sinab ko'rardi (`total`/`totalCount`/`totalItems`).
 * Bu yerda shakl ANIQ va jonli endpoint'da tasdiqlangan:
 * `{items, page, pageSize, total, pageCount}`
 * (`EmployeeDtos.cs:22`). Taxminlarni ko'chirish ularni haqiqatga
 * aylantirib qo'yadi — noto'g'ri javob kelsa, u yashirinmasligi kerak.
 */
function readPage<T>(
  data: unknown,
  map: (raw: unknown) => T,
  filters: { page: number; pageSize: number },
): PagedResult<T> {
  const box = (data ?? {}) as Record<string, unknown>
  const items = (Array.isArray(box['items']) ? box['items'] : []).map(map)

  return {
    items,
    total: count(box['total']),
    page: count(box['page']) || filters.page,
    pageSize: count(box['pageSize']) || filters.pageSize,
  }
}

/**
 * Filtrni so'rov satriga o'giradi: bo'sh qiymat UMUMAN yuborilmaydi.
 *
 * NEGA muhim: backend bo'sh satrni ham tekshiradi va `?status=` ni
 * «noto'g'ri holat» deb 400 qaytarishi mumkin edi. Uni sanoq
 * o'qiydigan joyda (`RecruitingQueryParsing`) maxsus qoplab
 * qo'yishgan, lekin klient baribir keraksiz shovqin yubormasligi kerak.
 */
function params(source: Record<string, string | number | undefined>): Record<string, unknown> {
  const result: Record<string, unknown> = {}

  for (const [key, value] of Object.entries(source)) {
    if (value === undefined || value === '') continue
    result[key] = value
  }

  return result
}

/* ═══════════════════════════════════════════════════════════ xaritalash */

function toStage(raw: unknown): PipelineStage {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    name: text(box['name']) ?? '—',
    isActive: box['isActive'] === true,
    note: text(box['note']),
    sortOrder: count(box['sortOrder']),
    invitesToInterview: box['invitesToInterview'] === true,
  }
}

function toVacancy(raw: unknown): Vacancy {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    vacancyTitleId: text(box['vacancyTitleId']) ?? '',
    title: text(box['title']),
    openings: count(box['openings']),
    status: oneOf(box['status'], VACANCY_STATUSES, 'Open'),
    openedAt: text(box['openedAt']) ?? '',
    closedAt: text(box['closedAt']),
    responsibleEmployeeId: text(box['responsibleEmployeeId']),
    responsibleName: text(box['responsibleName']),
    note: text(box['note']),
    candidateCount: count(box['candidateCount']),
    hiredCount: count(box['hiredCount']),
  }
}

function toStageEntry(raw: unknown): CandidateStageEntry {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    stageId: text(box['stageId']) ?? '',
    stage: text(box['stage']),
    date: text(box['date']) ?? '',
    outcome: oneOf(box['outcome'], STAGE_OUTCOMES, 'Pending'),
    conclusion: text(box['conclusion']),
    recordedBy: text(box['recordedBy']),
    recordedAt: text(box['recordedAt']) ?? '',
  }
}

function toCandidateRow(raw: unknown): CandidateListItem {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    fullName: text(box['fullName']) ?? '—',
    shortName: text(box['shortName']) ?? '—',
    phone: text(box['phone']),
    birthDate: text(box['birthDate']),
    speciality: text(box['speciality']),
    vacancyId: text(box['vacancyId']) ?? '',
    vacancy: text(box['vacancy']),
    currentStageId: text(box['currentStageId']),
    currentStage: text(box['currentStage']),
    status: oneOf(box['status'], CANDIDATE_STATUSES, 'InProgress'),
    source: oneOf(box['source'], CANDIDATE_SOURCES, 'Manual'),
    hasResume: box['hasResume'] === true,
    createdAt: text(box['createdAt']) ?? '',
    hiredEmployeeId: text(box['hiredEmployeeId']),
  }
}

/**
 * Anketa. `null` — nomzodda anketa YO'Q (HR qo'lda kiritgan yoki bot
 * ishga tushgunicha yozilgan yozuv) va bu odatiy hol.
 */
function toQuestionnaire(raw: unknown): CandidateQuestionnaire | null {
  if (raw === null || raw === undefined || typeof raw !== 'object') return null
  const box = raw as Record<string, unknown>

  return {
    education: enumOrNull(box['education'], EDUCATION_LEVELS),
    workExperience: text(box['workExperience']),
    maritalStatus: enumOrNull(box['maritalStatus'], MARITAL_STATUSES),
    russianLevel: enumOrNull(box['russianLevel'], LANGUAGE_LEVELS),
    otherLanguages: text(box['otherLanguages']),
    officeSkills: text(box['officeSkills']),
    lastSalary: text(box['lastSalary']),
    extraNote: text(box['extraNote']),
    hasDriverLicense: box['hasDriverLicense'] === true,
    driverLicenseCategories: text(box['driverLicenseCategories']),
    expectedSalary: enumOrNull(box['expectedSalary'], SALARY_EXPECTATIONS),
    referralSource: enumOrNull(box['referralSource'], REFERRAL_SOURCES),
    offerAcceptedAt: text(box['offerAcceptedAt']),
  }
}

function toCandidate(raw: unknown): Candidate {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    lastName: text(box['lastName']) ?? '',
    firstName: text(box['firstName']) ?? '',
    middleName: text(box['middleName']),
    fullName: text(box['fullName']) ?? '—',
    birthDate: text(box['birthDate']),
    phone: text(box['phone']),
    address: text(box['address']),
    speciality: text(box['speciality']),
    vacancyId: text(box['vacancyId']) ?? '',
    vacancy: text(box['vacancy']),
    currentStageId: text(box['currentStageId']),
    currentStage: text(box['currentStage']),
    status: oneOf(box['status'], CANDIDATE_STATUSES, 'InProgress'),
    source: oneOf(box['source'], CANDIDATE_SOURCES, 'Manual'),
    note: text(box['note']),
    closedAt: text(box['closedAt']),
    closingNote: text(box['closingNote']),
    resumeFileName: text(box['resumeFileName']),
    resumeContentType: text(box['resumeContentType']),
    resumeSizeBytes:
      typeof box['resumeSizeBytes'] === 'number' ? box['resumeSizeBytes'] : null,
    hasPhoto: box['hasPhoto'] === true,
    hiredEmployeeId: text(box['hiredEmployeeId']),
    hiredEmploymentId: text(box['hiredEmploymentId']),
    interviewAt: text(box['interviewAt']),
    interviewNoticeSentAt: text(box['interviewNoticeSentAt']),
    stages: (Array.isArray(box['stages']) ? box['stages'] : []).map(toStageEntry),
    questionnaire: toQuestionnaire(box['questionnaire']),
  }
}

function toHireOutcome(raw: unknown): HireOutcome {
  const hire = (raw ?? {}) as Record<string, unknown>
  const employment = (hire['employment'] ?? {}) as Record<string, unknown>

  return {
    // Xodim identifikatori mehnat munosabati ICHIDA
    // (`HireResultDto.Employment` → `EmploymentDto`), yassi emas.
    employeeId: text(employment['employeeId']),
    employmentId: text(employment['id']),
    warnings: (Array.isArray(hire['warnings']) ? hire['warnings'] : []).flatMap((item) => {
      const warning = (item ?? {}) as Record<string, unknown>
      const code = text(warning['code'])
      return code === null ? [] : [{ code, message: text(warning['message']) ?? code }]
    }),
  }
}

function toDirectHireResult(raw: unknown): DirectHireResult {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    candidate: toCandidate(box['candidate']),
    hire: toHireOutcome(box['hire']),
  }
}

/* ═══════════════════════════════════════════════════════════ bosqichlar */

const STAGES = '/api/recruiting/stages'

export async function fetchStages(
  onlyActive: boolean,
  signal?: AbortSignal,
): Promise<PipelineStage[]> {
  const { data } = await api.get<unknown>(STAGES, { params: { onlyActive }, signal })
  return Array.isArray(data) ? data.map(toStage) : []
}

/*
  BOSQICHLARNI TAHRIRLASH KLIENTDA YO'Q (2026-08-31, foydalanuvchi
  qarori: «voronka bosqichini menyudan olib tashlayver, bizga umuman
  kerakmas»).

  Voronka uchta bosqichdan iborat va u MBF ning jarayoni — u yiliga
  ham o'zgarmaydi. Uni tahrirlash ekrani HR ning kundalik ishida
  hech qachon ochilmasdi va menyuda faqat joy egallab turardi.

  SERVERDAGI yozish endpoint'lari qoldi (`POST`, `PUT`, `/reorder`,
  `/activate`, `/deactivate`): jarayon o'zgarsa, bosqichlar
  konteynerni qayta yig'masdan o'zgartiriladi. Aynan shu yo'l bilan
  uchta ortiqcha bosqich arxivlangan edi.
*/

/* ══════════════════════════════════════════════════════════ vakansiyalar */

const VACANCIES = '/api/recruiting/vacancies'

export async function fetchVacancies(
  filters: VacancyFilters,
  signal?: AbortSignal,
): Promise<PagedResult<Vacancy>> {
  const { data } = await api.get<unknown>(VACANCIES, {
    params: params({
      status: filters.status,
      vacancyTitleId: filters.vacancyTitleId,
      page: filters.page,
      pageSize: filters.pageSize,
      sort: filters.sort,
    }),
    signal,
  })

  return readPage(data, toVacancy, filters)
}

export async function saveVacancy(id: string | null, body: VacancyInput): Promise<Vacancy> {
  const { data } =
    id === null
      ? await api.post<unknown>(VACANCIES, body)
      : await api.put<unknown>(`${VACANCIES}/${id}`, body)

  return toVacancy(data)
}

/**
 * Yopish va qayta ochish — bitta funksiya: tanasi ham, javobi ham bir xil.
 *
 * Amal nomi to'g'ridan-to'g'ri MANZILGA tushadi va bu ataylab: server
 * marshrutlari ham aynan shunday nomlanadi (`/close`, `/reopen`). Ilgari
 * bu yerda `pause` va `resume` ham bor edi — birinchisi butunlay
 * o'chirildi, ikkinchisi esa `reopen` ga qayta nomlandi, chunki
 * «davom ettirish» to'xtatishdan qolgan nom edi va endi hech narsa
 * davom ettirilmaydi.
 */
export async function changeVacancyStatus(
  id: string,
  action: 'close' | 'reopen',
  body: { reason?: string | null; closedAt?: string | null },
): Promise<Vacancy> {
  const { data } = await api.post<unknown>(`${VACANCIES}/${id}/${action}`, body)
  return toVacancy(data)
}

/**
 * Yumshoq o'chirish: yozuv bazada qoladi.
 *
 * ⚠️ Server FAQAT nomzodi yo'q vakansiyani o'chiradi
 * («vakansiya.nomzodlari_bor», 409): nomzodi bori — tarix va uni
 * o'chirish nomzodlarning yozuvini yetim qoldirardi. Ro'yxatda bunday
 * qatorda tugma umuman chizilmaydi (`vacancies-page.tsx`), lekin
 * xatoni ushlash baribir kerak — ro'yxat eskirgan bo'lishi mumkin.
 */
export async function deleteVacancy(id: string): Promise<void> {
  await api.delete(`${VACANCIES}/${id}`)
}

/* ═════════════════════════════════════════════════════════════ nomzodlar */

const CANDIDATES = '/api/recruiting/candidates'

export async function fetchCandidates(
  filters: CandidateFilters,
  signal?: AbortSignal,
): Promise<PagedResult<CandidateListItem>> {
  const { data } = await api.get<unknown>(CANDIDATES, {
    params: params({
      search: filters.search,
      vacancyId: filters.vacancyId,
      stageId: filters.stageId,
      status: filters.status,
      source: filters.source,
      page: filters.page,
      pageSize: filters.pageSize,
      sort: filters.sort,
    }),
    signal,
  })

  return readPage(data, toCandidateRow, filters)
}

export async function fetchCandidate(id: string, signal?: AbortSignal): Promise<Candidate> {
  const { data } = await api.get<unknown>(`${CANDIDATES}/${id}`, { signal })
  return toCandidate(data)
}

/**
 * Nomzodni voronkaga kiritish.
 *
 * `source` yuborilmaydi — server sukut bo'yicha `Manual` qo'yadi va bu
 * ekrandan kiritilgan har bir nomzod aynan shunday. `TelegramBot` ni
 * faqat botning o'zi yozadi (keyingi agent).
 */
export async function registerCandidate(body: RegisterCandidateInput): Promise<Candidate> {
  const { data } = await api.post<unknown>(CANDIDATES, body)
  return toCandidate(data)
}

/** Tahrirlash — FAQAT shaxsiy maydonlar; vakansiya va manba o'zgarmaydi. */
export async function updateCandidate(id: string, body: CandidateInput): Promise<Candidate> {
  const { data } = await api.put<unknown>(`${CANDIDATES}/${id}`, body)
  return toCandidate(data)
}

/** Vakansiyani almashtirish — alohida amal: nomzod boshqa o'ringa mos kelib qolgan. */
export async function reassignCandidate(id: string, vacancyId: string): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/reassign`, { vacancyId })
  return toCandidate(data)
}

/**
 * ⭐ Voronkaning YAGONA harakati.
 *
 * Oxirgi yozuv aynan shu bosqich uchun va `Pending` bo'lsa, u
 * TO'LDIRILADI (yangisi qo'shilmaydi) — HR suhbatni «kutilmoqda» deb
 * ochib, tugagach natijasini yozadi. Oldinga yurish majburlanmaydi:
 * bosqichni o'tkazib yuborish va orqaga qaytish ham mumkin.
 */
/**
 * ⭐ Nomzodni suhbatga chaqirish — bitta so'rovda uchta natija.
 *
 * Server bosqichni yozadi, suhbat vaqtini qo'yadi va Telegram taklifini
 * navbatga tashlaydi. Ularni uch so'rovga bo'lish «bosqich yozildi,
 * xabar ketmadi» degan yarim holatni tug'dirardi.
 *
 * ⚠️ `interviewAt` SILJISH bilan yuboriladi («…+05:00»). Siljishsiz
 * qiymatni server UTC deb o'qiydi va nomzod besh soat oldingi vaqtni
 * ko'rardi.
 *
 * `stageId` bo'sh bo'lsa bosqich o'zgarmaydi — suhbatni KO'CHIRISH
 * aynan shunday ishlaydi.
 */
export async function inviteCandidateToInterview(
  id: string,
  body: {
    interviewAt: string
    stageId: string | null
    conclusion: string | null
  },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/interview-invite`, body)
  return toCandidate(data)
}

export async function recordCandidateStage(
  id: string,
  body: {
    stageId: string
    outcome: StageOutcome
    date: string | null
    conclusion: string | null
  },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/stages`, body)
  return toCandidate(data)
}

/**
 * Voronkadan chiqarish: «rad etdik» (`reject`), «o'zi voz kechdi»
 * (`withdraw`) va «rezervga» (`reserve`). Uchalasi ATAYLAB ajratilgan —
 * voronka hisobotida ular butunlay boshqa ma'no beradi
 * (`CandidateStatus` izohi): birinchisi tanlov sifati, ikkinchisi
 * taklif shartlari, uchinchisi esa «odam yaxshi, o'rin yo'q» haqida
 * gapiradi.
 *
 * Uchalasining SO'ROV TANASI bir xil (`CloseCandidateRequest`: sana va
 * sabab), ya'ni bitta funksiya yetadi. `reserve` dan farqli o'laroq,
 * qolgan ikkitasi qaytarilmaydi — qaytarish faqat rezervda bor
 * (`returnCandidateFromReserve`).
 */
export async function closeCandidate(
  id: string,
  action: 'reject' | 'withdraw' | 'reserve',
  body: { at: string | null; reason: string | null },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/${action}`, body)
  return toCandidate(data)
}

/**
 * ⭐ Rezervdagi nomzodni voronkaga QAYTARADI — rezervning butun ma'nosi
 * shu amalda.
 *
 * So'rov tanasi YO'Q va bo'lishi ham kerak emas: nomzod qaysi bosqichda
 * rezervga o'tgan bo'lsa, o'sha yerdan davom etadi (server bosqichlar
 * tarixini saqlaydi), chiqish sanasi va sababi esa tozalanadi.
 *
 * Manba FAQAT rezerv: rad etilgan, voz kechgan yoki ishga olingan
 * nomzod uchun server «nomzod.rezervda_emas» (409) qaytaradi.
 */
export async function returnCandidateFromReserve(id: string): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/return-from-reserve`)
  return toCandidate(data)
}

/**
 * ⚠️ QAYTARIB BO'LMAYDIGAN AMAL: nomzod XODIMGA aylanadi.
 *
 * Bitta tranzaksiyada: nomzod «ishga olingan» bo'lib voronkadan chiqadi,
 * xodim kartasi, mehnat munosabati, ishga qabul buyrug'i va MAJBURIY
 * sinov muddati yaratiladi.
 */
export async function hireCandidate(
  id: string,
  body: HireCandidateInput,
): Promise<DirectHireResult> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/hire`, body)
  return toDirectHireResult(data)
}

/* ─────────────────────────────────────────────────────────────── rezyume */

/**
 * Rezyume yuklash — **multipart**, maydon nomi `file`.
 *
 * `Content-Type` bu yerda QO'YILMAYDI: axios `FormData` ni ko'rib uni
 * chegara (boundary) satri bilan o'zi yozadi — sabab `http-client.ts` da.
 */
export async function uploadResume(id: string, file: File): Promise<Candidate> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/resume`, form)
  return toCandidate(data)
}

/**
 * Rezyume baytlari — BLOB, havola emas.
 *
 * NEGA `<a href="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q. Brauzer bu so'rovga sarlavha
 * qo'sha olmaydi va server 401 qaytaradi (`documents-api.ts` dagi
 * `fetchDocumentFile` bilan aynan bir xil sabab).
 */
export async function fetchResume(
  id: string,
  signal?: AbortSignal,
): Promise<{ blob: Blob; disposition: string | null }> {
  const response = await api.get<Blob>(`${CANDIDATES}/${id}/resume`, {
    responseType: 'blob',
    signal,
  })

  return {
    blob: response.data,
    disposition: response.headers['content-disposition'] as string | null,
  }
}

/** Fayl HAQIQATAN diskdan o'chiriladi — unga boshqa hech kim ishora qilmaydi. */
export async function deleteResume(id: string): Promise<Candidate> {
  const { data } = await api.delete<unknown>(`${CANDIDATES}/${id}/resume`)
  return toCandidate(data)
}

/* ──────────────────────────────────────────────────────────────── surat */

/**
 * Nomzod surati — BAYTLAR, havola emas.
 *
 * NEGA `<img src="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q — brauzer `<img>` so'roviga
 * sarlavha qo'sha olmaydi va server 401 qaytaradi (`fetchEmployeePhoto`
 * bilan aynan bir xil sabab va aynan bir xil quvur).
 *
 * `404` — XATO EMAS. U «bu nomzodning surati yo'q» degani va `null` ga
 * aylanadi: chaqiruvchi jimgina bosh harflarga qaytadi.
 */
export async function fetchCandidatePhoto(
  id: string,
  signal?: AbortSignal,
): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>(`${CANDIDATES}/${id}/photo`, {
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
 * Suratni yuklash — **multipart**, maydon nomi `file`. Yangisi eskisini
 * almashtiradi.
 *
 * Javobdagi `photoKey` ATAYLAB qaytarilmaydi: nomzod kartasi uni umuman
 * ko'rsatmaydi (`CandidateDto` da faqat `hasPhoto` bor), ya'ni undan
 * foydalanadigan joy yo'q. Keshni yangilash `useUploadCandidatePhoto`
 * zimmasida.
 *
 * `Content-Type` bu yerda QO'YILMAYDI: axios `FormData` ni ko'rib uni
 * chegara (boundary) satri bilan o'zi yozadi — sabab `http-client.ts` da.
 */
export async function uploadCandidatePhoto(id: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)

  await api.post(`${CANDIDATES}/${id}/photo`, form)
}

/** Suratni o'chirish. Server idempotent: surat bo'lmasa ham `204` qaytaradi. */
export async function deleteCandidatePhoto(id: string): Promise<void> {
  await api.delete(`${CANDIDATES}/${id}/photo`)
}
