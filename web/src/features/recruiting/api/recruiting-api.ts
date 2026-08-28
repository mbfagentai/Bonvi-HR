/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employments-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Maydon nomlari TAXMIN emas — ular haqiqiy manbadan olingan va jonli
 * endpoint'da tekshirilgan (`GET /api/recruiting/stages` javobi bilan
 * solishtirildi):
 *   api/src/Bonvi.Application/Recruiting/Dtos/RecruitingDtos.cs
 *   api/src/Bonvi.Api/Endpoints/{Vacancy,Candidate,JobOffer,PipelineStage}Endpoints.cs
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `recruiting-queries.ts` generatsiya qilingan hook'larga o'tkaziladi.
 *
 * NEGA butun rekruting BITTA faylda (vakansiya, nomzod, taklif, bosqich).
 * To'rttala endpoint bitta shartnomaning bo'laklari: nomzod kartasi
 * javobida taklif ham, bosqich yozuvi ham bor (`CandidateDto.offers`,
 * `.stages`), ya'ni tiplar bir-biriga bog'langan. Fayllarga bo'linsa,
 * ular orasida o'zaro import paydo bo'lardi va `unknown` dan o'qish
 * yordamchilari (`text`, `money`, `readPage`) ham nusxalanardi —
 * `employees-api.ts` da aynan shunday bir necha nusxa hosil bo'lgan edi.
 */
import type { MoneyValue, PagedResult } from '@/features/employees/api/employees-api'
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

export const VACANCY_STATUSES = ['Open', 'Paused', 'Closed'] as const
export type VacancyStatus = (typeof VACANCY_STATUSES)[number]

export const CANDIDATE_STATUSES = ['InProgress', 'Rejected', 'Hired', 'Withdrawn'] as const
export type CandidateStatus = (typeof CANDIDATE_STATUSES)[number]

export const CANDIDATE_SOURCES = ['Manual', 'TelegramBot'] as const
export type CandidateSource = (typeof CANDIDATE_SOURCES)[number]

export const STAGE_OUTCOMES = ['Pending', 'Passed', 'Failed'] as const
export type StageOutcome = (typeof STAGE_OUTCOMES)[number]

export const OFFER_STATUSES = ['Sent', 'Accepted', 'Declined', 'Expired'] as const
export type JobOfferStatus = (typeof OFFER_STATUSES)[number]

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
}

/** `VacancyDto` — ro'yxat va karta uchun umumiy ko'rinish. */
export interface Vacancy {
  id: string
  positionId: string
  position: string | null
  legalEntityId: string
  legalEntity: string | null
  departmentId: string | null
  department: string | null
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

/** `CandidateDto` — nomzod kartasi: bosqichlar tarixi va takliflari bilan. */
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
  hiredEmployeeId: string | null
  hiredEmploymentId: string | null
  /** Yozilgan tartibda. */
  stages: CandidateStageEntry[]
  /** Yangidan eskiga — muzokara tarixi shu tartibda o'qiladi. */
  offers: JobOffer[]
}

