/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Endpoint ALLAQACHON TAYYOR va testlangan (boshqa agent backend'ni
 * yozgan), shu sababli maydon nomlari «kutilayotgan shartnoma» emas —
 * ular haqiqiy manbadan olingan:
 *   api/src/Bonvi.Domain/Safety/Incident.cs
 *   api/src/Bonvi.Application/Safety/Dtos/IncidentDtos.cs
 *   api/src/Bonvi.Api/Endpoints/IncidentEndpoints.cs
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * generatsiya qilingan hook'larga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'

/**
 * NEGA satr birlashmasi, C# enum'ining o'zi emas: `incentives-api.ts`
 * dagi xuddi shu izohga qarang. Tartib C# `IncidentSeverity` qiymatlariga
 * mos (1 dan boshlanadi): `Minor = 1` → `names[0]`.
 */
export const INCIDENT_SEVERITIES = ['minor', 'moderate', 'severe', 'fatal'] as const
export type IncidentSeverityValue = (typeof INCIDENT_SEVERITIES)[number]

/** Baxtsiz hodisa yozuvi — jabrlangan va mas'ul xodim nomlari bilan. */
export interface Incident {
  id: string
  employeeId: string
  employeeName: string | null
  photoKey: string | null
  incidentDate: string
  shift: string | null
  positionId: string | null
  positionName: string | null
  location: string
  description: string
  cause: string | null
  correctiveAction: string | null
  severity: IncidentSeverityValue
  medicalAidProvided: boolean
  medicalAidNote: string | null
  /** Komissiya xulosasi — yaratishda bo'sh, `/conclude` orqali keyinroq to'ldiriladi. */
  commissionConclusion: string | null
  concludedAt: string | null
  responsibleEmployeeId: string | null
  responsibleName: string | null
}

export interface IncidentFilters {
  /** `DateOnly` → «2026-08-25». Ikki tomondan yopiq oraliq. */
  from?: string
  to?: string
  severity?: IncidentSeverityValue
  /**
   * JABRLANGAN xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki
   * telefon.
   *
   * Hodisa TAVSIFI va MAS'UL shaxs qidiruvga kirmaydi (server ham
   * shunday): tavsifda begona odamlarning ismi tez-tez uchraydi
   * («Karimov guvoh bo'ldi»), komissiya raisi esa o'nlab hodisada
   * turadi — ikkalasi ham qidiruvni ma'nosiz qilardi.
   */
  search?: string
}

/**
 * Yaratish va tahrirlashda umumiy maydonlar.
 *
 * `employeeId` BU YERDA ATAYLAB YO'Q: `UpdateIncidentRequest`da u umuman
 * yo'q (`IncidentEndpoints.cs`) — tahrirlashda jabrlangan xodim
 * o'zgarmaydi, xuddi `Incentive.Update` dagi kabi (noto'g'ri xodimga
 * yozilgan bo'lsa, bu tuzatish emas, boshqa fakt).
 */
export interface IncidentFormRequest {
  incidentDate: string
  location: string
  description: string
  severity: IncidentSeverityValue
  shift: string | null
  positionId: string | null
  cause: string | null
  correctiveAction: string | null
  medicalAidProvided: boolean
  medicalAidNote: string | null
  responsibleEmployeeId: string | null
}

/**
 * `POST` tanasi. Komissiya xulosasi BU YERDA ATAYLAB YO'Q — u tekshiruv
 * tugagach, alohida `/conclude` orqali qo'shiladi (tekshiruv vaqt oladi;
 * darhol so'ralsa, HR uni to'qib yozishga majbur bo'lardi).
 */
export interface RecordIncidentRequest extends IncidentFormRequest {
  employeeId: string
}

export type UpdateIncidentRequest = IncidentFormRequest

export interface ConcludeIncidentRequest {
  conclusion: string
  /** `null` — server bugungi sanani qo'yadi. */
  concludedAt: string | null
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * `names` tartibi C# enum qiymatlariga mos (1 dan boshlanadi).
 * Ikkalasini ham qabul qilamiz (son va satr) — `incentives-api.ts`
 * dagi `enumValue` bilan bir xil sabab.
 */
function enumValue<T extends string>(raw: unknown, names: readonly T[], fallback: T): T {
  if (typeof raw === 'number') return names[raw - 1] ?? fallback

  if (typeof raw === 'string') {
    const needle = raw.toLowerCase()
    return names.find((name) => name.toLowerCase() === needle) ?? fallback
  }

  return fallback
}

/** Frontend qiymatini C# enum nomiga qaytaradi: «severe» → «Severe». */
function enumName(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

function toIncident(raw: unknown): Incident {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    employeeName: text(box['employeeName']),
    photoKey: text(box['photoKey']),
    incidentDate: text(box['incidentDate']) ?? '',
    shift: text(box['shift']),
    positionId: text(box['positionId']),
    positionName: text(box['positionName']),
    location: text(box['location']) ?? '',
    description: text(box['description']) ?? '',
    cause: text(box['cause']),
    correctiveAction: text(box['correctiveAction']),
    severity: enumValue(box['severity'], INCIDENT_SEVERITIES, 'minor'),
    medicalAidProvided: box['medicalAidProvided'] === true,
    medicalAidNote: text(box['medicalAidNote']),
    commissionConclusion: text(box['commissionConclusion']),
    concludedAt: text(box['concludedAt']),
    responsibleEmployeeId: text(box['responsibleEmployeeId']),
    responsibleName: text(box['responsibleName']),
  }
}

function listParams(filters: IncidentFilters): Record<string, unknown> {
  const params: Record<string, unknown> = {}
  if (filters.from) params['from'] = filters.from
  if (filters.to) params['to'] = filters.to
  if (filters.severity !== undefined) params['severity'] = enumName(filters.severity)

  // Qidiruv SERVERGA yuboriladi, klientda `filter()` qilinmaydi: ism
  // yig'ish va telefon ajratgichlarini tashlash qoidalari bazada
  // yashaydi va ularning ikkinchi nusxasi vaqt o'tib ajralib ketardi.
  if (filters.search !== undefined && filters.search.trim() !== '') {
    params['search'] = filters.search
  }

  return params
}

/** `from`/`to` bo'lmasa — hammasi, hodisa sanasi bo'yicha kamayish tartibida (server saralaydi). */
export async function fetchIncidents(
  filters: IncidentFilters,
  signal?: AbortSignal,
): Promise<Incident[]> {
  const { data } = await api.get<unknown>('/api/safety/incidents', {
    params: listParams(filters),
    signal,
  })
  return Array.isArray(data) ? data.map(toIncident) : []
}

export async function fetchIncident(id: string, signal?: AbortSignal): Promise<Incident> {
  const { data } = await api.get<unknown>(`/api/safety/incidents/${id}`, { signal })
  return toIncident(data)
}

export async function createIncident(body: RecordIncidentRequest): Promise<Incident> {
  const { data } = await api.post<unknown>('/api/safety/incidents', body)
  return toIncident(data)
}

export async function updateIncident(id: string, body: UpdateIncidentRequest): Promise<Incident> {
  const { data } = await api.put<unknown>(`/api/safety/incidents/${id}`, body)
  return toIncident(data)
}

export async function concludeIncident(
  id: string,
  body: ConcludeIncidentRequest,
): Promise<Incident> {
  const { data } = await api.post<unknown>(`/api/safety/incidents/${id}/conclude`, body)
  return toIncident(data)
}

/** Yumshoq o'chirish — server javobi `204`. */
export async function deleteIncident(id: string): Promise<void> {
  await api.delete(`/api/safety/incidents/${id}`)
}
