import type { AuditAction } from '@/shared/api/generated/model'
import { tashkentParts } from '@/shared/lib/timezone'

import {
  AUDIT_DEFAULT_PERIOD_DAYS,
  isAuditAction,
  isAuditEntity,
  isAuditModule,
  type AuditEntityName,
  type AuditModuleKey,
} from './audit-keys'

/**
 * Audit jurnali sahifasining holati — manzil satrida (`/buyruqlar` naqshi,
 * `employments-search.ts`). Sabab o'sha: filtr bilan sahifani ochib, yozuv
 * tafsilotiga, keyin xodim kartasiga o'tib, «orqaga» bosgan HR o'sha
 * filtrga qaytishi kerak. Xodim kartasidagi «Tuzatishlarni ko'rish»
 * havolasi ham aynan shu parametrlar bilan ishlaydi.
 *
 * Barcha maydonlar IXTIYORIY va standart qiymat manzilga yozilmaydi.
 */
export interface AuditSearch {
  /** 1 dan boshlanadi. 1 — standart, manzilda ko'rinmaydi. */
  page?: number
  /** Davr boshi — `DateOnly` («2026-10-01»). Berilmasa — `to` dan 29 kun oldin. */
  from?: string
  /** Davr oxiri. Berilmasa — bugun (Toshkent). */
  to?: string
  /** Foydalanuvchi identifikatori yoki `system`. */
  actor?: string
  module?: AuditModuleKey
  /** Entity turi (`EmploymentEvent`). */
  entity?: AuditEntityName
  action?: AuditAction
  /** Faqat `security` yoziladi — «O'zgarishlar» standart. */
  kind?: 'security'
  /** Bitta yozuv — xodim kartasidagi havoladan keladi («Bitta yozuv» chipi). */
  entityId?: string
}

/** Ekrandagi ko'rinish: o'zgarishlar yoki kirish va xavfsizlik. */
export type AuditKind = 'changes' | 'security'

/** «Kim» filtridagi tizim tanlovi — server shu so'zni taniydi. */
export const ACTOR_SYSTEM = 'system'

/**
 * Manzil satridan kelgan qiymatlarni tekshiradi.
 *
 * Hech qachon xato tashlamaydi: noto'g'ri qiymat («?module=abc») shunchaki
 * tashlab yuboriladi — sahifa ochiladi, server esa 400 (`audit.filtr_notogri`)
 * qaytarmaydi.
 */
export function validateAuditSearch(search: Record<string, unknown>): AuditSearch {
  return {
    page: readPage(search['page']),
    from: readDate(search['from']),
    to: readDate(search['to']),
    actor: readActor(search['actor']),
    module: readModule(search['module']),
    entity: readEntity(search['entity']),
    action: readAction(search['action']),
    kind: search['kind'] === 'security' ? 'security' : undefined,
    entityId: readEntityId(search['entityId']),
  }
}

/**
 * Amaldagi davr — server qoidasi bilan AYNAN bir xil (`ListAuditEntriesQuery`):
 * oxiri berilmasa — bugun, boshi berilmasa — oxiridan 29 kun oldin. Faqat
 * bittasi berilsa ham qolgani shu qoida bilan to'ladi, ya'ni maydonlarda
 * ko'rinib turgan sanalar va kelgan ro'yxat hech qachon farq qilmaydi.
 */
export function effectivePeriod(search: AuditSearch, today: string): { from: string; to: string } {
  const to = search.to ?? today
  const from = search.from ?? shiftIsoDate(to, -(AUDIT_DEFAULT_PERIOD_DAYS - 1))
  return { from, to }
}

/**
 * Foydalanuvchi biror filtr qo'yganmi — bo'sh natija xabari shunga qarab
 * tanlanadi. Sahifa va ko'rinish hisobga OLINMAYDI: ular ro'yxatni
 * qisqartirmaydi.
 */
export function isAuditFiltered(search: AuditSearch): boolean {
  return (
    search.from !== undefined ||
    search.to !== undefined ||
    search.actor !== undefined ||
    search.module !== undefined ||
    search.entity !== undefined ||
    search.action !== undefined ||
    search.entityId !== undefined
  )
}

/**
 * Sanani kunlarga siljitadi — SOF son arifmetikasi, mintaqasiz (`Date.UTC`):
 * `date-value.ts` izohidagi sabab — `new Date(satr)` brauzer mintaqasida
 * kunni siljitib yuborardi.
 */
export function shiftIsoDate(value: string, days: number): string {
  const [year, month, day] = value.split('-').map(Number)
  const shifted = new Date(Date.UTC(year ?? 1970, (month ?? 1) - 1, (day ?? 1) + days))

  const y = String(shifted.getUTCFullYear()).padStart(4, '0')
  const m = String(shifted.getUTCMonth() + 1).padStart(2, '0')
  const d = String(shifted.getUTCDate()).padStart(2, '0')

  return `${y}-${m}-${d}`
}

/**
 * Lahzaning (UTC) TOSHKENTDAGI sanasi — «2026-10-12T19:30:00Z» → «2026-10-13».
 * Server davrni Toshkent kunlari bilan kesadi, havola ham shunday yasalishi kerak.
 */
export function tashkentDateOf(instant: string): string | null {
  const date = new Date(instant)
  if (Number.isNaN(date.getTime())) return null

  const { year, month, day } = tashkentParts(date)
  return `${year}-${month}-${day}`
}

/* ─────────────────────────────────────────────────────────── o'qish */

function readPage(value: unknown): number | undefined {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 2) return undefined
  return value
}

/** `DateOnly` shakli — boshqasi backend'ga 400 bilan qaytardi. */
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/u

function readDate(value: unknown): string | undefined {
  return typeof value === 'string' && DATE_ONLY.test(value) ? value : undefined
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu

function readActor(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined
  return value === ACTOR_SYSTEM || UUID.test(value) ? value : undefined
}

function readModule(value: unknown): AuditModuleKey | undefined {
  return typeof value === 'string' && isAuditModule(value) ? value : undefined
}

function readEntity(value: unknown): AuditEntityName | undefined {
  return typeof value === 'string' && isAuditEntity(value) ? value : undefined
}

function readAction(value: unknown): AuditAction | undefined {
  return typeof value === 'string' && isAuditAction(value) ? value : undefined
}

/**
 * Yozuv identifikatori — odatda GUID, lekin `entity_id` satr (masalan
 * sozlama kaliti). Shu sababli faqat xavfsiz belgilar va uzunlik tekshiriladi.
 *
 * ⚠️ Router satrga o'xshagan qiymatni `JSON.parse` dan o'tkazadi — raqamli
 * kalit son bo'lib kelishi mumkin, u ham satrga qaytariladi.
 */
const ENTITY_ID = /^[\w.:-]{1,100}$/u

function readEntityId(value: unknown): string | undefined {
  const text = typeof value === 'number' ? String(value) : value
  return typeof text === 'string' && ENTITY_ID.test(text) ? text : undefined
}