/** `JobOfferDto` — ish taklifi. */
export interface JobOffer {
  id: string
  candidateId: string
  candidateName: string | null
  vacancyId: string
  vacancy: string | null
  salary: MoneyValue
  startDate: string
  sentAt: string
  expiresAt: string | null
  status: JobOfferStatus
  respondedAt: string | null
  responseNote: string | null
  note: string | null
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
 * `CandidateHireResultDto` — taklif qabul qilinganining natijasi.
 *
 * Uchalasi BIRGA qaytadi, chunki bu bitta amalning uch tomoni: taklif
 * yopildi, nomzod «ishga olingan» bo'ldi va XODIM yaratildi. Ekran
 * shu javobdan darhol yangi xodim kartasiga havola qo'yadi —
 * qo'shimcha so'rovsiz.
 */
export interface CandidateHireResult {
  offer: JobOffer
  candidate: Candidate
  hire: {
    /** Yaratilgan mehnat munosabati — bizga faqat `employeeId` kerak. */
    employeeId: string | null
    employmentId: string | null
    /** ⚠️ E'tiborsiz qolmasligi kerak: stavka chegarasi va h.k. */
    warnings: HrWarning[]
  }
}

/* ═══════════════════════════════════════════════════ so'rov tanalari */

export interface VacancyInput {
  positionId: string
  legalEntityId: string
  departmentId: string | null
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

export interface OfferInput {
  salaryAmount: number
  salaryCurrency: string | null
  startDate: string | null
  expiresAt: string | null
  note: string | null
}

/** Taklif qabul qilinganda so'raladigan HUJJATIY qism — shaxsiy ma'lumot nomzoddan ko'chadi. */
export interface AcceptOfferInput {
  respondedAt: string | null
  responseNote: string | null
  hireDate: string | null
  orderNo: string | null
  orderDate: string | null
  pinfl: string | null
  passport: string | null
  rate: number | null
  isPrimary: boolean
  responsibleEmployeeId: string | null
  probationDays: number | null
}

/* ═══════════════════════════════════════════════════════════════ filtrlar */

export interface VacancyFilters {
  status?: VacancyStatus
  positionId?: string
  departmentId?: string
  legalEntityId?: string
  page: number
  pageSize: number
  /** `openedAt` (sukut, yangidan eskiga) yoki `position`; `-` — kamayish. */
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

/** `MoneyDto` — `{amount, currency, formatted}`; bizga birinchi ikkitasi kerak. */
function money(value: unknown): MoneyValue {
  const box = (value ?? {}) as Record<string, unknown>
  const amount = box['amount']

  return {
    amount: typeof amount === 'number' && Number.isFinite(amount) ? amount : 0,
    currency: text(box['currency']) ?? 'UZS',
  }
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
  }
}

function toVacancy(raw: unknown): Vacancy {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    positionId: text(box['positionId']) ?? '',
    position: text(box['position']),
    legalEntityId: text(box['legalEntityId']) ?? '',
    legalEntity: text(box['legalEntity']),
    departmentId: text(box['departmentId']),
    department: text(box['department']),
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

function toOffer(raw: unknown): JobOffer {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    candidateId: text(box['candidateId']) ?? '',
    candidateName: text(box['candidateName']),
    vacancyId: text(box['vacancyId']) ?? '',
    vacancy: text(box['vacancy']),
    salary: money(box['salary']),
    startDate: text(box['startDate']) ?? '',
    sentAt: text(box['sentAt']) ?? '',
    expiresAt: text(box['expiresAt']),
    status: oneOf(box['status'], OFFER_STATUSES, 'Sent'),
    respondedAt: text(box['respondedAt']),
    responseNote: text(box['responseNote']),
    note: text(box['note']),
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
    hiredEmployeeId: text(box['hiredEmployeeId']),
    hiredEmploymentId: text(box['hiredEmploymentId']),
    stages: (Array.isArray(box['stages']) ? box['stages'] : []).map(toStageEntry),
    offers: (Array.isArray(box['offers']) ? box['offers'] : []).map(toOffer),
  }
}

function toHireResult(raw: unknown): CandidateHireResult {
  const box = (raw ?? {}) as Record<string, unknown>
  const hire = (box['hire'] ?? {}) as Record<string, unknown>
  const employment = (hire['employment'] ?? {}) as Record<string, unknown>

  return {
    offer: toOffer(box['offer']),
    candidate: toCandidate(box['candidate']),
    hire: {
      // Xodim identifikatori mehnat munosabati ICHIDA
      // (`HireResultDto.Employment` → `EmploymentDto`), yassi emas.
      employeeId: text(employment['employeeId']),
      employmentId: text(employment['id']),
      warnings: (Array.isArray(hire['warnings']) ? hire['warnings'] : []).flatMap((item) => {
        const warning = (item ?? {}) as Record<string, unknown>
        const code = text(warning['code'])
        return code === null ? [] : [{ code, message: text(warning['message']) ?? code }]
      }),
    },
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

export async function saveStage(
  id: string | null,
  body: { name: string; note: string | null },
): Promise<PipelineStage> {
  const { data } =
    id === null
      ? await api.post<unknown>(STAGES, body)
      : await api.put<unknown>(`${STAGES}/${id}`, body)

  return toStage(data)
}

/**
 * Butun tartib BITTA so'rovda — har siljishda alohida emas
 * (`reorderDuties` bilan bir xil naqsh). Server ro'yxat TO'LIQ
 * bo'lishini talab qiladi: yetishmasa «bosqich.tartib_royxati».
 */
export async function reorderStages(orderedIds: string[]): Promise<PipelineStage[]> {
  const { data } = await api.post<unknown>(`${STAGES}/reorder`, { orderedIds })
  return Array.isArray(data) ? data.map(toStage) : []
}

export async function setStageActive(id: string, active: boolean): Promise<PipelineStage> {
  const { data } = await api.post<unknown>(`${STAGES}/${id}/${active ? 'activate' : 'deactivate'}`)
  return toStage(data)
}

/* ══════════════════════════════════════════════════════════ vakansiyalar */

const VACANCIES = '/api/recruiting/vacancies'

export async function fetchVacancies(
  filters: VacancyFilters,
  signal?: AbortSignal,
): Promise<PagedResult<Vacancy>> {
  const { data } = await api.get<unknown>(VACANCIES, {
    params: params({
      status: filters.status,
      positionId: filters.positionId,
      departmentId: filters.departmentId,
      legalEntityId: filters.legalEntityId,
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

/** To'xtatish, davom ettirish va yopish — bitta funksiya: tanasi ham, javobi ham bir xil. */
export async function changeVacancyStatus(
  id: string,
  action: 'pause' | 'resume' | 'close',
  body: { reason?: string | null; closedAt?: string | null },
): Promise<Vacancy> {
  const { data } = await api.post<unknown>(`${VACANCIES}/${id}/${action}`, body)
  return toVacancy(data)
}

/** Yumshoq o'chirish: yozuv bazada qoladi, nomzodlari yo'qolmaydi. */
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
 * Voronkadan chiqarish: «rad etdik» (`reject`) va «o'zi voz kechdi»
 * (`withdraw`). Ikkalasi ATAYLAB ajratilgan — voronka hisobotida
 * ular butunlay boshqa ma'no beradi (`CandidateStatus` izohi).
 */
export async function closeCandidate(
  id: string,
  action: 'reject' | 'withdraw',
  body: { at: string | null; reason: string | null },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/${action}`, body)
  return toCandidate(data)
}

/** Yumshoq o'chirish — faqat XATO kiritilgan yozuv uchun. */
export async function deleteCandidate(id: string): Promise<void> {
  await api.delete(`${CANDIDATES}/${id}`)
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

/* ═════════════════════════════════════════════════════════════ takliflar */

const OFFERS = '/api/recruiting/offers'

/**
 * Takliflar — DOIM nomzod bo'yicha (`candidateId` majburiy).
 *
 * NEGA alohida so'rov, nomzod kartasida ular allaqachon borligiga
 * qaramay: kartadagi ro'yxat karta so'rovi bilan birga eskiradi, bu
 * so'rov esa taklif amalidan keyin darhol yangilanadi. Ikkalasini bir
 * joyga yig'ish uchun karta so'rovini butunlay qayta yuklash kerak
 * bo'lardi — bu esa nomzodning bosqichlar tarixini ham beparvo
 * qayta tortardi.
 */
export async function fetchOffers(candidateId: string, signal?: AbortSignal): Promise<JobOffer[]> {
  const { data } = await api.get<unknown>(OFFERS, { params: { candidateId }, signal })
  return Array.isArray(data) ? data.map(toOffer) : []
}

export async function saveOffer(
  id: string | null,
  candidateId: string,
  body: OfferInput,
): Promise<JobOffer> {
  const { data } =
    id === null
      ? await api.post<unknown>(OFFERS, { candidateId, ...body })
      : await api.put<unknown>(`${OFFERS}/${id}`, body)

  return toOffer(data)
}

/** Rad javobi va muddat o'tishi — ikkalasi ham taklifni YOPADI, nomzodni emas. */
export async function closeOffer(
  id: string,
  action: 'decline' | 'expire',
  body: { at: string | null; note: string | null },
): Promise<JobOffer> {
  const { data } = await api.post<unknown>(`${OFFERS}/${id}/${action}`, body)
  return toOffer(data)
}

/**
 * ⚠️ QAYTARIB BO'LMAYDIGAN AMAL: nomzod XODIMGA aylanadi.
 *
 * Bitta tranzaksiyada: taklif «qabul qilindi», nomzod «ishga olingan»,
 * xodim kartasi va uning MAJBURIY sinov muddati yaratiladi.
 */
export async function acceptOffer(
  id: string,
  body: AcceptOfferInput,
): Promise<CandidateHireResult> {
  const { data } = await api.post<unknown>(`${OFFERS}/${id}/accept`, body)
  return toHireResult(data)
}
