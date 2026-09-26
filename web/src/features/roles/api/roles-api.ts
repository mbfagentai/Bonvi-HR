/**
 * ⚠️ QO'LDA YOZILGAN KLIENT (`users-api.ts` boshidagi izohga qarang: bir
 * xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Maydon nomlari taxmin emas — rollar MVP shartnomasidan (§2–§3) olingan.
 * Backend shu shartnoma bo'yicha PARALLEL yozilgan, shu sababli bu fayl
 * generatsiya qilingan klientni kutmaydi. `./bonvi gen` dan keyin ham u
 * qolgan bo'limlar kabi o'z tipini saqlaydi (generatsiya qilingan kod
 * git'ga tushmaydi).
 */
import { api } from '@/shared/api/http-client'

import type { GrantLevel } from '../lib/role-keys'

/**
 * Bitta rol — ro'yxatda ham, tahrirlash oynasida ham.
 *
 * `code` ekranda HECH QACHON ko'rsatilmaydi: yangi rolga server
 * `custom_<8 hex>` kodini o'zi beradi va u odam uchun ma'nosiz. Kod faqat
 * ikki joyda kerak — foydalanuvchi formasidagi tanlov qiymati va
 * «himoyalangan super administrator» ni tanish.
 */
export interface RoleSummary {
  id: string
  code: string
  /** Egasi o'zi yozgan nom — o'zgartirishsiz ko'rsatiladi. */
  name: string
  /** Faqat `super_admin` (va ro'yxatga umuman tushmaydigan bot roli). */
  isProtected: boolean
  permissions: string[]
  /** Shu roldagi hisoblar — faol va faolsiz birga (o'chirish qoidasi ham shunday sanaydi). */
  userCount: number
}

/**
 * Bo'limning darajalari. Faqat bo'lim TAKLIF qiladiganlari bor: masalan
 * «Foydalanuvchilar» da yagona `full`. Har bir ro'yxat O'SIB boradi —
 * yuqori daraja pastkisining kodlarini ham o'z ichiga oladi.
 */
export type RoleModuleLevels = Partial<Record<GrantLevel, string[]>>

export interface RoleCatalogModule {
  key: string
  levels: RoleModuleLevels
  /**
   * Bo'limning alohida amallari (hozir faqat mehnat munosabatlarida).
   * Ular bo'lim kamida «Ko'rish» bo'lganda ma'noli.
   */
  actions: string[]
}

export interface RoleCatalog {
  modules: RoleCatalogModule[]
  /** Hech bir bo'limga kirmaydigan ruxsatlar (`salary.view`). */
  extras: string[]
}

/** Yaratish va tahrirlashda bir xil tana — tahrirlash TO'LIQ almashtiradi. */
export interface RoleInput {
  name: string
  permissions: string[]
}

/** Rol nomining chegarasi — server bilan bir xil (`rol.nom_uzun`). */
export const ROLE_NAME_MAX_LENGTH = 100

/**
 * Rollar ro'yxati. Server tartiblab beradi (himoyalangan birinchi, keyin
 * nom bo'yicha) va botning texnik rolini umuman qaytarmaydi.
 */
export async function fetchRoles(signal?: AbortSignal): Promise<RoleSummary[]> {
  const { data } = await api.get<unknown>('/api/roles', { signal })
  return Array.isArray(data) ? data.map(toRole) : []
}

/** Ruxsat katalogi — kodda aniqlangan, har deploy'da o'zgarishi mumkin. */
export async function fetchRoleCatalog(signal?: AbortSignal): Promise<RoleCatalog> {
  const { data } = await api.get<unknown>('/api/roles/catalog', { signal })
  return toCatalog(data)
}

export async function createRole(body: RoleInput): Promise<RoleSummary> {
  const { data } = await api.post<unknown>('/api/roles', body)
  return toRole(data)
}

export async function updateRole(id: string, body: RoleInput): Promise<RoleSummary> {
  const { data } = await api.put<unknown>(`/api/roles/${id}`, body)
  return toRole(data)
}

/** Javob: 204. Rolda hisob bo'lsa server 409 (`rol.foydalanuvchisi_bor`) qaytaradi. */
export async function deleteRole(id: string): Promise<void> {
  await api.delete(`/api/roles/${id}`)
}

/* ─────────────────────────────────────────────────── xaritalash (map) */

function toRole(raw: unknown): RoleSummary {
  const box = (raw ?? {}) as Record<string, unknown>
  const code = text(box['code']) ?? ''

  return {
    id: text(box['id']) ?? '',
    code,
    // Nomsiz rol bo'lmaydi (server `rol.nom_bosh` bilan rad etadi), lekin
    // bo'sh qator ro'yxatda «ko'rinmas» qator bo'lib qolmasin — kod chiqadi.
    name: text(box['name']) ?? code,
    isProtected: box['isProtected'] === true,
    permissions: strings(box['permissions']),
    userCount: typeof box['userCount'] === 'number' ? box['userCount'] : 0,
  }
}

function toCatalog(raw: unknown): RoleCatalog {
  const box = (raw ?? {}) as Record<string, unknown>
  const modules = Array.isArray(box['modules']) ? box['modules'] : []

  return {
    modules: modules.map(toModule).filter((module) => module.key !== ''),
    extras: strings(box['extras']),
  }
}

function toModule(raw: unknown): RoleCatalogModule {
  const box = (raw ?? {}) as Record<string, unknown>
  const levelsBox = (box['levels'] ?? {}) as Record<string, unknown>

  /*
    Faqat tanish uchta daraja o'qiladi. Notanish kalit (masalan kelajakda
    qo'shilgan to'rtinchi daraja) jimgina tashlab yuboriladi: segmentli
    boshqaruv uni chiza olmaydi, yarim chizilgan tanlov esa rolni
    noto'g'ri saqlashga olib kelardi.
  */
  const levels: RoleModuleLevels = {}
  for (const level of ['view', 'edit', 'full'] as const) {
    const codes = strings(levelsBox[level])
    if (codes.length > 0) levels[level] = codes
  }

  return {
    key: text(box['key']) ?? '',
    levels,
    actions: strings(box['actions']),
  }
}

function strings(value: unknown): string[] {
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string' && item !== '')
    : []
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}
