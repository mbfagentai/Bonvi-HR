/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Endpoint ALLAQACHON TAYYOR va testlangan (boshqa agent backend'ni
 * yozgan), shu sababli maydon nomlari «kutilayotgan shartnoma» emas —
 * ular haqiqiy manbadan olingan:
 *   api/src/Bonvi.Domain/Employees/Incentive.cs
 *   api/src/Bonvi.Api/Endpoints/IncentiveEndpoints.cs
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `employees-queries.ts` dagi hook'lar generatsiya qilinganiga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'
import type { CatalogItem } from '@/features/catalog/api/catalog-api'

/**
 * NEGA satr birlashmasi, C# enum'ining o'zi emas: `employees-api.ts`
 * dagi xuddi shu izohga qarang. Backend `JsonStringEnumConverter`
 * ro'yxatdan o'tkazilgan bo'lsa ham (`Program.cs`), ikkalasi ham
 * (raqam va satr) o'qiladi — shartnoma qaysi tomonga qotishidan qat'i
 * nazar interfeys ishlaydi.
 */
export const INCENTIVE_KINDS = ['award', 'penalty'] as const
export type IncentiveKindValue = (typeof INCENTIVE_KINDS)[number]

export interface IncentiveMoney {
  amount: number
  currency: string
}

/**
 * Ma'lumotnomadagi rag'bat/jarima TURI — `AwardType` yoki `PenaltyType`.
 *
 * `hasAmount` ikkalasida ham bor edi (S2 da qo'shilgan): shu maydonga
 * qarab forma summa maydonini ko'rsatadi yoki yashiradi.
 */
export interface IncentiveTypeItem extends CatalogItem {
  hasAmount: boolean
}

/** Rag'bat yoki jarima yozuvi (91.14 + HR so'ragan yangi jarima imkoniyati). */
export interface Incentive {
  id: string
  employeeId: string
  kind: IncentiveKindValue
  typeId: string
  /** Tur nomi — ro'yxatda ko'rsatish uchun, `id` yonida. */
  typeName: string | null
  date: string
  /** Faqat tur pul bilan bog'liq bo'lsa (`hasAmount`) to'ldirilgan bo'ladi. */
  amount: IncentiveMoney | null
  /**
   * Foiz («4» → «4 %») — faqat pulli JARIMADA, `amount` o'rniga: ikkalasi
   * hech qachon birga to'lmaydi (server cheklovi).
   */
  percent: number | null
  /** Faqat rag'bat uchun ma'noli — jarimada bo'lmaydi. */
  giftDescription: string | null
  /** Jarima uchun MAJBURIY. */
  reason: string | null
  /** Buyruq faqat rag'batda — jarima uchun buyruq chiqarilmaydi. */
  orderNo: string | null
  orderDate: string | null
}

/** `POST` tanasi — yangi yozuv berish. */
export interface GrantIncentiveRequest {
  kind: IncentiveKindValue
  typeId: string
  date: string
  amount: number | null
  /** Faqat jarimada, `amount` o'rniga. */
  percent: number | null
  giftDescription: string | null
  reason: string | null
  /** Faqat rag'batda — jarimada server `buyruq_jarimada` bilan rad etadi. */
  orderNo: string | null
  orderDate: string | null
}

/**
 * `PUT` tanasi — `kind` ATAYLAB YO'Q: tahrirlashda tur o'zgarmaydi,
 * backend ham rad etadi («rag'batni jarimaga aylantirish» — tuzatish
 * emas, boshqa fakt: yozuv o'chirilib, yangisi kiritiladi).
 */
export interface UpdateIncentiveRequest {
  typeId: string
  date: string
  amount: number | null
  percent: number | null
  giftDescription: string | null
  reason: string | null
  orderNo: string | null
  orderDate: string | null
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * `names` tartibi C# `IncentiveKind` enum'ining raqamli qiymatlariga mos
 * (1 dan boshlanadi): `Award = 1` → `names[0]`, `Penalty = 2` → `names[1]`.
 */
function enumValue<T extends string>(raw: unknown, names: readonly T[], fallback: T): T {
  if (typeof raw === 'number') return names[raw - 1] ?? fallback

  if (typeof raw === 'string') {
    const needle = raw.toLowerCase()
    return names.find((name) => name.toLowerCase() === needle) ?? fallback
  }

  return fallback
}

/** Frontend qiymatini C# enum nomiga qaytaradi: «award» → «Award». */
function enumName(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

/** `MoneyDto` — `{ amount, currency, formatted }`. `amount` son yoki satr bo'lishi mumkin. */
function money(value: unknown): IncentiveMoney | null {
  if (value === null || value === undefined) return null
  if (typeof value === 'number' && Number.isFinite(value)) return { amount: value, currency: 'UZS' }
  if (typeof value !== 'object') return null

  const box = value as Record<string, unknown>
  const raw = box['amount']
  const amount = typeof raw === 'number' ? raw : typeof raw === 'string' ? Number(raw) : null

  if (amount === null || !Number.isFinite(amount)) return null

  return { amount, currency: text(box['currency']) ?? 'UZS' }
}

/** Foiz — `decimal`, JSON'da son yoki (ba'zi sozlamalarda) satr. */
export function percentValue(value: unknown): number | null {
  const number = typeof value === 'number' ? value : typeof value === 'string' ? Number(value) : null
  return number !== null && Number.isFinite(number) ? number : null
}

function toIncentive(raw: unknown): Incentive {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    kind: enumValue(box['kind'], INCENTIVE_KINDS, 'award'),
    typeId: text(box['typeId']) ?? '',
    typeName: text(box['typeName']),
    date: text(box['date']) ?? '',
    amount: money(box['amount']),
    percent: percentValue(box['percent']),
    giftDescription: text(box['giftDescription']),
    reason: text(box['reason']),
    orderNo: text(box['orderNo']),
    orderDate: text(box['orderDate']),
  }
}

/** `kind` berilmasa — hammasi (rag'bat + jarima), sana bo'yicha kamayish tartibida (server saralaydi). */
export async function fetchIncentives(
  employeeId: string,
  kind: IncentiveKindValue | undefined,
  signal?: AbortSignal,
): Promise<Incentive[]> {
  const { data } = await api.get<unknown>(`/api/employees/${employeeId}/incentives`, {
    params: kind === undefined ? undefined : { kind: enumName(kind) },
    signal,
  })
  return Array.isArray(data) ? data.map(toIncentive) : []
}

export async function createIncentive(
  employeeId: string,
  body: GrantIncentiveRequest,
): Promise<Incentive> {
  const { data } = await api.post<unknown>(`/api/employees/${employeeId}/incentives`, {
    ...body,
    kind: enumName(body.kind),
  })
  return toIncentive(data)
}

export async function updateIncentive(
  employeeId: string,
  incentiveId: string,
  body: UpdateIncentiveRequest,
): Promise<Incentive> {
  const { data } = await api.put<unknown>(
    `/api/employees/${employeeId}/incentives/${incentiveId}`,
    body,
  )
  return toIncentive(data)
}

/** Yumshoq o'chirish — server javobi `204`. */
export async function deleteIncentive(employeeId: string, incentiveId: string): Promise<void> {
  await api.delete(`/api/employees/${employeeId}/incentives/${incentiveId}`)
}
