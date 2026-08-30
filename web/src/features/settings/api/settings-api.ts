import axios from 'axios'

import { api } from '@/shared/api/http-client'

/** Ruxsat kodi — backend'dagi `Permissions.SettingsManage` bilan bir xil. */
export const SETTINGS_MANAGE = 'settings.manage'

/**
 * Sozlamaning turi.
 *
 * `Secret` — token va kalitlar. `Text` dan farqi TURIDA emas,
 * MUOMALADA: qiymati javobda hech qachon qaytmaydi (`value` har doim
 * bo'sh), o'rniga `isSet` va `preview` beriladi.
 */
export type SettingKind = 'Text' | 'Number' | 'Secret' | 'Photo'

/**
 * Bitta sozlama — qiymati va CHEGARALARI bilan.
 *
 * Chegaralar (`min`, `max`, `maxLength`) serverdan keladi, mijozda
 * takrorlanmaydi: ular domen qoidasi va ikki joyda yozilsa, biri
 * o'zgarganda ikkinchisi jimgina eskirardi. Forma ularni faqat
 * KO'RSATADI va oldindan tekshiradi — yakuniy so'z serverda.
 */
export interface SystemSetting {
  key: string
  /**
   * Joriy qiymat.
   *
   * ⚠️ `kind === 'Secret'` bo'lganda HAR DOIM bo'sh: sir serverdan
   * chiqmaydi — hatto uni qo'ygan super administratorga ham. Bu
   * mijozning cheklovi emas, serverning qoidasi.
   */
  value: string
  kind: SettingKind
  defaultValue: string
  min: number | null
  max: number | null
  maxLength: number | null
  /** Qiymat ATAYLAB qo'yilganmi. Sir uchun bu yagona ko'rsatkich. */
  isSet: boolean
  /** Sirning niqoblangan ko'rinishi (`8123456789:…abcd`). Sir bo'lmasa `null`. */
  preview: string | null
}

export interface SettingUpdate {
  key: string
  value: string
}

export async function fetchSettings(signal?: AbortSignal): Promise<SystemSetting[]> {
  const { data } = await api.get<unknown>('/api/settings', { signal })
  return Array.isArray(data) ? (data as SystemSetting[]) : []
}

export async function saveSettings(items: SettingUpdate[]): Promise<void> {
  await api.put('/api/settings', { items })
}

/**
 * Bot logotipi.
 *
 * ⚠️ `Photo` turidagi sozlama umumiy «saqlash» so'roviga TUSHMAYDI —
 * server uni rad etadi (`sozlama.qolda_ozgarmaydi`). Rasm alohida
 * yo'l bilan yuklanadi: u yerda hajm, tur va mazmun tekshiriladi,
 * eski fayl o'chiriladi. Shu sababli quyidagi uchta funksiya bor va
 * ular ekrandagi «saqlash» tugmasidan MUSTAQIL ishlaydi — rasm
 * tanlangan zahoti ketadi.
 */
export async function fetchBotLogo(signal?: AbortSignal): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>('/api/settings/bot/logo', {
      responseType: 'blob',
      signal,
    })
    return data
  } catch (error) {
    // 404 — logotip qo'yilmagan. Bu xato emas, odatiy hol.
    if (axios.isAxiosError(error) && error.response?.status === 404) return null
    throw error
  }
}

export async function uploadBotLogo(file: File): Promise<string | null> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<{ key?: unknown }>('/api/settings/bot/logo', form)

  return typeof data?.key === 'string' ? data.key : null
}

export async function deleteBotLogo(): Promise<void> {
  await api.delete('/api/settings/bot/logo')
}
