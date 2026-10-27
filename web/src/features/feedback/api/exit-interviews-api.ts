/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Endpoint ALLAQACHON TAYYOR va testlangan (boshqa agent backend'ni
 * yozgan), shu sababli maydon nomlari «kutilayotgan shartnoma» emas —
 * ular haqiqiy manbadan olingan:
 *   api/src/Bonvi.Domain/Feedback/ExitInterview.cs
 *   api/src/Bonvi.Application/Feedback/Dtos/ExitInterviewDtos.cs
 *   api/src/Bonvi.Api/Endpoints/ExitInterviewEndpoints.cs
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * generatsiya qilingan hook'larga o'tkaziladi.
 */
import axios from 'axios'

import { api } from '@/shared/api/http-client'

/**
 * NEGA satr birlashmasi, C# enum'ining o'zi emas: `incidents-api.ts` dagi
 * xuddi shu izohga qarang. Tartib C# `RecommendationAnswer` qiymatlariga
 * mos (1 dan boshlanadi): `Yes = 1` → `names[0]`.
 */
export const RECOMMENDATION_ANSWERS = ['yes', 'no', 'dontKnow'] as const
export type RecommendationAnswerValue = (typeof RECOMMENDATION_ANSWERS)[number]

/**
 * Qayta aloqa (exit interview) yozuvi — bog'langan mehnat munosabatining
 * ish joyi/bo'lim/lavozim/sabab ma'lumotlari bilan birga.
 *
 * `employmentId`ga bog'langan, `employeeId`ga EMAS (`ExitInterview.cs`
 * izohiga qarang): bitta shaxsning bir nechta mehnat munosabati bo'lishi
 * mumkin, suhbat aynan QAYSI ketishga tegishli ekani muhim.
 */
export interface ExitInterview {
  id: string
  employmentId: string
  employeeId: string | null
  employeeName: string | null
  photoKey: string | null
  legalEntityId: string | null
  legalEntityName: string | null
  departmentId: string | null
  departmentName: string | null
  positionId: string | null
  positionName: string | null
  /** Bo'shatish sanasi — bog'langan `Employment`dan. */
  terminationDate: string | null
  /** Rasmiy buyruqdagi sabab — `reason` (xodimning o'z so'zi) bilan ataylab yonma-yon. */
  terminationReasonName: string | null
  interviewDate: string
  reason: string
  wouldRecommend: RecommendationAnswerValue | null
  impressions: string | null
  conditionsToReturn: string | null
}

export interface ExitInterviewFilters {
  /** `DateOnly` → «2026-08-25». Ikki tomondan yopiq oraliq (suhbat sanasi bo'yicha). */
  from?: string
  to?: string
  /**
   * Xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon.
   *
   * Suhbat MATNI (sabab, taassurot) qidiruvga KIRMAYDI — server ham
   * shunday: erkin matnda begona odamlarning ismi tez-tez uchraydi
   * («Karimov bilan ishlash og'ir edi») va u qidiruvga qo'shilsa,
   * Karimovni izlagan HR unga UMUMAN aloqasi yo'q suhbatni ko'rardi.
   */
  search?: string
}

/** Yaratish va tahrirlashda umumiy maydonlar — `employmentId` bu yerda ATAYLAB YO'Q. */
export interface ExitInterviewFormRequest {
  interviewDate: string
  reason: string
  wouldRecommend: RecommendationAnswerValue | null
  impressions: string | null
  conditionsToReturn: string | null
}

/**
 * `POST` tanasi. `employmentId` FAQAT bu yerda: tahrirlashda mehnat
 * munosabati o'zgarmaydi (`UpdateExitInterviewRequest`da yo'q) — noto'g'ri
 * munosabatga yozilgan bo'lsa, yozuv o'chirilib yangisi kiritiladi.
 */
export interface RecordExitInterviewRequest extends ExitInterviewFormRequest {
  employmentId: string
}

export type UpdateExitInterviewRequest = ExitInterviewFormRequest

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * `names` tartibi C# enum qiymatlariga mos (1 dan boshlanadi). `raw` `null`
 * yoki `undefined` bo'lsa — `null` (`wouldRecommend` ixtiyoriy savol,
 * xodim javob bermagan bo'lishi mumkin).
 */
function enumValueOrNull<T extends string>(raw: unknown, names: readonly T[]): T | null {
  if (raw === null || raw === undefined) return null
  if (typeof raw === 'number') return names[raw - 1] ?? null

  if (typeof raw === 'string') {
    const needle = raw.toLowerCase()
    return names.find((name) => name.toLowerCase() === needle) ?? null
  }

  return null
}

