/**
 * Audit jurnali — API qatlami.
 *
 * Tiplar va chaqiruvlar GENERATSIYA QILINGAN (`./bonvi gen`, SPEC §0.3:
 * yangi chaqiruvlar qo'lda yozilmaydi). Bu yerda faqat yupqa o'ramlar:
 *
 * - sahifa raqamlari `number | string` dan `number` ga keltiriladi
 *   (Orval butun sonni «pattern» bilan satr ham deb e'lon qiladi, server
 *   esa son yuboradi — `attendance-api.ts` dagi `AsSent` izohi);
 * - ro'yxat sahifadan sahifaga o'tganda bo'shab qolmaydi (`placeholderData`);
 * - `enabled` — ruxsat tekshiruvi CHAQIRUVCHIDA: ruxsatsiz odamda so'rov
 *   umuman ketmaydi, konsolga behuda 403 tushmaydi.
 *
 * Kesh yangilanishi qo'lda emas: `providers.tsx` har bir muvaffaqiyatli
 * yozuvdan keyin hamma so'rovni eskirgan deb belgilaydi — xodim kartasida
 * buyruq tuzatilsa, tarix ham o'zi yangilanadi.
 */
import {
  useGetAuditEntry,
  useListAuditActors,
  useListAuditEntries,
  useListEmployeeHistory,
} from '@/shared/api/generated/audit/audit'
import type {
  AuditActorDto,
  AuditActorOptionDto,
  AuditChangeDto,
  AuditEntryDetailsDto,
  AuditEntryDto,
  ListAuditEntriesParams,
  ListEmployeeHistoryParams,
  PagedResultOfAuditEntryDto,
} from '@/shared/api/generated/model'

export type AuditEntry = AuditEntryDto
export type AuditChange = AuditChangeDto
export type AuditActor = AuditActorDto
export type AuditActorOption = AuditActorOptionDto
export type AuditEntryDetails = AuditEntryDetailsDto

/** Sahifa — sonlari haqiqiy `number`. */
export interface AuditPage {
  items: AuditEntry[]
  page: number
  total: number
}

function toPage(raw: PagedResultOfAuditEntryDto): AuditPage {
  return {
    items: raw.items,
    page: toNumber(raw.page, 1),
    total: toNumber(raw.total, 0),
  }
}

function toNumber(value: number | string, fallback: number): number {
  const parsed = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(parsed) ? parsed : fallback
}

/** Oldingi sahifa yangisi kelguncha ko'rinib turadi (buyruqlar ro'yxatidagi bilan bir xil). */
function keepPrevious<T>(previous: T | undefined): T | undefined {
  return previous
}

/** Jurnal ro'yxati — filtrlangan va sahifalangan. */
export function useAuditEntries(params: ListAuditEntriesParams, enabled: boolean) {
  return useListAuditEntries(params, {
    query: { enabled, placeholderData: keepPrevious, select: toPage },
  })
}

/** «Kim» filtri — jurnalda uchragan foydalanuvchilar. */
export function useAuditActors(enabled: boolean) {
  return useListAuditActors({
    query: { enabled, staleTime: 5 * 60 * 1000 },
  })
}

/**
 * Bitta qator: IP, qurilma va shu harakatning boshqa qatorlari.
 *
 * Qayta urinish YO'Q: `audit.topilmadi` (404 — yo'q yoki ko'rinmaydi)
 * qayta so'rash bilan tuzalmaydi.
 */
export function useAuditEntryDetails(id: string | null) {
  return useGetAuditEntry(id ?? '', {
    query: { enabled: id !== null, retry: false },
  })
}

/** Xodim kartasidagi «O'zgarishlar tarixi». */
export function useEmployeeHistory(
  employeeId: string,
  params: ListEmployeeHistoryParams,
  enabled: boolean,
) {
  return useListEmployeeHistory(employeeId, params, {
    query: {
      enabled: enabled && employeeId !== '',
      placeholderData: keepPrevious,
      select: toPage,
    },
  })
}