/** Frontend qiymatini C# enum nomiga qaytaradi: «dontKnow» → «DontKnow». */
function enumName(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

function toExitInterview(raw: unknown): ExitInterview {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employmentId: text(box['employmentId']) ?? '',
    employeeId: text(box['employeeId']),
    employeeName: text(box['employeeName']),
    photoKey: text(box['photoKey']),
    legalEntityId: text(box['legalEntityId']),
    legalEntityName: text(box['legalEntityName']),
    departmentId: text(box['departmentId']),
    departmentName: text(box['departmentName']),
    positionId: text(box['positionId']),
    positionName: text(box['positionName']),
    terminationDate: text(box['terminationDate']),
    terminationReasonName: text(box['terminationReasonName']),
    interviewDate: text(box['interviewDate']) ?? '',
    reason: text(box['reason']) ?? '',
    wouldRecommend: enumValueOrNull(box['wouldRecommend'], RECOMMENDATION_ANSWERS),
    impressions: text(box['impressions']),
    conditionsToReturn: text(box['conditionsToReturn']),
  }
}

/**
 * Ro'yxat so'rovining parametrlari — ro'yxat ham, Excel eksporti ham AYNAN
 * shu funksiyadan (`docs/SPEC-excel-eksport.md` §3). Ro'yxat sahifalanmaydi.
 */
export function exitInterviewListParams(filters: ExitInterviewFilters): Record<string, unknown> {
  const params: Record<string, unknown> = {}
  if (filters.from) params['from'] = filters.from
  if (filters.to) params['to'] = filters.to

  // Qidiruv SERVERGA yuboriladi, klientda `filter()` qilinmaydi: ism
  // yig'ish va telefon ajratgichlarini tashlash qoidalari bazada
  // yashaydi va ularning ikkinchi nusxasi vaqt o'tib ajralib ketardi.
  if (filters.search !== undefined && filters.search.trim() !== '') {
    params['search'] = filters.search
  }

  return params
}

/** `from`/`to` bo'lmasa — hammasi, suhbat sanasi bo'yicha kamayish tartibida (server saralaydi). */
export async function fetchExitInterviews(
  filters: ExitInterviewFilters,
  signal?: AbortSignal,
): Promise<ExitInterview[]> {
  const { data } = await api.get<unknown>('/api/exit-interviews', {
    params: exitInterviewListParams(filters),
    signal,
  })
  return Array.isArray(data) ? data.map(toExitInterview) : []
}

/**
 * Bitta XODIMning barcha suhbatlari — xodim kartasidagi «Belgilar» tabi uchun.
 *
 * Munosabat bo'yicha bittalab so'rash o'rniga bitta so'rov: bir shaxsning
 * bir nechta ketishi bo'lishi mumkin va React'da ularni sikl ichida
 * so'rash hooklar qoidasini buzardi. Bo'sh massiv — odatiy hol.
 */
export async function fetchEmployeeExitInterviews(
  employeeId: string,
  signal?: AbortSignal,
): Promise<ExitInterview[]> {
  const { data } = await api.get<unknown>(`/api/exit-interviews/by-employee/${employeeId}`, {
    signal,
  })
  return Array.isArray(data) ? data.map(toExitInterview) : []
}

/**
 * Bitta mehnat munosabati bo'yicha suhbat.
 *
 * `404` — XATO EMAS. U «bu munosabatga hali suhbat yozilmagan» degani
 * (`fetchEmployeePhoto` dagi bilan bir xil naqsh, `employees-api.ts`):
 * ko'p munosabatda suhbat umuman bo'lmasligi mumkin, bu — kutilgan holat.
 */
export async function fetchExitInterviewByEmployment(
  employmentId: string,
  signal?: AbortSignal,
): Promise<ExitInterview | null> {
  try {
    const { data } = await api.get<unknown>(`/api/exit-interviews/by-employment/${employmentId}`, {
      signal,
    })
    return toExitInterview(data)
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) return null
    throw error
  }
}

export async function createExitInterview(
  body: RecordExitInterviewRequest,
): Promise<ExitInterview> {
  const { data } = await api.post<unknown>('/api/exit-interviews', {
    ...body,
    wouldRecommend: body.wouldRecommend === null ? null : enumName(body.wouldRecommend),
  })
  return toExitInterview(data)
}

export async function updateExitInterview(
  id: string,
  body: UpdateExitInterviewRequest,
): Promise<ExitInterview> {
  const { data } = await api.put<unknown>(`/api/exit-interviews/${id}`, {
    ...body,
    wouldRecommend: body.wouldRecommend === null ? null : enumName(body.wouldRecommend),
  })
  return toExitInterview(data)
}

/** Yumshoq o'chirish — server javobi `204`. */
export async function deleteExitInterview(id: string): Promise<void> {
  await api.delete(`/api/exit-interviews/${id}`)
}
